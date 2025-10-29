using Mirror;
using UnityEngine;
using ElitesAndPawns.Networking; // Added this line

namespace ElitesAndPawns.Player
{
    /// <summary>
    /// Handles player health, damage, and death.
    /// Synchronized across the network.
    /// </summary>
    public class PlayerHealth : NetworkBehaviour
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;

        [SyncVar(hook = nameof(OnHealthChanged))]
        private float currentHealth;

        [Header("Respawn")]
        [SerializeField] private float respawnDelay = 3f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Events
        public event System.Action<float, float> OnHealthChangedEvent; // current, max
        public event System.Action OnDeath;
        public event System.Action OnRespawn;

        // State
        private bool isDead = false;

        private void Start()
        {
            // Initialize health
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

            currentHealth -= damage;
            currentHealth = Mathf.Max(0, currentHealth);

            if (debugMode)
            {
                string attackerName = attacker != null ? attacker.PlayerName : "Unknown";
                Debug.Log($"[PlayerHealth] Took {damage} damage from {attackerName}. Health: {currentHealth}/{maxHealth}");
            }

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

            currentHealth += amount;
            currentHealth = Mathf.Min(maxHealth, currentHealth);

            if (debugMode)
            {
                Debug.Log($"[PlayerHealth] Healed {amount}. Health: {currentHealth}/{maxHealth}");
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
                Debug.Log($"[PlayerHealth] Player died. Killed by: {killerName}");
            }

            // Notify clients of death
            RpcOnDeath();

            // TODO: Award kill to attacker
            // TODO: Update scoreboard

            // Schedule respawn
            Invoke(nameof(Respawn), respawnDelay);
        }

        /// <summary>
        /// Respawn the player (called on server)
        /// </summary>
        [Server]
        private void Respawn()
        {
            // TODO: Check if battle has tokens remaining
            // TODO: Find spawn point
            // TODO: Teleport player to spawn

            // Reset health
            currentHealth = maxHealth;
            isDead = false;

            if (debugMode)
            {
                Debug.Log("[PlayerHealth] Player respawned");
            }

            // Notify clients
            RpcOnRespawn();
        }

        /// <summary>
        /// Called on all clients when health changes
        /// </summary>
        private void OnHealthChanged(float oldHealth, float newHealth)
        {
            if (debugMode)
            {
                Debug.Log($"[PlayerHealth] Health changed: {oldHealth} → {newHealth}");
            }

            // Invoke event for UI updates
            OnHealthChangedEvent?.Invoke(newHealth, maxHealth);
        }

        /// <summary>
        /// RPC to notify all clients of death
        /// </summary>
        [ClientRpc]
        private void RpcOnDeath()
        {
            OnDeath?.Invoke();

            if (debugMode)
            {
                Debug.Log("[PlayerHealth] Death RPC received");
            }

            // Play death effects (animation, sound, etc.)
            PlayDeathEffects();
        }

        /// <summary>
        /// RPC to notify all clients of respawn
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
        }

        /// <summary>
        /// Play respawn visual/audio effects
        /// </summary>
        private void PlayRespawnEffects()
        {
            // TODO: Play respawn animation
            // TODO: Play respawn sound

            // Re-enable renderer
            Renderer renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }

        /// <summary>
        /// Get current health percentage (0-1)
        /// </summary>
        public float GetHealthPercentage()
        {
            return currentHealth / maxHealth;
        }

        /// <summary>
        /// Check if player is dead
        /// </summary>
        public bool IsDead()
        {
            return isDead;
        }

        /// <summary>
        /// Get current health
        /// </summary>
        public float GetCurrentHealth()
        {
            return currentHealth;
        }

        /// <summary>
        /// Get max health
        /// </summary>
        public float GetMaxHealth()
        {
            return maxHealth;
        }
    }
}
