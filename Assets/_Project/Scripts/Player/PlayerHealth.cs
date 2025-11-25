using Mirror;
using UnityEngine;
using ElitesAndPawns.Networking;

namespace ElitesAndPawns.Player
{
    /// <summary>
    /// Handles player health, damage, death, and respawn.
    /// Synchronized across the network.
    /// ENHANCED VERSION - Integrates with weapon system
    /// </summary>
    public class PlayerHealth : NetworkBehaviour
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;

        [SyncVar(hook = nameof(OnHealthChanged))]
        private float currentHealth;

        [Header("Respawn")]
        [SerializeField] private float respawnDelay = 3f;
        [SerializeField] private bool autoRespawn = true;
        
        [Header("Team Settings")]
        [SerializeField] private bool friendlyFireEnabled = false;

        [Header("Damage Feedback")]
        [SerializeField] private float damageFeedbackDuration = 0.2f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Events
        public event System.Action<float, float> OnHealthChangedEvent; // current, max
        public event System.Action<float, NetworkPlayer> OnDamageTaken; // damage, attacker
        public event System.Action<NetworkPlayer> OnDeath; // killer
        public event System.Action OnRespawn;

        // State
        [SyncVar(hook = nameof(OnIsDeadChanged))]
        private bool isDead = false;
        private NetworkPlayer lastAttacker = null;

        // Components cache
        private CharacterController characterController;
        private Collider[] colliders;

        // Properties
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => isDead;
        public float HealthPercentage => currentHealth / maxHealth;

        private void Awake()
        {
            // Cache components
            characterController = GetComponent<CharacterController>();
            colliders = GetComponentsInChildren<Collider>();
        }

        private void Start()
        {
            // Initialize health on server
            if (isServer)
            {
                currentHealth = maxHealth;
            }
        }

        /// <summary>
        /// Take damage (called on server)
        /// </summary>
        [Server]
        public void TakeDamage(float damage, NetworkPlayer attacker = null)
        {
            if (isDead) return;
            
            // Check for friendly fire
            if (!friendlyFireEnabled && attacker != null)
            {
                NetworkPlayer myPlayer = GetComponent<NetworkPlayer>();
                if (myPlayer != null && myPlayer.Faction == attacker.Faction && myPlayer.Faction != Core.FactionType.None)
                {
                    if (debugMode)
                    {
                        Debug.Log($"[PlayerHealth] Friendly fire blocked! {attacker.PlayerName} ({attacker.Faction}) tried to damage teammate {myPlayer.PlayerName}");
                    }
                    return; // No damage from teammates
                }
            }

            // Store attacker
            lastAttacker = attacker;

            // Apply damage
            float oldHealth = currentHealth;
            currentHealth -= damage;
            currentHealth = Mathf.Max(0, currentHealth);

            if (debugMode)
            {
                string attackerName = attacker != null ? attacker.PlayerName : "Unknown";
                Debug.Log($"[PlayerHealth] {GetComponent<NetworkPlayer>().PlayerName} took {damage:F1} damage from {attackerName}. Health: {currentHealth:F1}/{maxHealth}");
            }

            // Notify clients of damage
            RpcOnDamageTaken(damage, attacker);

            // Check if dead
            if (currentHealth <= 0)
            {
                Die(attacker);
            }
        }

        /// <summary>
        /// Heal player (called on server)
        /// </summary>
        [Server]
        public void Heal(float amount)
        {
            if (isDead) return;

            float oldHealth = currentHealth;
            currentHealth += amount;
            currentHealth = Mathf.Min(maxHealth, currentHealth);

            if (debugMode && oldHealth != currentHealth)
            {
                Debug.Log($"[PlayerHealth] Healed {amount:F1}. Health: {currentHealth:F1}/{maxHealth}");
            }
        }

        /// <summary>
        /// Set health directly (for testing/admin commands)
        /// </summary>
        [Server]
        public void SetHealth(float health)
        {
            currentHealth = Mathf.Clamp(health, 0, maxHealth);
            
            if (currentHealth <= 0 && !isDead)
            {
                Die(null);
            }
        }

        /// <summary>
        /// Kill the player (called on server)
        /// </summary>
        [Server]
        private void Die(NetworkPlayer killer = null)
        {
            if (isDead) return;

            isDead = true;

            if (debugMode)
            {
                string killerName = killer != null ? killer.PlayerName : "Unknown";
                Debug.Log($"[PlayerHealth] {GetComponent<NetworkPlayer>().PlayerName} died. Killed by: {killerName}");
            }

            // CRITICAL: Disable colliders on SERVER so dead players can't capture points
            DisableColliders();

            if (debugMode)
            {
                Debug.Log($"[PlayerHealth] Server disabled colliders for dead player");
            }

            // Notify all clients of death (they'll also disable colliders for visuals)
            RpcOnDeath(killer);

            // Award kill to attacker (future: update scoreboard, stats)
            if (killer != null)
            {
                // TODO: Increment killer's score
            }

            // Schedule respawn if auto-respawn is enabled
            if (autoRespawn)
            {
                Invoke(nameof(Respawn), respawnDelay);
            }
        }

        /// <summary>
        /// Respawn the player (called on server)
        /// </summary>
        [Server]
        public void Respawn()
        {
            // Reset health and state
            currentHealth = maxHealth;
            isDead = false;
            lastAttacker = null;

            // Find and teleport to spawn point
            NetworkPlayer networkPlayer = GetComponent<NetworkPlayer>();
            if (networkPlayer != null)
            {
                Core.FactionType faction = networkPlayer.Faction;
                Core.SpawnPoint spawnPoint = Core.SpawnPoint.GetRandomSpawnPoint(faction);

                if (spawnPoint != null)
                {
                    // Disable CharacterController to teleport
                    if (characterController != null)
                    {
                        characterController.enabled = false;
                    }

                    // Teleport to spawn point
                    Vector3 spawnPos = spawnPoint.transform.position;
                    Quaternion spawnRot = spawnPoint.transform.rotation;
                    transform.position = spawnPos;
                    transform.rotation = spawnRot;

                    if (debugMode)
                    {
                        Debug.Log($"[PlayerHealth] {networkPlayer.PlayerName} teleported to {spawnPoint.name} ({faction} spawn) at position {spawnPos}");
                    }
                    
                    // Explicitly sync position to all clients (in case NetworkTransform missed it)
                    RpcSyncPosition(spawnPos, spawnRot);
                }
                else
                {
                    if (debugMode)
                    {
                        Debug.LogWarning($"[PlayerHealth] No spawn point found for {faction} faction! Player respawned at death location.");
                    }
                }
            }

            // CRITICAL: Re-enable colliders on SERVER
            EnableColliders();

            if (debugMode)
            {
                Debug.Log($"[PlayerHealth] Server re-enabled colliders for respawned player");
            }

            // Notify clients to respawn (they'll re-enable colliders and visuals)
            RpcOnRespawn();
        }

        /// <summary>
        /// Called when health changes (on all clients)
        /// </summary>
        private void OnHealthChanged(float oldHealth, float newHealth)
        {
            if (debugMode)
            {
                Debug.Log($"[PlayerHealth] Health changed: {oldHealth:F1} → {newHealth:F1}");
            }

            // Invoke event for UI updates
            OnHealthChangedEvent?.Invoke(newHealth, maxHealth);
        }

        /// <summary>
        /// Called when isDead changes (on all clients)
        /// This ensures clients know when players die for proper ControlPoint cleanup
        /// </summary>
        private void OnIsDeadChanged(bool oldValue, bool newValue)
        {
            if (debugMode)
            {
                Debug.Log($"[PlayerHealth] isDead changed: {oldValue} → {newValue} (Client-side sync)");
            }
            
            // Note: We don't need to do anything here since RpcOnDeath handles visuals
            // This hook just ensures the IsDead property is synced for ControlPoint.CleanupPlayerLists()
        }

        /// <summary>
        /// RPC: Notify clients of damage taken
        /// </summary>
        [ClientRpc]
        private void RpcOnDamageTaken(float damage, NetworkPlayer attacker)
        {
            OnDamageTaken?.Invoke(damage, attacker);

            // Visual feedback (damage indicator, screen flash, etc.)
            if (isLocalPlayer)
            {
                ShowDamageFeedback();
            }
        }

        /// <summary>
        /// RPC: Notify all clients of death
        /// </summary>
        [ClientRpc]
        private void RpcOnDeath(NetworkPlayer killer)
        {
            OnDeath?.Invoke(killer);

            if (debugMode)
            {
                Debug.Log("[PlayerHealth] Death RPC received on client");
            }

            // Play death effects (animation, sound, etc.)
            PlayDeathEffects();

            // Disable controls if local player (but allow camera to look around)
            if (isLocalPlayer)
            {
                DisablePlayerControls();
            }
        }

        /// <summary>
        /// RPC: Notify all clients of respawn
        /// </summary>
        [ClientRpc]
        private void RpcOnRespawn()
        {
            OnRespawn?.Invoke();

            if (debugMode)
            {
                Debug.Log("[PlayerHealth] Respawn RPC received on client");
            }

            // Play respawn effects
            PlayRespawnEffects();

            // Re-enable controls if local player
            if (isLocalPlayer)
            {
                EnablePlayerControls();
            }
        }

        /// <summary>
        /// RPC: Sync player position to all clients after teleport
        /// This ensures CharacterController doesn't interfere with NetworkTransform
        /// </summary>
        [ClientRpc]
        private void RpcSyncPosition(Vector3 position, Quaternion rotation)
        {
            // Disable CharacterController temporarily
            if (characterController != null)
            {
                characterController.enabled = false;
            }
            
            // Set position and rotation
            transform.position = position;
            transform.rotation = rotation;
            
            // Re-enable CharacterController
            if (characterController != null)
            {
                characterController.enabled = true;
            }
            
            if (debugMode)
            {
                Debug.Log($"[PlayerHealth] Client synced to position {position}");
            }
        }

        /// <summary>
        /// Show damage feedback to local player
        /// </summary>
        private void ShowDamageFeedback()
        {
            // TODO: Implement visual damage feedback
            // - Red screen flash
            // - Damage indicator showing direction of attack
            // - Camera shake
            // - Audio cue
            
            if (debugMode)
            {
                Debug.Log("[PlayerHealth] Showing damage feedback");
            }
        }

        /// <summary>
        /// Play death visual/audio effects
        /// </summary>
        private void PlayDeathEffects()
        {
            // Disable visual components
            Renderer renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }

            // Disable weapon rendering
            Weapons.WeaponManager weaponManager = GetComponent<Weapons.WeaponManager>();
            if (weaponManager != null && weaponManager.CurrentWeapon != null)
            {
                weaponManager.CurrentWeapon.gameObject.SetActive(false);
            }

            // Also disable colliders on client side for consistency
            DisableColliders();

            if (debugMode)
            {
                Debug.Log("[PlayerHealth] Client death effects applied - colliders disabled");
            }
        }

        /// <summary>
        /// Play respawn visual/audio effects
        /// </summary>
        private void PlayRespawnEffects()
        {
            // Re-enable visual components
            Renderer renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = true;
            }

            // Re-enable weapon
            Weapons.WeaponManager weaponManager = GetComponent<Weapons.WeaponManager>();
            if (weaponManager != null && weaponManager.CurrentWeapon != null)
            {
                weaponManager.CurrentWeapon.gameObject.SetActive(true);
            }

            // Re-enable colliders on client side
            EnableColliders();

            if (debugMode)
            {
                Debug.Log("[PlayerHealth] Client respawn effects applied - colliders enabled");
            }
        }

        /// <summary>
        /// Disable all colliders (when dead)
        /// </summary>
        private void DisableColliders()
        {
            // Disable CharacterController (prevents body blocking)
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            // Disable all other colliders (prevents capture point detection)
            if (colliders != null)
            {
                foreach (Collider col in colliders)
                {
                    if (col != null && col != characterController)
                    {
                        col.enabled = false;
                    }
                }
            }
        }

        /// <summary>
        /// Enable all colliders (when alive)
        /// </summary>
        private void EnableColliders()
        {
            // Re-enable CharacterController
            if (characterController != null)
            {
                characterController.enabled = true;
            }

            // Re-enable all other colliders
            if (colliders != null)
            {
                foreach (Collider col in colliders)
                {
                    if (col != null && col != characterController)
                    {
                        col.enabled = true;
                    }
                }
            }
        }

        /// <summary>
        /// Disable player controls (when dead)
        /// KEEPS CAMERA ENABLED so player can look around while dead
        /// </summary>
        private void DisablePlayerControls()
        {
            // Disable movement
            PlayerController controller = GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.CanMove = false;  // Disables movement but camera still works!
            }

            // Disable weapons
            Weapons.WeaponManager weaponManager = GetComponent<Weapons.WeaponManager>();
            if (weaponManager != null)
            {
                weaponManager.enabled = false;
            }

            // NOTE: We DON'T disable the camera - player can still look around!
        }

        /// <summary>
        /// Enable player controls (after respawn)
        /// </summary>
        private void EnablePlayerControls()
        {
            // Re-enable movement
            PlayerController controller = GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.CanMove = true;
            }

            // Re-enable weapons
            Weapons.WeaponManager weaponManager = GetComponent<Weapons.WeaponManager>();
            if (weaponManager != null)
            {
                weaponManager.enabled = true;
            }
        }

        #region Public API

        /// <summary>
        /// Get current health percentage (0-1)
        /// </summary>
        public float GetHealthPercentage()
        {
            return currentHealth / maxHealth;
        }

        /// <summary>
        /// Check if player is alive
        /// </summary>
        public bool IsAlive()
        {
            return !isDead && currentHealth > 0;
        }

        /// <summary>
        /// Get the player who last attacked this player
        /// </summary>
        public NetworkPlayer GetLastAttacker()
        {
            return lastAttacker;
        }

        #endregion
    }
}
