using Mirror;
using UnityEngine;

namespace ElitesAndPawns.Player
{
    /// <summary>
    /// Handles player movement, jumping, and camera control.
    /// Uses Unity's new Input System and CharacterController.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float sprintMultiplier = 1.5f;
        [SerializeField] private float jumpForce = 5f;
        [SerializeField] private float gravity = -9.81f;

        [Header("Camera")]
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float maxLookAngle = 80f;
        [SerializeField] private Transform cameraTransform;

        [Header("Ground Check")]
        [SerializeField] private float groundCheckDistance = 0.4f;
        [SerializeField] private LayerMask groundMask = -1; // Everything by default

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Components
        private CharacterController characterController;
        
        // Movement state
        private Vector3 velocity;
        private bool isGrounded;
        private float verticalRotation = 0f;

        // Input
        private Vector2 moveInput;
        private Vector2 lookInput;
        private bool jumpPressed;
        private bool sprintPressed;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();

            // Find or create camera
            if (cameraTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    cameraTransform = cam.transform;
                }
                else
                {
                    // Create camera if doesn't exist
                    GameObject cameraObj = new GameObject("PlayerCamera");
                    cameraObj.tag = "MainCamera"; // TAG IT SO Camera.main WORKS!
                    cameraObj.transform.SetParent(transform);
                    cameraObj.transform.localPosition = new Vector3(0, 0.6f, 0); // Eye level
                    cameraTransform = cameraObj.transform;
                    Camera newCam = cameraObj.AddComponent<Camera>();
                    newCam.enabled = false; // Disabled by default
                }
            }
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            // Lock cursor for local player
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Enable camera for local player only
            if (cameraTransform != null)
            {
                Camera cam = cameraTransform.GetComponent<Camera>();
                if (cam != null)
                {
                    cam.enabled = true;
                    // Make sure it's tagged
                    if (cam.tag != "MainCamera")
                    {
                        cam.tag = "MainCamera";
                    }
                }
            }

            if (debugMode)
            {
                Debug.Log("[PlayerController] Local player controller initialized");
                Debug.Log($"[PlayerController] Camera: {cameraTransform?.name}, Tag: {cameraTransform?.tag}");
            }
        }

        private void Update()
        {
            // Only process input for local player
            if (!isLocalPlayer) return;

            // Handle input
            HandleInput();

            // Ground check
            CheckGrounded();

            // Handle movement
            HandleMovement();

            // Handle camera
            HandleCamera();

            // Unlock cursor with ESC
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // Re-lock cursor on click
            if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        /// <summary>
        /// Handle player input (using legacy Input for MVP, will upgrade to Input System later)
        /// </summary>
        private void HandleInput()
        {
            // Movement input
            float horizontal = Input.GetAxis("Horizontal"); // A/D or Left/Right arrows
            float vertical = Input.GetAxis("Vertical");     // W/S or Up/Down arrows
            moveInput = new Vector2(horizontal, vertical);

            // Look input
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            lookInput = new Vector2(mouseX, mouseY);

            // Jump input
            jumpPressed = Input.GetButtonDown("Jump"); // Spacebar

            // Sprint input
            sprintPressed = Input.GetKey(KeyCode.LeftShift);
        }

        /// <summary>
        /// Check if player is on the ground - FIXED VERSION
        /// </summary>
        private void CheckGrounded()
        {
            // Check a sphere at the bottom of the character controller
            float checkDistance = (characterController.height / 2f) + 0.1f;
            Vector3 spherePosition = transform.position - new Vector3(0, checkDistance, 0);
            float sphereRadius = characterController.radius * 0.9f;
            
            // Use CheckSphere for detection
            isGrounded = Physics.CheckSphere(spherePosition, sphereRadius, groundMask);

            // Additional check: make sure we're not detecting ourselves
            if (isGrounded)
            {
                // Verify with a raycast
                bool raycastCheck = Physics.Raycast(
                    transform.position,
                    Vector3.down,
                    checkDistance + sphereRadius,
                    groundMask
                );
                
                isGrounded = raycastCheck;
            }

            // Debug visualization
            if (debugMode)
            {
                Color debugColor = isGrounded ? Color.green : Color.red;
                Debug.DrawRay(transform.position, Vector3.down * (checkDistance + sphereRadius), debugColor);
            }
        }

        /// <summary>
        /// Handle player movement and jumping - FIXED VERSION
        /// </summary>
        private void HandleMovement()
        {
            // Calculate move direction
            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
            
            // Apply speed
            float currentSpeed = sprintPressed ? moveSpeed * sprintMultiplier : moveSpeed;
            Vector3 moveVelocity = move * currentSpeed;

            // Apply movement (horizontal)
            characterController.Move(moveVelocity * Time.deltaTime);

            // Handle jumping - only if grounded
            if (jumpPressed && isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
                
                if (debugMode)
                {
                    Debug.Log($"[PlayerController] JUMP! Velocity.y: {velocity.y:F2}");
                }
            }

            // Apply gravity
            velocity.y += gravity * Time.deltaTime;
            
            // Clamp falling velocity
            velocity.y = Mathf.Max(velocity.y, -50f);

            // CRITICAL FIX: Reset velocity AFTER applying gravity if grounded
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f; // Small downward force to keep grounded
            }

            // Apply vertical velocity
            characterController.Move(velocity * Time.deltaTime);
        }

        /// <summary>
        /// Handle camera rotation (first-person look)
        /// </summary>
        private void HandleCamera()
        {
            if (cameraTransform == null) return;

            // Horizontal rotation (rotate player body)
            float horizontalRotation = lookInput.x * mouseSensitivity;
            transform.Rotate(Vector3.up * horizontalRotation);

            // Vertical rotation (rotate camera)
            verticalRotation -= lookInput.y * mouseSensitivity;
            verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
            cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        }

        /// <summary>
        /// Set move input (for external control or Input System)
        /// </summary>
        public void SetMoveInput(Vector2 input)
        {
            moveInput = input;
        }

        /// <summary>
        /// Trigger jump (for external control or Input System)
        /// </summary>
        public void Jump()
        {
            if (isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            }
        }

        /// <summary>
        /// Draw gizmos for debugging
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!debugMode || characterController == null) return;

            // Draw ground check sphere at the bottom of character
            Gizmos.color = isGrounded ? Color.green : Color.red;
            float checkDistance = (characterController.height / 2f) + 0.1f;
            Vector3 spherePosition = transform.position - new Vector3(0, checkDistance, 0);
            Gizmos.DrawWireSphere(spherePosition, characterController.radius * 0.9f);
            
            // Draw a line from player to sphere check position
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, spherePosition);
        }
    }
}
