using Mirror;
using UnityEngine;

namespace ElitesAndPawns.Networking
{
    /// <summary>
    /// Custom Network Manager for Elites and Pawns True.
    /// Handles player connections, spawning, and faction assignment.
    /// </summary>
    public class ElitesNetworkManager : NetworkManager
    {
        [Header("Elites Configuration")]
        [SerializeField] private int maxPlayersPerTeam = 8; // 8v8 = 16 total
        [SerializeField] private bool autoAssignFaction = true;
        [SerializeField] private bool autoRegisterProjectiles = true;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Track connected players per faction
        private int bluePlayerCount = 0;
        private int redPlayerCount = 0;
        private int greenPlayerCount = 0;

        public override void Awake()
        {
            // CRITICAL: Call base.Awake() for Mirror's singleton pattern
            base.Awake();
            
            // Auto-register projectile prefabs if enabled
            if (autoRegisterProjectiles)
            {
                AutoRegisterProjectilePrefabs();
            }
            
            // Debug: Log detailed info
            Debug.Log($"[ElitesNetworkManager] Awake called on GameObject: {gameObject.name}");
            Debug.Log($"[ElitesNetworkManager] InstanceID: {GetInstanceID()}");
            Debug.Log($"[ElitesNetworkManager] Scene: {gameObject.scene.name}");
            Debug.Log($"[ElitesNetworkManager] Parent: {(transform.parent != null ? transform.parent.name : "null")}");
            Debug.Log($"[ElitesNetworkManager] Is this the singleton? {NetworkManager.singleton == this}");
            
            // Check if we're a duplicate
            if (NetworkManager.singleton != null && NetworkManager.singleton != this)
            {
                Debug.LogError($"[ElitesNetworkManager] DUPLICATE DETECTED! Singleton is: {NetworkManager.singleton.name} (ID: {NetworkManager.singleton.GetInstanceID()})");
                Debug.LogError($"[ElitesNetworkManager] This instance: {gameObject.name} (ID: {GetInstanceID()})");
            }
        }

        private void OnDestroy()
        {
            // Debug: Log when this NetworkManager is destroyed
            Debug.LogWarning($"[ElitesNetworkManager] OnDestroy called on GameObject: {gameObject.name} (InstanceID: {GetInstanceID()})");
            
            // Log stack trace to see WHO is destroying this
            Debug.LogWarning($"[ElitesNetworkManager] Destroyed from:\n{System.Environment.StackTrace}");
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            
            if (debugMode)
            {
                Debug.Log("[ElitesNetworkManager] Server started");
            }
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            // Spawn player at appropriate position
            Transform startPos = GetStartPosition();
            GameObject player = startPos != null
                ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
                : Instantiate(playerPrefab);

            // Add player to connection
            NetworkServer.AddPlayerForConnection(conn, player);

            // Assign faction (MVP: Blue only, but ready for expansion)
            if (autoAssignFaction && player.TryGetComponent<NetworkPlayer>(out var networkPlayer))
            {
                Core.FactionType faction = GetBalancedFaction();
                networkPlayer.SetFaction(faction);

                if (debugMode)
                {
                    Debug.Log($"[ElitesNetworkManager] Player connected. Assigned to {faction} faction. " +
                              $"Total players - Blue: {bluePlayerCount}, Red: {redPlayerCount}, Green: {greenPlayerCount}");
                }
            }
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // Track faction before disconnect
            if (conn.identity != null && conn.identity.TryGetComponent<NetworkPlayer>(out var networkPlayer))
            {
                Core.FactionType faction = networkPlayer.Faction;
                DecrementFactionCount(faction);

                if (debugMode)
                {
                    Debug.Log($"[ElitesNetworkManager] Player from {faction} faction disconnected. " +
                              $"Remaining - Blue: {bluePlayerCount}, Red: {redPlayerCount}, Green: {greenPlayerCount}");
                }
            }

            base.OnServerDisconnect(conn);
        }

        /// <summary>
        /// Automatically find and register all projectile prefabs from WeaponData assets
        /// </summary>
        private void AutoRegisterProjectilePrefabs()
        {
            // Find all WeaponData scriptable objects
            Weapons.WeaponData[] weaponDataAssets = Resources.FindObjectsOfTypeAll<Weapons.WeaponData>();
            
            if (debugMode)
            {
                Debug.Log($"[ElitesNetworkManager] Auto-registering projectile prefabs. Found {weaponDataAssets.Length} WeaponData assets.");
            }

            int registeredCount = 0;
            foreach (var weaponData in weaponDataAssets)
            {
                if (weaponData.projectilePrefab != null)
                {
                    // Check if already registered
                    if (!spawnPrefabs.Contains(weaponData.projectilePrefab))
                    {
                        spawnPrefabs.Add(weaponData.projectilePrefab);
                        registeredCount++;
                        
                        if (debugMode)
                        {
                            Debug.Log($"[ElitesNetworkManager] Registered projectile prefab: {weaponData.projectilePrefab.name} from weapon: {weaponData.weaponName}");
                        }
                    }
                }
            }
            
            if (debugMode)
            {
                Debug.Log($"[ElitesNetworkManager] Registered {registeredCount} projectile prefab(s). Total spawnable prefabs: {spawnPrefabs.Count}");
            }
        }

        /// <summary>
        /// Get the least populated faction for team balance
        /// MVP: Returns Blue only
        /// </summary>
        private Core.FactionType GetBalancedFaction()
        {
            // MVP: Only Blue faction available
            bluePlayerCount++;
            return Core.FactionType.Blue;

            // Post-MVP: Balance across all factions
            /*
            if (bluePlayerCount <= redPlayerCount && bluePlayerCount <= greenPlayerCount)
            {
                bluePlayerCount++;
                return Core.FactionType.Blue;
            }
            else if (redPlayerCount <= greenPlayerCount)
            {
                redPlayerCount++;
                return Core.FactionType.Red;
            }
            else
            {
                greenPlayerCount++;
                return Core.FactionType.Green;
            }
            */
        }

        /// <summary>
        /// Decrement faction player count when player leaves
        /// </summary>
        private void DecrementFactionCount(Core.FactionType faction)
        {
            switch (faction)
            {
                case Core.FactionType.Blue:
                    bluePlayerCount = Mathf.Max(0, bluePlayerCount - 1);
                    break;
                case Core.FactionType.Red:
                    redPlayerCount = Mathf.Max(0, redPlayerCount - 1);
                    break;
                case Core.FactionType.Green:
                    greenPlayerCount = Mathf.Max(0, greenPlayerCount - 1);
                    break;
            }
        }

        /// <summary>
        /// Check if server is full
        /// </summary>
        public bool IsServerFull()
        {
            return numPlayers >= maxPlayersPerTeam * 2; // 8v8 = 16 total
        }

        /// <summary>
        /// Get current player counts per faction
        /// </summary>
        public (int blue, int red, int green) GetFactionCounts()
        {
            return (bluePlayerCount, redPlayerCount, greenPlayerCount);
        }
    }
}
