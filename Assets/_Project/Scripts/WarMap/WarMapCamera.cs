using UnityEngine;

namespace ElitesAndPawns.WarMap
{
    /// <summary>
    /// Top-down camera controller for the War Map.
    /// Supports panning with WASD/arrow keys or middle mouse drag, and zooming with scroll wheel.
    /// </summary>
    public class WarMapCamera : MonoBehaviour
    {
        #region Singleton
        
        private static WarMapCamera _instance;
        public static WarMapCamera Instance => _instance;
        
        #endregion
        
        [Header("Camera Settings")]
        [SerializeField] private float panSpeed = 20f;
        [SerializeField] private float panSpeedMultiplier = 2f; // When holding shift
        [SerializeField] private float zoomSpeed = 5f;
        [SerializeField] private float minZoom = 5f;
        [SerializeField] private float maxZoom = 30f;
        [SerializeField] private float smoothTime = 0.1f;
        
        [Header("Bounds")]
        [SerializeField] private bool useBounds = true;
        [SerializeField] private Vector2 boundsMin = new Vector2(-20f, -20f);
        [SerializeField] private Vector2 boundsMax = new Vector2(20f, 20f);
        
        [Header("Initial Position")]
        [SerializeField] private Vector3 initialPosition = new Vector3(0f, 15f, 0f);
        [SerializeField] private float initialZoom = 15f;
        
        private Camera cam;
        private Vector3 targetPosition;
        private float targetZoom;
        private Vector3 velocity;
        private float zoomVelocity;
        
        // Drag state
        private bool isDragging;
        private Vector3 dragStartScreenPos;
        private Vector3 dragStartCameraPos;
        
        #region Properties
        
        public float CurrentZoom => cam != null ? cam.orthographicSize : targetZoom;
        public Vector3 Position => transform.position;
        
        #endregion
        
        #region Unity Lifecycle
        
        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            
            cam = GetComponent<Camera>();
            if (cam == null)
            {
                cam = gameObject.AddComponent<Camera>();
            }
            
            // Setup camera for top-down view
            cam.orthographic = true;
            cam.orthographicSize = initialZoom;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
            
            // Position camera looking down
            transform.position = initialPosition;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            
            targetPosition = transform.position;
            targetZoom = initialZoom;
        }
        
        void Update()
        {
            HandleKeyboardPan();
            HandleMouseDrag();
            HandleZoom();
            
            ApplyMovement();
        }
        
        #endregion
        
        #region Input Handling
        
        void HandleKeyboardPan()
        {
            Vector3 moveDir = Vector3.zero;
            
            // WASD and Arrow keys
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                moveDir.z += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                moveDir.z -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                moveDir.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                moveDir.x += 1f;
            
            if (moveDir != Vector3.zero)
            {
                float speed = panSpeed;
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    speed *= panSpeedMultiplier;
                
                // Scale pan speed based on zoom level
                float zoomFactor = targetZoom / initialZoom;
                
                targetPosition += moveDir.normalized * speed * zoomFactor * Time.deltaTime;
            }
        }
        
        void HandleMouseDrag()
        {
            // Middle mouse or right mouse to drag
            if (Input.GetMouseButtonDown(2) || Input.GetMouseButtonDown(1))
            {
                isDragging = true;
                dragStartScreenPos = Input.mousePosition;
                dragStartCameraPos = targetPosition;
            }
            
            if (Input.GetMouseButtonUp(2) || Input.GetMouseButtonUp(1))
            {
                isDragging = false;
            }
            
            if (isDragging)
            {
                // Calculate screen-space delta
                Vector3 screenDelta = Input.mousePosition - dragStartScreenPos;
                
                // Convert to world units based on current zoom
                // Orthographic size is half the vertical view, so multiply by 2
                float worldUnitsPerPixel = (cam.orthographicSize * 2f) / Screen.height;
                
                // Apply delta (inverted because dragging moves the view, not the camera target)
                Vector3 worldDelta = new Vector3(
                    -screenDelta.x * worldUnitsPerPixel,
                    0,
                    -screenDelta.y * worldUnitsPerPixel
                );
                
                targetPosition = dragStartCameraPos + worldDelta;
            }
        }
        
        void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            
            if (Mathf.Abs(scroll) > 0.01f)
            {
                targetZoom -= scroll * zoomSpeed * targetZoom * 0.5f;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            }
        }
        
        void ApplyMovement()
        {
            // Clamp to bounds
            if (useBounds)
            {
                targetPosition.x = Mathf.Clamp(targetPosition.x, boundsMin.x, boundsMax.x);
                targetPosition.z = Mathf.Clamp(targetPosition.z, boundsMin.y, boundsMax.y);
            }
            
            // Keep Y position fixed (height)
            targetPosition.y = initialPosition.y;
            
            // Smooth movement
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
            
            // Smooth zoom
            if (cam != null)
            {
                cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetZoom, ref zoomVelocity, smoothTime);
            }
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Get world position of mouse on the horizontal plane (Y=0).
        /// </summary>
        public Vector3 GetMouseWorldPosition()
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            
            if (plane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }
            
            return Vector3.zero;
        }
        
        /// <summary>
        /// Focus camera on a specific world position.
        /// </summary>
        public void FocusOn(Vector3 worldPosition)
        {
            targetPosition = new Vector3(worldPosition.x, initialPosition.y, worldPosition.z);
        }
        
        /// <summary>
        /// Focus camera on a specific node.
        /// </summary>
        public void FocusOnNode(WarMapNode node)
        {
            if (node != null)
            {
                FocusOn(node.transform.position);
            }
        }
        
        /// <summary>
        /// Set zoom level directly.
        /// </summary>
        public void SetZoom(float zoom)
        {
            targetZoom = Mathf.Clamp(zoom, minZoom, maxZoom);
        }
        
        /// <summary>
        /// Reset camera to initial position and zoom.
        /// </summary>
        public void ResetView()
        {
            targetPosition = initialPosition;
            targetZoom = initialZoom;
        }
        
        #endregion
    }
}
