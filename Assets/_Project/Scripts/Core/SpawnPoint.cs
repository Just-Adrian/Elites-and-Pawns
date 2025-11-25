using UnityEngine;

namespace ElitesAndPawns.Core
{
    /// <summary>
    /// Defines a spawn point for players with team assignment.
    /// Place these in the scene to define where players spawn based on their team.
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        [Header("Spawn Configuration")]
        [SerializeField] private FactionType teamOwner = FactionType.None;
        [SerializeField] private bool isActiveSpawnPoint = true;
        [SerializeField] private float spawnRadius = 2f; // Random spawn within this radius
        
        [Header("Visual Feedback")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private float gizmoSize = 1f;

        // Properties
        public FactionType TeamOwner => teamOwner;
        public bool IsActive => isActiveSpawnPoint;

        /// <summary>
        /// Get a spawn position with some random offset within the spawn radius
        /// </summary>
        public Vector3 GetSpawnPosition()
        {
            if (spawnRadius > 0)
            {
                Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
                return transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
            }
            return transform.position;
        }

        /// <summary>
        /// Get the spawn rotation (uses the transform's rotation)
        /// </summary>
        public Quaternion GetSpawnRotation()
        {
            return transform.rotation;
        }

        /// <summary>
        /// Check if this spawn point is valid for a given team
        /// </summary>
        public bool IsValidForTeam(FactionType team)
        {
            // If spawn point has no team assignment, it's valid for anyone
            if (teamOwner == FactionType.None)
                return true;

            // Otherwise, must match the team
            return teamOwner == team;
        }

        /// <summary>
        /// Set the team owner of this spawn point
        /// </summary>
        public void SetTeamOwner(FactionType newTeam)
        {
            teamOwner = newTeam;
        }

        /// <summary>
        /// Enable or disable this spawn point
        /// </summary>
        public void SetActive(bool active)
        {
            isActiveSpawnPoint = active;
        }

        /// <summary>
        /// Find a random spawn point for a specific team
        /// </summary>
        public static SpawnPoint GetRandomSpawnPoint(FactionType team)
        {
            SpawnPoint[] allSpawnPoints = FindObjectsOfType<SpawnPoint>();
            
            // Filter to active spawn points for this team
            System.Collections.Generic.List<SpawnPoint> validSpawnPoints = 
                new System.Collections.Generic.List<SpawnPoint>();
            
            foreach (SpawnPoint sp in allSpawnPoints)
            {
                if (sp.IsActive && sp.IsValidForTeam(team))
                {
                    validSpawnPoints.Add(sp);
                }
            }
            
            // Return random valid spawn point
            if (validSpawnPoints.Count > 0)
            {
                return validSpawnPoints[Random.Range(0, validSpawnPoints.Count)];
            }
            
            // Fallback: return any active spawn point
            foreach (SpawnPoint sp in allSpawnPoints)
            {
                if (sp.IsActive)
                {
                    return sp;
                }
            }
            
            // No spawn points found
            return null;
        }

        // Unity Editor visualization
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            // Set color based on team
            Color gizmoColor = GetTeamColor();
            gizmoColor.a = 0.5f;
            Gizmos.color = gizmoColor;

            // Draw spawn point
            Gizmos.DrawCube(transform.position, Vector3.one * gizmoSize);
            
            // Draw spawn radius
            if (spawnRadius > 0)
            {
                Gizmos.DrawWireSphere(transform.position, spawnRadius);
            }

            // Draw direction arrow
            Gizmos.color = gizmoColor;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;

            // Draw more prominent when selected
            Color gizmoColor = GetTeamColor();
            Gizmos.color = gizmoColor;

            Gizmos.DrawWireCube(transform.position, Vector3.one * (gizmoSize * 1.2f));
            
            if (spawnRadius > 0)
            {
                // Draw filled circle on ground
                int segments = 32;
                float angleStep = 360f / segments;
                Vector3 prevPoint = transform.position + new Vector3(spawnRadius, 0, 0);

                for (int i = 1; i <= segments; i++)
                {
                    float angle = angleStep * i * Mathf.Deg2Rad;
                    Vector3 newPoint = transform.position + new Vector3(Mathf.Cos(angle) * spawnRadius, 0, Mathf.Sin(angle) * spawnRadius);
                    Gizmos.DrawLine(prevPoint, newPoint);
                    prevPoint = newPoint;
                }
            }

            // Draw label
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, 
                $"{gameObject.name}\n{teamOwner} Team\n{(isActiveSpawnPoint ? "Active" : "Inactive")}");
            #endif
        }

        private Color GetTeamColor()
        {
            return teamOwner switch
            {
                FactionType.Blue => Color.blue,
                FactionType.Red => Color.red,
                FactionType.Green => Color.green,
                _ => Color.gray
            };
        }

        /// <summary>
        /// Validate spawn point setup
        /// </summary>
        private void OnValidate()
        {
            // Ensure spawn radius is not negative
            spawnRadius = Mathf.Max(0, spawnRadius);
            
            // Ensure gizmo size is reasonable
            gizmoSize = Mathf.Clamp(gizmoSize, 0.1f, 5f);

            // Update name in editor for clarity
            #if UNITY_EDITOR
            if (Application.isPlaying == false)
            {
                string teamName = teamOwner == FactionType.None ? "Neutral" : teamOwner.ToString();
                gameObject.name = $"SpawnPoint_{teamName}_{GetInstanceID()}";
            }
            #endif
        }
    }
}
