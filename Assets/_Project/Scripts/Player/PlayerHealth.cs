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
        private bool isDead = false;
        private NetworkPlayer lastAttacker = null;

        // Properties
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => isDead;
        public float HealthPercentage => currentHealth / maxHealth;

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

            // Notify all clients of death
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
            // TODO: Check if battle has tokens remaining
            // TODO: Find spawn point
            // For now, just reset health and state

            // Reset health
            currentHealth = maxHealth;
            isDead = false;
            lastAttacker = null;

            if (debugMode)
            {
                Debug.Log($"[PlayerHealth] {GetComponent<NetworkPlayer>().PlayerName} respawned");
            }

            // Notify clients
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
                Debug.Log("[PlayerHealth] Death RPC received");
            }

            // Play death effects (animation, sound, etc.)
            PlayDeathEffects();

            // Disable controls if local player
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
                Debug.Log("[PlayerHealth] Respawn RPC received");
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
            // TODO: Play death animation
            // TODO: Play death sound
            // TODO: Ragdoll or fade out

            // For now, just disable renderer temporarily
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
        }

        /// <summary>
        /// Play respawn visual/audio effects
        /// </summary>
        private void PlayRespawnEffects()
        {
            // TODO: Play respawn animation
            // TODO: Play respawn sound
            // TODO: Spawn protection effect

            // Re-enable renderer
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
        }

        /// <summary>
        /// Disable player controls (when dead)
        /// </summary>
        private void DisablePlayerControls()
        {
            PlayerController controller = GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            Weapons.WeaponManager weaponManager = GetComponent<Weapons.WeaponManager>();
            if (weaponManager != null)
            {
                weaponManager.enabled = false;
            }
        }

        /// <summary>
        /// Enable player controls (after respawn)
        /// </summary>
        private void EnablePlayerControls()
        {
            PlayerController controller = GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.enabled = true;
            }

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
