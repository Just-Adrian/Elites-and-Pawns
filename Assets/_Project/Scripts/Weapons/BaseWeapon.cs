using Mirror;
using UnityEngine;

namespace ElitesAndPawns.Weapons
{
    /// <summary>
    /// Abstract base class for all weapons in the game.
    /// Handles common functionality like ammo, reloading, and firing logic.
    /// </summary>
    public abstract class BaseWeapon : NetworkBehaviour
    {
        [Header("Weapon Data")]
        [SerializeField] protected WeaponData weaponData;

        [Header("Ammo State")]
        [SyncVar] protected int currentAmmo;
        [SyncVar] protected int reserveAmmo;
        [SyncVar] protected bool isReloading = false;

        [Header("References")]
        [SerializeField] protected Transform firePoint; // Where bullets spawn from
        [SerializeField] protected Camera playerCamera; // For aiming

        [Header("Debug")]
        [SerializeField] protected bool debugMode = true;

        // State
        protected float nextFireTime = 0f;
        protected bool isFiring = false;
        protected bool isAiming = false;

        // Events
        public event System.Action<int, int> OnAmmoChanged; // current, reserve
        public event System.Action OnWeaponFired;
        public event System.Action OnReloadStarted;
        public event System.Action OnReloadFinished;

        // Properties
        public WeaponData Data => weaponData;
        public int CurrentAmmo => currentAmmo;
        public int ReserveAmmo => reserveAmmo;
        public bool IsReloading => isReloading;
        public bool CanFire => !isReloading && currentAmmo > 0 && Time.time >= nextFireTime;

        protected virtual void Start()
        {
            // Initialize ammo
            if (isServer)
            {
                currentAmmo = weaponData.magazineSize;
                reserveAmmo = weaponData.maxReserveAmmo;
            }

            // Find fire point if not assigned
            if (firePoint == null)
            {
                firePoint = transform;
            }

            // NOTE: Camera is now set by WeaponManager - no need to auto-find
            // This prevents finding the wrong camera (Scene view, etc.)
        }

        /// <summary>
        /// Set the player camera reference (called by WeaponManager)
        /// CRITICAL: This must be called after weapon is equipped!
        /// </summary>
        public void SetPlayerCamera(Camera camera)
        {
            playerCamera = camera;
            
            if (debugMode)
            {
                Debug.Log($"[BaseWeapon] Camera set to: {camera.name} at position {camera.transform.position}");
            }
        }

        /// <summary>
        /// Attempt to fire the weapon
        /// </summary>
        public virtual void TryFire()
        {
            if (!isLocalPlayer) return;

            if (CanFire)
            {
                // Make sure we have a camera
                if (playerCamera == null)
                {
                    Debug.LogError("[BaseWeapon] Cannot fire - no camera assigned!");
                    return;
                }

                Vector3 shootOrigin = playerCamera.transform.position;
                Vector3 shootDirection = playerCamera.transform.forward;

                if (debugMode)
                {
                    Debug.Log($"[BaseWeapon] Firing from {shootOrigin} in direction {shootDirection}");
                }

                CmdFire(shootOrigin, shootDirection);
            }
            else if (currentAmmo <= 0 && !isReloading)
            {
                // Play empty click sound
                PlayEmptySound();
            }
        }

        /// <summary>
        /// Command: Fire weapon (called from client, executed on server)
        /// </summary>
        [Command]
        protected virtual void CmdFire(Vector3 shootOrigin, Vector3 shootDirection)
        {
            if (!CanFire) return;

            // Consume ammo
            currentAmmo--;

            // Set next fire time
            nextFireTime = Time.time + weaponData.fireRate;

            if (debugMode)
            {
                Debug.Log($"[BaseWeapon] CmdFire - Direction: {shootDirection}, Origin: {shootOrigin}");
            }

            // Perform the actual shooting logic (implemented by subclasses)
            PerformShot(shootOrigin, shootDirection);

            // Notify all clients with current ammo values
            RpcOnWeaponFired(currentAmmo, reserveAmmo);

            // Notify ammo changed
            if (debugMode)
            {
                Debug.Log($"[BaseWeapon] Fired! Ammo: {currentAmmo}/{reserveAmmo}");
            }
        }

        /// <summary>
        /// Abstract method: Subclasses implement their specific shooting logic
        /// </summary>
        protected abstract void PerformShot(Vector3 shootOrigin, Vector3 shootDirection);

        /// <summary>
        /// Start reloading the weapon
        /// </summary>
        public virtual void TryReload()
        {
            if (!isLocalPlayer) return;

            if (!isReloading && currentAmmo < weaponData.magazineSize && reserveAmmo > 0)
            {
                CmdReload();
            }
        }

        /// <summary>
        /// Command: Reload weapon
        /// </summary>
        [Command]
        protected virtual void CmdReload()
        {
            if (isReloading || reserveAmmo <= 0 || currentAmmo >= weaponData.magazineSize)
                return;

            isReloading = true;
            RpcStartReload();

            // Schedule reload completion
            Invoke(nameof(CompleteReload), weaponData.reloadTime);

            if (debugMode)
            {
                Debug.Log($"[BaseWeapon] Reloading... ({weaponData.reloadTime}s)");
            }
        }

        /// <summary>
        /// Complete the reload (called after reload time)
        /// </summary>
        [Server]
        protected virtual void CompleteReload()
        {
            int ammoNeeded = weaponData.magazineSize - currentAmmo;
            int ammoToReload = Mathf.Min(ammoNeeded, reserveAmmo);

            currentAmmo += ammoToReload;
            reserveAmmo -= ammoToReload;

            isReloading = false;
            RpcFinishReload(currentAmmo, reserveAmmo);

            if (debugMode)
            {
                Debug.Log($"[BaseWeapon] Reload complete! Ammo: {currentAmmo}/{reserveAmmo}");
            }
        }

        /// <summary>
        /// Set aiming state
        /// </summary>
        public virtual void SetAiming(bool aiming)
        {
            isAiming = aiming;
        }

        /// <summary>
        /// Calculate current spread based on movement and aiming
        /// </summary>
        protected virtual float GetCurrentSpread()
        {
            float spread = isAiming ? weaponData.aimSpread : weaponData.baseSpread;

            // Increase spread if moving (will implement later with player movement integration)
            // For now, just return base spread
            return spread;
        }

        /// <summary>
        /// Apply spread to shooting direction (VIRTUAL so subclasses can override)
        /// </summary>
        protected virtual Vector3 ApplySpread(Vector3 direction)
        {
            float spread = GetCurrentSpread();
            
            // Add random spread
            Vector3 spreadVector = new Vector3(
                Random.Range(-spread, spread),
                Random.Range(-spread, spread),
                0
            );

            return (direction + spreadVector).normalized;
        }

        #region RPC Methods (Visual/Audio Feedback)

        /// <summary>
        /// RPC: Notify all clients that weapon was fired
        /// </summary>
        [ClientRpc]
        protected virtual void RpcOnWeaponFired(int ammo, int reserve)
        {
            OnWeaponFired?.Invoke();
            OnAmmoChanged?.Invoke(ammo, reserve);

            // Play shoot sound
            if (weaponData.shootSound != null)
            {
                AudioSource.PlayClipAtPoint(weaponData.shootSound, firePoint.position);
            }

            // Spawn muzzle flash (if exists)
            if (weaponData.muzzleFlashPrefab != null)
            {
                Instantiate(weaponData.muzzleFlashPrefab, firePoint.position, firePoint.rotation);
            }
        }

        /// <summary>
        /// RPC: Start reload animation/sound
        /// </summary>
        [ClientRpc]
        protected virtual void RpcStartReload()
        {
            OnReloadStarted?.Invoke();

            // Play reload sound
            if (weaponData.reloadSound != null)
            {
                AudioSource.PlayClipAtPoint(weaponData.reloadSound, transform.position);
            }
        }

        /// <summary>
        /// RPC: Finish reload
        /// </summary>
        [ClientRpc]
        protected virtual void RpcFinishReload(int ammo, int reserve)
        {
            OnReloadFinished?.Invoke();
            OnAmmoChanged?.Invoke(ammo, reserve);
        }

        /// <summary>
        /// Play empty weapon sound
        /// </summary>
        protected virtual void PlayEmptySound()
        {
            if (weaponData.emptySound != null)
            {
                AudioSource.PlayClipAtPoint(weaponData.emptySound, transform.position);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Add ammo to reserve
        /// </summary>
        [Server]
        public void AddAmmo(int amount)
        {
            reserveAmmo = Mathf.Min(reserveAmmo + amount, weaponData.maxReserveAmmo);
            OnAmmoChanged?.Invoke(currentAmmo, reserveAmmo);
        }

        /// <summary>
        /// Refill ammo completely
        /// </summary>
        [Server]
        public void RefillAmmo()
        {
            currentAmmo = weaponData.magazineSize;
            reserveAmmo = weaponData.maxReserveAmmo;
            OnAmmoChanged?.Invoke(currentAmmo, reserveAmmo);
        }

        #endregion
    }
}
