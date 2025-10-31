using Mirror;
using UnityEngine;

namespace ElitesAndPawns.Weapons
{
    /// <summary>
    /// Projectile-based weapon implementation - spawns physical projectiles.
    /// Used for all weapons: rifles, pistols, SMGs, snipers, shotguns.
    /// Makes long-range combat engaging with bullet travel time and drop.
    /// </summary>
    public class ProjectileWeapon : BaseWeapon
    {
        [Header("Projectile Settings")]
        [SerializeField] private LayerMask hitMask = -1; // What projectiles can hit
        [SerializeField] private Transform projectileSpawnPoint; // Where projectiles spawn from
        [SerializeField] private bool showDebugTrajectory = true;

        protected override void Start()
        {
            base.Start();

            // Use fire point as spawn point if not set
            if (projectileSpawnPoint == null)
            {
                projectileSpawnPoint = firePoint;
            }

            // Validate projectile prefab
            if (weaponData.projectilePrefab == null)
            {
                Debug.LogError($"[ProjectileWeapon] {weaponData.weaponName} has no projectile prefab assigned!");
            }
        }

        /// <summary>
        /// Spawn and fire a projectile
        /// </summary>
        protected override void PerformShot(Vector3 shootOrigin, Vector3 shootDirection)
        {
            // Spawn multiple projectiles if needed (for shotguns)
            for (int i = 0; i < weaponData.projectilesPerShot; i++)
            {
                // Apply spread
                Vector3 finalDirection = ApplySpread(shootDirection);

                // Spawn projectile
                SpawnProjectile(finalDirection);

                // Debug visualization
                if (showDebugTrajectory)
                {
                    // Predict trajectory for visualization
                    DrawTrajectoryPrediction(projectileSpawnPoint.position, finalDirection, weaponData.projectileSpeed, weaponData.projectileGravity);
                }
            }
        }

        /// <summary>
        /// Spawn a single projectile
        /// </summary>
        [Server]
        private void SpawnProjectile(Vector3 direction)
        {
            if (weaponData.projectilePrefab == null)
            {
                Debug.LogError("[ProjectileWeapon] No projectile prefab!");
                return;
            }

            // Spawn position (slightly forward to avoid hitting shooter)
            Vector3 spawnPos = projectileSpawnPoint.position;

            // Instantiate projectile
            GameObject projectileObj = Instantiate(
                weaponData.projectilePrefab,
                spawnPos,
                Quaternion.LookRotation(direction)
            );

            // CRITICAL: Spawn on network FIRST before calling any RPCs
            NetworkServer.Spawn(projectileObj);

            // Initialize projectile (AFTER network spawn so RPCs work)
            Projectile projectile = projectileObj.GetComponent<Projectile>();
            if (projectile != null)
            {
                // Get shooter (owner of this weapon)
                Networking.NetworkPlayer shooter = GetComponentInParent<Networking.NetworkPlayer>();
                
                // Initialize with weapon data
                projectile.Initialize(weaponData, shooter, direction, hitMask);
            }
            else
            {
                Debug.LogError("[ProjectileWeapon] Projectile prefab missing Projectile component!");
            }

            if (debugMode)
            {
                Debug.Log($"[ProjectileWeapon] Spawned projectile: {weaponData.weaponName}");
            }
        }

        /// <summary>
        /// Draw predicted trajectory for debugging
        /// </summary>
        private void DrawTrajectoryPrediction(Vector3 start, Vector3 direction, float speed, float gravity)
        {
            Vector3 position = start;
            Vector3 velocity = direction.normalized * speed;
            float timeStep = 0.1f;
            int steps = 50;

            for (int i = 0; i < steps; i++)
            {
                Vector3 nextPosition = position + velocity * timeStep;
                
                // Apply gravity
                velocity += Vector3.down * gravity * timeStep;
                
                // Draw line segment
                Debug.DrawLine(position, nextPosition, Color.cyan, 1f);
                
                position = nextPosition;
            }
        }

        /// <summary>
        /// Override spread to use degrees instead of raw values
        /// </summary>
        protected override Vector3 ApplySpread(Vector3 direction)
        {
            float spread = GetCurrentSpread(); // Spread in degrees
            
            // Convert to radians
            float spreadRad = spread * Mathf.Deg2Rad;
            
            // Random angle and rotation around the direction
            float randomAngle = Random.Range(0f, 360f);
            float randomRadius = Random.Range(0f, spreadRad);
            
            // Create spread offset
            Vector3 up = Vector3.up;
            if (Vector3.Dot(direction, up) > 0.99f)
            {
                up = Vector3.right;
            }
            
            Vector3 right = Vector3.Cross(direction, up).normalized;
            Vector3 actualUp = Vector3.Cross(right, direction).normalized;
            
            // Apply random spread
            Vector3 spreadOffset = 
                right * Mathf.Cos(randomAngle) * Mathf.Sin(randomRadius) +
                actualUp * Mathf.Sin(randomAngle) * Mathf.Sin(randomRadius);
            
            Vector3 finalDirection = (direction + spreadOffset).normalized;
            
            return finalDirection;
        }

        private void OnDrawGizmos()
        {
            if (!debugMode || projectileSpawnPoint == null) return;

            // Draw spawn point
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(projectileSpawnPoint.position, 0.05f);
            
            // Draw forward direction
            Gizmos.color = Color.red;
            Gizmos.DrawRay(projectileSpawnPoint.position, projectileSpawnPoint.forward * 2f);
        }
    }
}
