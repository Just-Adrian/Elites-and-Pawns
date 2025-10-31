using UnityEngine;

namespace ElitesAndPawns.Weapons
{
    /// <summary>
    /// OPTIONAL: Advanced projectile physics settings for WeaponData.
    /// Add this to WeaponData.cs if you want per-weapon Rigidbody control.
    /// </summary>
    [System.Serializable]
    public class ProjectilePhysicsSettings
    {
        [Header("Rigidbody Override (Optional)")]
        public bool overrideRigidbodySettings = false;
        
        [Header("Custom Rigidbody Settings")]
        public float mass = 0.01f;
        public float drag = 0f;
        public float angularDrag = 0.05f;
        
        [Header("Advanced")]
        public bool useAirResistance = false;
        public float airResistanceCoefficient = 0.01f;
        
        /// <summary>
        /// Apply these settings to a Rigidbody
        /// </summary>
        public void ApplyToRigidbody(Rigidbody rb)
        {
            if (!overrideRigidbodySettings) return;
            
            rb.mass = mass;
            rb.linearDamping = drag;
            rb.angularDamping = angularDrag;
        }
    }
}
