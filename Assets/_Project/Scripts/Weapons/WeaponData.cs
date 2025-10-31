using UnityEngine;

namespace ElitesAndPawns.Weapons
{
    /// <summary>
    /// ScriptableObject that defines weapon statistics and properties.
    /// Create different WeaponData assets for each gun type.
    /// PROJECTILE-BASED VERSION - COMPLETE
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "Elites and Pawns/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        [Header("Basic Info")]
        public string weaponName = "Assault Rifle";
        public Sprite weaponIcon;

        [Header("Damage")]
        public float damage = 25f;
        public float headshotMultiplier = 2f;
        public float range = 100f; // Kept for backwards compatibility
        
        [Header("Damage Falloff")]
        public float maxDamageRange = 50f; // Full damage up to this range
        public float minDamageRange = 100f; // Damage falls off after this
        public float minDamageFalloff = 0.5f; // Minimum damage multiplier (50% at max range)

        [Header("Projectile Physics")]
        public float projectileSpeed = 100f; // meters per second
        public float projectileGravity = 9.81f; // Gravity effect on projectile
        public float projectileLifetime = 5f; // Max lifetime before despawn
        public bool hasTracer = true; // Visual trail
        public float tracerLength = 2f;

        [Header("Firing")]
        public float fireRate = 0.1f; // Time between shots (lower = faster)
        public bool isAutomatic = true;
        public int projectilesPerShot = 1; // For shotguns (renamed from bulletsPerShot)

        [Header("Ammo")]
        public int magazineSize = 30;
        public int maxReserveAmmo = 120;
        public float reloadTime = 2.0f;

        [Header("Accuracy")]
        public float baseSpread = 2f; // Degrees of spread cone
        public float aimSpread = 0.5f; // Spread when aiming
        public float moveSpreadMultiplier = 2f; // Spread increases when moving
        public float recoilAmount = 0.1f;

        [Header("Projectile Prefab")]
        public GameObject projectilePrefab; // The actual bullet/projectile

        [Header("Visual Effects")]
        public GameObject muzzleFlashPrefab;
        public GameObject impactEffectPrefab;
        public GameObject tracerPrefab; // Added for completeness

        [Header("Audio")]
        public AudioClip shootSound;
        public AudioClip reloadSound;
        public AudioClip emptySound;
        public AudioClip projectileWhizSound; // Sound when projectile passes by

        [Header("Animation")]
        public string shootAnimationTrigger = "Shoot";
        public string reloadAnimationTrigger = "Reload";
    }
}
