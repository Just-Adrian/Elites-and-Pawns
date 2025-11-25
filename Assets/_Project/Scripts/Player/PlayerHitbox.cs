using UnityEngine;

namespace ElitesAndPawns.Player
{
    /// <summary>
    /// Dedicated hitbox collider for player damage detection.
    /// Separate from CharacterController to ensure reliable hit detection from all angles.
    /// 
    /// SETUP INSTRUCTIONS:
    /// 1. Add this component to your Player prefab
    /// 2. It will automatically create a Capsule Collider for hit detection
    /// 3. Adjust the size in the Inspector if needed
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerHitbox : MonoBehaviour
    {
        [Header("Hitbox Settings")]
        [SerializeField] private float capsuleRadius = 0.4f;
        [SerializeField] private float capsuleHeight = 1.8f;
        [SerializeField] private Vector3 capsuleCenter = new Vector3(0, 0.9f, 0);
        
        [Header("Debug")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Color gizmoColor = new Color(0, 1, 0, 0.3f);
        
        private CapsuleCollider hitboxCollider;
        private PlayerHealth playerHealth;
        
        private void Awake()
        {
            SetupHitbox();
            playerHealth = GetComponent<PlayerHealth>();
        }
        
        /// <summary>
        /// Setup the hitbox collider
        /// </summary>
        private void SetupHitbox()
        {
            hitboxCollider = GetComponent<CapsuleCollider>();
            
            // Configure the capsule collider
            hitboxCollider.isTrigger = true; // CRITICAL: Must be trigger for projectiles to detect
            hitboxCollider.radius = capsuleRadius;
            hitboxCollider.height = capsuleHeight;
            hitboxCollider.center = capsuleCenter;
            
            // Make sure it's on the correct layer
            if (gameObject.layer == LayerMask.NameToLayer("Default"))
            {
                Debug.LogWarning("[PlayerHitbox] Player is on Default layer. Consider using a dedicated 'Player' layer for better collision control.");
            }
            
            Debug.Log($"[PlayerHitbox] Hitbox configured: Radius={capsuleRadius}, Height={capsuleHeight}, Center={capsuleCenter}");
        }
        
        /// <summary>
        /// Validate settings and provide feedback
        /// </summary>
        private void OnValidate()
        {
            // Ensure positive values
            capsuleRadius = Mathf.Max(0.1f, capsuleRadius);
            capsuleHeight = Mathf.Max(0.5f, capsuleHeight);
            
            // Update collider if it exists
            if (hitboxCollider != null)
            {
                hitboxCollider.radius = capsuleRadius;
                hitboxCollider.height = capsuleHeight;
                hitboxCollider.center = capsuleCenter;
            }
        }
        
        /// <summary>
        /// Draw the hitbox in the editor
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            
            Gizmos.color = gizmoColor;
            
            // Draw the capsule hitbox
            Vector3 worldCenter = transform.TransformPoint(capsuleCenter);
            
            // Calculate capsule end points
            float halfHeight = capsuleHeight / 2f;
            Vector3 topSphere = worldCenter + Vector3.up * (halfHeight - capsuleRadius);
            Vector3 bottomSphere = worldCenter - Vector3.up * (halfHeight - capsuleRadius);
            
            // Draw the capsule
            DrawWireCapsule(worldCenter, capsuleRadius, capsuleHeight);
        }
        
        /// <summary>
        /// Helper method to draw a wire capsule
        /// </summary>
        private void DrawWireCapsule(Vector3 center, float radius, float height)
        {
            float halfHeight = height / 2f;
            Vector3 top = center + Vector3.up * (halfHeight - radius);
            Vector3 bottom = center - Vector3.up * (halfHeight - radius);
            
            // Draw spheres at top and bottom
            Gizmos.DrawWireSphere(top, radius);
            Gizmos.DrawWireSphere(bottom, radius);
            
            // Draw connecting lines
            Gizmos.DrawLine(top + Vector3.forward * radius, bottom + Vector3.forward * radius);
            Gizmos.DrawLine(top - Vector3.forward * radius, bottom - Vector3.forward * radius);
            Gizmos.DrawLine(top + Vector3.right * radius, bottom + Vector3.right * radius);
            Gizmos.DrawLine(top - Vector3.right * radius, bottom - Vector3.right * radius);
        }
        
        /// <summary>
        /// OPTIONAL: Auto-fit the hitbox to match a visual mesh
        /// Call this method if you want to automatically size the hitbox
        /// </summary>
        [ContextMenu("Auto-Fit to Mesh Bounds")]
        private void AutoFitToMesh()
        {
            Renderer renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;
                
                // Convert world bounds to local space
                Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
                
                // Set capsule properties
                capsuleRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
                capsuleHeight = bounds.size.y;
                capsuleCenter = localCenter;
                
                // Update the collider
                SetupHitbox();
                
                Debug.Log($"[PlayerHitbox] Auto-fitted to mesh bounds: Radius={capsuleRadius:F2}, Height={capsuleHeight:F2}");
            }
            else
            {
                Debug.LogWarning("[PlayerHitbox] No renderer found on player or children. Cannot auto-fit.");
            }
        }
        
        /// <summary>
        /// Match the CharacterController size
        /// </summary>
        [ContextMenu("Match CharacterController Size")]
        private void MatchCharacterController()
        {
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                capsuleRadius = cc.radius;
                capsuleHeight = cc.height;
                capsuleCenter = cc.center;
                
                SetupHitbox();
                
                Debug.Log($"[PlayerHitbox] Matched CharacterController: Radius={capsuleRadius:F2}, Height={capsuleHeight:F2}");
            }
            else
            {
                Debug.LogWarning("[PlayerHitbox] No CharacterController found on player.");
            }
        }
    }
}
