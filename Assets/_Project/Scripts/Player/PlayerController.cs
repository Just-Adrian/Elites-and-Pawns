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
                }
            }

            if (debugMode)
            {
                Debug.Log("[PlayerController] Local player controller initialized");
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
        /// Check if player is on the ground - IMPROVED VERSION
        /// </summary>
        private void CheckGrounded()
        {
            // Use CharacterController's built-in check as primary
            isGrounded = characterController.isGrounded;

            // Additional sphere check for more reliable detection
            Vector3 spherePosition = transform.position + new Vector3(0, 0.1f, 0);
            bool sphereCheck = Physics.CheckSphere(spherePosition, characterController.radius * 0.9f, groundMask);
            
            // Grounded if either check succeeds
            isGrounded = isGrounded || sphereCheck;

            // IMPORTANT: Reset velocity.y when grounded to prevent accumulation
            if (isGrounded)
            {
                if (velocity.y < 0)
                {
                    velocity.y = -2f; // Small negative value keeps us grounded
                }
            }

            // Debug visualization
            if (debugMode)
            {
                Debug.DrawRay(transform.position, Vector3.down * groundCheckDistance, isGrounded ? Color.green : Color.red);
            }
        }

        /// <summary>
        /// Handle player movement and jumping
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
                    Debug.Log($"[PlayerController] Jump! Velocity.y set to: {velocity.y}");
                }
            }

            // Apply gravity
            velocity.y += gravity * Time.deltaTime;

            // Clamp falling velocity to prevent extreme speeds
            velocity.y = Mathf.Max(velocity.y, -50f);

            // Apply vertical velocity
            characterController.Move(velocity * Time.deltaTime);

            // Debug info
            if (debugMode && jumpPressed)
            {
                Debug.Log($"[PlayerController] IsGrounded: {isGrounded}, Velocity.y: {velocity.y}");
            }
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

            // Draw ground check sphere
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Vector3 spherePosition = transform.position + new Vector3(0, 0.1f, 0);
            Gizmos.DrawWireSphere(spherePosition, characterController.radius * 0.9f);
        }
    }
}
