using Mirror;
using UnityEngine;

namespace ElitesAndPawns.Weapons
{
    /// <summary>
    /// Physics-based projectile that travels through the world.
    /// Handles movement, gravity, collision detection, and damage application.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : NetworkBehaviour
    {
        [Header("Projectile Data")]
        [SyncVar] private float damage;
        [SyncVar] private float headshotMultiplier;
        [SyncVar] private float maxDamageRange;
        [SyncVar] private float minDamageRange;
        [SyncVar] private float minDamageFalloff;

        [Header("Physics")]
        [SyncVar] private float projectileSpeed;
        [SyncVar] private float gravity;
        private Rigidbody rb;

        [Header("References")]
        private Networking.NetworkPlayer shooter;
        private Vector3 startPosition;
        private float spawnTime;
        private float lifetime;

        [Header("Visual")]
        [SerializeField] private TrailRenderer trailRenderer;
        [SerializeField] private GameObject impactEffectPrefab;

        [Header("Audio")]
        [SerializeField] private AudioClip impactSound;
        [SerializeField] private AudioClip whizSound;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        // State
        private bool hasHit = false;
        private LayerMask hitMask;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            
            // Setup rigidbody
            rb.useGravity = false; // We'll apply custom gravity
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        /// <summary>
        /// Initialize the projectile with weapon data and shooter info
        /// </summary>
        [Server]
        public void Initialize(WeaponData weaponData, Networking.NetworkPlayer shooter, Vector3 direction, LayerMask hitMask)
        {
            // Store data
            this.damage = weaponData.damage;
            this.headshotMultiplier = weaponData.headshotMultiplier;
            this.maxDamageRange = weaponData.maxDamageRange;
            this.minDamageRange = weaponData.minDamageRange;
            this.minDamageFalloff = weaponData.minDamageFalloff;
            this.projectileSpeed = weaponData.projectileSpeed;
            this.gravity = weaponData.projectileGravity;
            this.lifetime = weaponData.projectileLifetime;
            this.shooter = shooter;
            this.hitMask = hitMask;
            this.startPosition = transform.position;
            this.spawnTime = Time.time;

            // Set initial velocity
            rb.linearVelocity = direction.normalized * projectileSpeed;

            // Setup trail renderer
            RpcSetupTrail(weaponData.hasTracer, weaponData.tracerLength);

            if (debugMode)
            {
                Debug.Log($"[Projectile] Initialized. Speed: {projectileSpeed}, Gravity: {gravity}, Lifetime: {lifetime}");
            }
        }

        /// <summary>
        /// RPC: Setup visual trail on all clients
        /// </summary>
        [ClientRpc]
        private void RpcSetupTrail(bool hasTracer, float tracerLength)
        {
            if (trailRenderer != null)
            {
                trailRenderer.enabled = hasTracer;
                trailRenderer.time = tracerLength / projectileSpeed; // Adjust trail length based on speed
            }
        }

        private void FixedUpdate()
        {
            if (!isServer) return;

            // Apply custom gravity
            rb.linearVelocity += Vector3.down * gravity * Time.fixedDeltaTime;

            // Rotate to face velocity direction
            if (rb.linearVelocity.magnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
            }

            // Check lifetime
            if (Time.time - spawnTime >= lifetime)
            {
                DestroyProjectile();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isServer || hasHit) return;

            // Don't hit the shooter
            if (shooter != null && other.transform.IsChildOf(shooter.transform))
            {
                return;
            }

            // CRITICAL FIX: Ignore triggers that aren't damageable
            // This prevents bullets from being destroyed by capture points, etc.
            if (other.isTrigger)
            {
                // Only process if this trigger has a PlayerHealth component (i.e., a player hitbox)
                Player.PlayerHealth targetHealth = other.GetComponentInParent<Player.PlayerHealth>();
                if (targetHealth == null)
                {
                    // Not a damageable trigger, ignore it
                    if (debugMode)
                    {
                        Debug.Log($"[Projectile] Ignoring non-damageable trigger: {other.gameObject.name}");
                    }
                    return;
                }
            }

            // Check if we can hit this layer
            if (((1 << other.gameObject.layer) & hitMask) == 0)
            {
                return;
            }

            // Process hit
            ProcessHit(other);
        }

        /// <summary>
        /// Process collision and apply damage
        /// </summary>
        [Server]
        private void ProcessHit(Collider hitCollider)
        {
            hasHit = true;

            // Calculate distance for damage falloff
            float distance = Vector3.Distance(startPosition, transform.position);
            float damageFalloff = CalculateDamageFalloff(distance);
            float finalDamage = damage * damageFalloff;

            // Check for headshot
            bool isHeadshot = hitCollider.CompareTag("Head");
            if (isHeadshot)
            {
                finalDamage *= headshotMultiplier;
                
                if (debugMode)
                {
                    Debug.Log($"[Projectile] HEADSHOT! Damage: {finalDamage:F1} (base: {damage}, falloff: {damageFalloff:F2})");
                }
            }

            // Try to damage target
            Player.PlayerHealth targetHealth = hitCollider.GetComponentInParent<Player.PlayerHealth>();
            if (targetHealth != null)
            {
                targetHealth.TakeDamage(finalDamage, shooter);

                if (debugMode)
                {
                    string targetName = targetHealth.GetComponent<Networking.NetworkPlayer>()?.PlayerName ?? "Unknown";
                    Debug.Log($"[Projectile] Hit {targetName} for {finalDamage:F1} damage at {distance:F1}m");
                }
            }

            // Spawn impact effect at hit point
            RpcSpawnImpactEffect(transform.position, transform.forward);

            // Destroy projectile
            DestroyProjectile();
        }

        /// <summary>
        /// Calculate damage falloff based on distance
        /// </summary>
        private float CalculateDamageFalloff(float distance)
        {
            if (distance <= maxDamageRange)
            {
                // Full damage
                return 1f;
            }
            else if (distance >= minDamageRange)
            {
                // Minimum damage
                return minDamageFalloff;
            }
            else
            {
                // Linear falloff between max and min range
                float t = (distance - maxDamageRange) / (minDamageRange - maxDamageRange);
                return Mathf.Lerp(1f, minDamageFalloff, t);
            }
        }

        /// <summary>
        /// RPC: Spawn impact effect on all clients
        /// </summary>
        [ClientRpc]
        private void RpcSpawnImpactEffect(Vector3 position, Vector3 normal)
        {
            // Spawn impact effect
            if (impactEffectPrefab != null)
            {
                GameObject impact = Instantiate(
                    impactEffectPrefab,
                    position,
                    Quaternion.LookRotation(normal)
                );

                // Auto-destroy after 2 seconds
                Destroy(impact, 2f);
            }

            // Play impact sound
            if (impactSound != null)
            {
                AudioSource.PlayClipAtPoint(impactSound, position);
            }
        }

        /// <summary>
        /// Destroy the projectile
        /// </summary>
        [Server]
        private void DestroyProjectile()
        {
            NetworkServer.Destroy(gameObject);
        }

        /// <summary>
        /// Detect when projectile passes close to a player (whiz sound effect)
        /// </summary>
        private void OnTriggerStay(Collider other)
        {
            if (!isServer) return;

            // Check if it's a player (not the shooter)
            if (other.CompareTag("Player") && shooter != null && !other.transform.IsChildOf(shooter.transform))
            {
                // Play whiz sound for that player
                Networking.NetworkPlayer targetPlayer = other.GetComponent<Networking.NetworkPlayer>();
                if (targetPlayer != null)
                {
                    TargetPlayWhizSound(targetPlayer.connectionToClient);
                }
            }
        }

        /// <summary>
        /// Target RPC: Play whiz sound for specific player
        /// </summary>
        [TargetRpc]
        private void TargetPlayWhizSound(NetworkConnection target)
        {
            if (whizSound != null)
            {
                AudioSource.PlayClipAtPoint(whizSound, transform.position, 0.5f);
            }
        }

        private void OnDrawGizmos()
        {
            if (!debugMode) return;

            // Draw velocity vector
            Gizmos.color = Color.red;
            if (rb != null)
            {
                Gizmos.DrawRay(transform.position, rb.linearVelocity.normalized * 2f);
            }
        }
    }
}
