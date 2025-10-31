using UnityEngine;
using UnityEngine.UI;
using Mirror;
using ElitesAndPawns.Player;
using ElitesAndPawns.Weapons;
using ElitesAndPawns.Networking;

namespace ElitesAndPawns.UI
{
    /// <summary>
    /// Displays player HUD information (health, ammo, etc.)
    /// Only visible for local player
    /// </summary>
    public class PlayerHUD : MonoBehaviour
    {
        [Header("Health Display")]
        [SerializeField] private Text healthText;
        [SerializeField] private Image healthBar;
        [SerializeField] private GameObject healthPanel;

        [Header("Ammo Display")]
        [SerializeField] private Text ammoText;
        [SerializeField] private Text weaponNameText;
        [SerializeField] private GameObject ammoPanel;

        [Header("References")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private WeaponManager weaponManager;

        [Header("Settings")]
        [SerializeField] private Color healthColorHigh = Color.green;
        [SerializeField] private Color healthColorMid = Color.yellow;
        [SerializeField] private Color healthColorLow = Color.red;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        private BaseWeapon currentWeapon;

        private void Start()
        {
            // Find references if not assigned
            if (playerHealth == null)
            {
                playerHealth = GetComponentInParent<PlayerHealth>();
            }

            if (weaponManager == null)
            {
                weaponManager = GetComponentInParent<WeaponManager>();
            }

            // Subscribe to events
            if (playerHealth != null)
            {
                playerHealth.OnHealthChangedEvent += UpdateHealthDisplay;
                playerHealth.OnDeath += OnPlayerDeath;
                playerHealth.OnRespawn += OnPlayerRespawn;
            }

            if (weaponManager != null)
            {
                weaponManager.OnWeaponSwitched += OnWeaponSwitched;
            }

            // Initial update
            UpdateHealthDisplay(playerHealth != null ? playerHealth.CurrentHealth : 0, 
                                playerHealth != null ? playerHealth.MaxHealth : 100);

            if (weaponManager != null && weaponManager.CurrentWeapon != null)
            {
                OnWeaponSwitched(weaponManager.CurrentWeapon);
            }

            if (debugMode)
            {
                Debug.Log("[PlayerHUD] Initialized");
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (playerHealth != null)
            {
                playerHealth.OnHealthChangedEvent -= UpdateHealthDisplay;
                playerHealth.OnDeath -= OnPlayerDeath;
                playerHealth.OnRespawn -= OnPlayerRespawn;
            }

            if (weaponManager != null)
            {
                weaponManager.OnWeaponSwitched -= OnWeaponSwitched;
            }

            if (currentWeapon != null)
            {
                currentWeapon.OnAmmoChanged -= UpdateAmmoDisplay;
            }
        }

        /// <summary>
        /// Update health display
        /// </summary>
        private void UpdateHealthDisplay(float currentHealth, float maxHealth)
        {
            if (healthText != null)
            {
                healthText.text = $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
            }

            if (healthBar != null)
            {
                float healthPercent = currentHealth / maxHealth;
                healthBar.fillAmount = healthPercent;

                // Color coding
                if (healthPercent > 0.6f)
                {
                    healthBar.color = healthColorHigh;
                }
                else if (healthPercent > 0.3f)
                {
                    healthBar.color = healthColorMid;
                }
                else
                {
                    healthBar.color = healthColorLow;
                }
            }
        }

        /// <summary>
        /// Update ammo display
        /// </summary>
        private void UpdateAmmoDisplay(int currentAmmo, int reserveAmmo)
        {
            if (ammoText != null)
            {
                ammoText.text = $"{currentAmmo} / {reserveAmmo}";
            }
        }

        /// <summary>
        /// Called when weapon is switched
        /// </summary>
        private void OnWeaponSwitched(BaseWeapon newWeapon)
        {
            // Unsubscribe from old weapon
            if (currentWeapon != null)
            {
                currentWeapon.OnAmmoChanged -= UpdateAmmoDisplay;
            }

            // Subscribe to new weapon
            currentWeapon = newWeapon;
            if (currentWeapon != null)
            {
                currentWeapon.OnAmmoChanged += UpdateAmmoDisplay;

                // Update weapon name
                if (weaponNameText != null)
                {
                    weaponNameText.text = currentWeapon.Data.weaponName;
                }

                // Initial ammo update
                UpdateAmmoDisplay(currentWeapon.CurrentAmmo, currentWeapon.ReserveAmmo);
            }
        }

        /// <summary>
        /// Called when player dies (killer parameter matches the event signature)
        /// </summary>
        private void OnPlayerDeath(NetworkPlayer killer)
        {
            // Hide HUD on death (optional)
            if (healthPanel != null)
            {
                // Keep visible to show death state
            }

            if (ammoPanel != null)
            {
                // Hide weapon info when dead
                ammoPanel.SetActive(false);
            }

            if (debugMode)
            {
                string killerName = killer != null ? killer.PlayerName : "Unknown";
                Debug.Log($"[PlayerHUD] Player died. Killed by: {killerName}");
            }
        }

        /// <summary>
        /// Called when player respawns
        /// </summary>
        private void OnPlayerRespawn()
        {
            // Show HUD on respawn
            if (ammoPanel != null)
            {
                ammoPanel.SetActive(true);
            }

            // Refresh displays
            if (playerHealth != null)
            {
                UpdateHealthDisplay(playerHealth.CurrentHealth, playerHealth.MaxHealth);
            }

            if (currentWeapon != null)
            {
                UpdateAmmoDisplay(currentWeapon.CurrentAmmo, currentWeapon.ReserveAmmo);
            }

            if (debugMode)
            {
                Debug.Log("[PlayerHUD] Player respawned");
            }
        }

        /// <summary>
        /// Show/hide HUD
        /// </summary>
        public void SetHUDVisible(bool visible)
        {
            if (healthPanel != null)
            {
                healthPanel.SetActive(visible);
            }

            if (ammoPanel != null)
            {
                ammoPanel.SetActive(visible);
            }
        }
    }
}
