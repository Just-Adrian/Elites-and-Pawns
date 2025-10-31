using Mirror;
using UnityEngine;

namespace ElitesAndPawns.Weapons
{
    /// <summary>
    /// Manages the player's equipped weapons.
    /// Handles weapon switching, firing, reloading, and input.
    /// </summary>
    public class WeaponManager : NetworkBehaviour
    {
        [Header("Weapons")]
        [SerializeField] private BaseWeapon[] weapons; // All available weapons
        [SyncVar(hook = nameof(OnCurrentWeaponChanged))]
        [SerializeField] private int currentWeaponIndex = 0;

        [Header("Input")]
        [SerializeField] private KeyCode reloadKey = KeyCode.R;
        [SerializeField] private KeyCode aimKey = KeyCode.Mouse1;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // State
        private BaseWeapon currentWeapon;
        private bool isFiring = false;
        private bool isAiming = false;
        private Camera playerCamera;
        private bool isInitialized = false;

        // Events
        public event System.Action<BaseWeapon> OnWeaponSwitched;

        // Properties
        public BaseWeapon CurrentWeapon => currentWeapon;
        public int WeaponCount => weapons.Length;

        private void Start()
        {
            // Wait for camera to be ready before initializing
            if (isLocalPlayer)
            {
                StartCoroutine(InitializeWhenReady());
            }
            else
            {
                // For remote players, just initialize weapons (no camera needed)
                InitializeWeapons();
            }
        }

        /// <summary>
        /// Wait for camera to be ready, then initialize
        /// </summary>
        private System.Collections.IEnumerator InitializeWhenReady()
        {
            // Wait up to 2 seconds for camera
            float waitTime = 0f;
            while (playerCamera == null && waitTime < 2f)
            {
                playerCamera = GetComponentInChildren<Camera>();
                if (playerCamera == null)
                {
                    yield return new WaitForSeconds(0.1f);
                    waitTime += 0.1f;
                }
                else
                {
                    break;
                }
            }

            if (playerCamera == null)
            {
                Debug.LogError("[WeaponManager] Could not find player camera after 2 seconds!");
            }
            else
            {
                if (debugMode)
                {
                    Debug.Log($"[WeaponManager] Found camera: {playerCamera.name}");
                }
            }

            // Initialize weapons
            InitializeWeapons();
        }

        /// <summary>
        /// Initialize weapons
        /// </summary>
        private void InitializeWeapons()
        {
            if (isInitialized)
            {
                return;
            }

            if (weapons.Length > 0)
            {
                EquipWeapon(currentWeaponIndex);
                isInitialized = true;
            }
            else
            {
                Debug.LogError("[WeaponManager] No weapons assigned!");
            }
        }

        private void Update()
        {
            if (!isLocalPlayer) return;
            if (!isInitialized) return;

            HandleWeaponInput();
            HandleWeaponSwitching();
        }

        /// <summary>
        /// Handle weapon firing and reloading input
        /// </summary>
        private void HandleWeaponInput()
        {
            if (currentWeapon == null) return;

            // Firing
            if (currentWeapon.Data.isAutomatic)
            {
                // Hold to fire (automatic)
                if (Input.GetButton("Fire1"))
                {
                    currentWeapon.TryFire();
                }
            }
            else
            {
                // Click to fire (semi-automatic)
                if (Input.GetButtonDown("Fire1"))
                {
                    currentWeapon.TryFire();
                }
            }

            // Reloading
            if (Input.GetKeyDown(reloadKey))
            {
                currentWeapon.TryReload();
            }

            // Aiming
            bool wasAiming = isAiming;
            isAiming = Input.GetKey(aimKey);
            
            if (wasAiming != isAiming)
            {
                currentWeapon.SetAiming(isAiming);
            }
        }

        /// <summary>
        /// Handle weapon switching input (1, 2, 3 keys or scroll wheel)
        /// </summary>
        private void HandleWeaponSwitching()
        {
            // Number keys (1, 2, 3, etc.)
            for (int i = 0; i < Mathf.Min(weapons.Length, 9); i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    SwitchWeapon(i);
                    return;
                }
            }

            // Mouse scroll wheel
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0f)
            {
                // Scroll up - next weapon
                SwitchWeapon((currentWeaponIndex + 1) % weapons.Length);
            }
            else if (scroll < 0f)
            {
                // Scroll down - previous weapon
                int prevIndex = currentWeaponIndex - 1;
                if (prevIndex < 0) prevIndex = weapons.Length - 1;
                SwitchWeapon(prevIndex);
            }
        }

        /// <summary>
        /// Switch to a specific weapon by index
        /// </summary>
        public void SwitchWeapon(int index)
        {
            if (index < 0 || index >= weapons.Length || index == currentWeaponIndex)
                return;

            if (currentWeapon != null && currentWeapon.IsReloading)
            {
                if (debugMode)
                {
                    Debug.Log("[WeaponManager] Cannot switch weapon while reloading");
                }
                return;
            }

            CmdSwitchWeapon(index);
        }

        /// <summary>
        /// Command: Switch weapon on server
        /// </summary>
        [Command]
        private void CmdSwitchWeapon(int index)
        {
            if (index < 0 || index >= weapons.Length)
                return;

            currentWeaponIndex = index;
        }

        /// <summary>
        /// Hook: Called when currentWeaponIndex changes
        /// </summary>
        private void OnCurrentWeaponChanged(int oldIndex, int newIndex)
        {
            EquipWeapon(newIndex);
        }

        /// <summary>
        /// Equip a weapon by index
        /// </summary>
        private void EquipWeapon(int index)
        {
            if (index < 0 || index >= weapons.Length)
                return;

            // Deactivate old weapon
            if (currentWeapon != null)
            {
                currentWeapon.gameObject.SetActive(false);
            }

            // Activate new weapon
            currentWeapon = weapons[index];
            currentWeapon.gameObject.SetActive(true);

            // CRITICAL: Set the camera reference if this is local player
            if (isLocalPlayer && playerCamera != null)
            {
                currentWeapon.SetPlayerCamera(playerCamera);
                
                if (debugMode)
                {
                    Debug.Log($"[WeaponManager] Set camera for weapon: {currentWeapon.Data.weaponName}");
                }
            }

            // Notify
            OnWeaponSwitched?.Invoke(currentWeapon);

            if (debugMode)
            {
                Debug.Log($"[WeaponManager] Equipped: {currentWeapon.Data.weaponName}");
            }
        }

        /// <summary>
        /// Get weapon by index
        /// </summary>
        public BaseWeapon GetWeapon(int index)
        {
            if (index >= 0 && index < weapons.Length)
            {
                return weapons[index];
            }
            return null;
        }

        /// <summary>
        /// Add a weapon to the inventory (future: dynamic weapon pickup)
        /// </summary>
        [Server]
        public void AddWeapon(BaseWeapon weapon)
        {
            // TODO: Implement dynamic weapon adding (for future weapon pickups)
            Debug.Log($"[WeaponManager] AddWeapon not yet implemented: {weapon.Data.weaponName}");
        }

        /// <summary>
        /// Refill all weapon ammo
        /// </summary>
        [Server]
        public void RefillAllAmmo()
        {
            foreach (BaseWeapon weapon in weapons)
            {
                weapon.RefillAmmo();
            }

            if (debugMode)
            {
                Debug.Log("[WeaponManager] All ammo refilled");
            }
        }
    }
}
