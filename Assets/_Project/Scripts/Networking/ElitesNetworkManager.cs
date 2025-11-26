using Mirror;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ElitesAndPawns.Core;

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
        [SerializeField] private bool useTeamSpawnPoints = true;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Team Manager reference
        private SimpleTeamManager teamManager;
        
        // Spawn points cache
        private List<SpawnPoint> spawnPoints = new List<SpawnPoint>();
        private List<SpawnPoint> blueSpawnPoints = new List<SpawnPoint>();
        private List<SpawnPoint> redSpawnPoints = new List<SpawnPoint>();

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

        public override void OnDestroy()
        {
            // Debug: Log when this NetworkManager is destroyed
            Debug.LogWarning($"[ElitesNetworkManager] OnDestroy called on GameObject: {gameObject.name} (InstanceID: {GetInstanceID()})");
            
            // Log stack trace to see WHO is destroying this
            Debug.LogWarning($"[ElitesNetworkManager] Destroyed from:\n{System.Environment.StackTrace}");
            
            // Call base to ensure proper cleanup
            base.OnDestroy();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Get or create TeamManager
            teamManager = SimpleTeamManager.Instance;
            if (teamManager == null)
            {
                GameObject tmGo = new GameObject("SimpleTeamManager");
                teamManager = tmGo.AddComponent<SimpleTeamManager>();
                Debug.Log("[ElitesNetworkManager] Created SimpleTeamManager instance");
            }
            
            // Find and cache spawn points
            CacheSpawnPoints();
            
            if (debugMode)
            {
                Debug.Log("[ElitesNetworkManager] Server started");
                Debug.Log($"[ElitesNetworkManager] Found {spawnPoints.Count} total spawn points " +
                         $"(Blue: {blueSpawnPoints.Count}, Red: {redSpawnPoints.Count})");
            }
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            // Get balanced team assignment
            FactionType faction = FactionType.Blue;
            if (autoAssignFaction && teamManager != null)
            {
                faction = teamManager.GetBalancedTeam();
            }
            
            // Get spawn position for the team
            Transform startPos = GetTeamSpawnPosition(faction);
            
            // Spawn player at team-specific position
            GameObject player;
            if (startPos != null)
            {
                player = Instantiate(playerPrefab, startPos.position, startPos.rotation);
            }
            else
            {
                // Fallback to default spawn if no team spawn points found
                startPos = GetStartPosition();
                player = startPos != null
                    ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
                    : Instantiate(playerPrefab);
                    
                if (debugMode)
                {
                    Debug.LogWarning($"[ElitesNetworkManager] No spawn point found for {faction} team, using default");
                }
            }

            // Add player to connection
            NetworkServer.AddPlayerForConnection(conn, player);

            // Assign faction and add to team
            if (player.TryGetComponent<NetworkPlayer>(out var networkPlayer))
            {
                networkPlayer.SetFaction(faction);
                
                // Add to TeamManager tracking
                if (teamManager != null)
                {
                    teamManager.AddPlayerToTeam(networkPlayer.netId, faction);
                }

                if (debugMode)
                {
                    Debug.Log($"[ElitesNetworkManager] Player connected. Assigned to {faction} faction. " +
                              $"Team counts - Blue: {teamManager?.BluePlayerCount ?? 0}, " +
                              $"Red: {teamManager?.RedPlayerCount ?? 0}");
                }
            }
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // Remove from TeamManager tracking
            if (conn.identity != null && conn.identity.TryGetComponent<NetworkPlayer>(out var networkPlayer))
            {
                FactionType faction = networkPlayer.Faction;
                
                if (teamManager != null)
                {
                    teamManager.RemovePlayerFromTeams(networkPlayer.netId);
                }

                if (debugMode)
                {
                    Debug.Log($"[ElitesNetworkManager] Player from {faction} faction disconnected. " +
                              $"Remaining - Blue: {teamManager?.BluePlayerCount ?? 0}, " +
                              $"Red: {teamManager?.RedPlayerCount ?? 0}");
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
        /// Cache all spawn points in the scene
        /// </summary>
        private void CacheSpawnPoints()
        {
            spawnPoints.Clear();
            blueSpawnPoints.Clear();
            redSpawnPoints.Clear();
            
            SpawnPoint[] allSpawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
            
            foreach (var sp in allSpawnPoints)
            {
                if (!sp.IsActive) continue;
                
                spawnPoints.Add(sp);
                
                if (sp.TeamOwner == FactionType.Blue)
                {
                    blueSpawnPoints.Add(sp);
                }
                else if (sp.TeamOwner == FactionType.Red)
                {
                    redSpawnPoints.Add(sp);
                }
            }
        }
        
        /// <summary>
        /// Get a spawn position for a specific team
        /// </summary>
        private Transform GetTeamSpawnPosition(FactionType team)
        {
            if (!useTeamSpawnPoints)
            {
                return GetStartPosition();
            }
            
            List<SpawnPoint> teamSpawns = team switch
            {
                FactionType.Blue => blueSpawnPoints,
                FactionType.Red => redSpawnPoints,
                _ => spawnPoints
            };
            
            // If no team-specific spawns, try neutral spawns
            if (teamSpawns.Count == 0)
            {
                teamSpawns = spawnPoints.Where(sp => sp.TeamOwner == FactionType.None).ToList();
            }
            
            // Pick a random spawn point from the available ones
            if (teamSpawns.Count > 0)
            {
                SpawnPoint spawnPoint = teamSpawns[Random.Range(0, teamSpawns.Count)];
                
                // Create a temporary transform at the spawn position
                GameObject temp = new GameObject($"TempSpawn_{team}");
                temp.transform.position = spawnPoint.GetSpawnPosition();
                temp.transform.rotation = spawnPoint.GetSpawnRotation();
                Transform result = temp.transform;
                
                // Clean up after a frame
                Destroy(temp, 0.1f);
                
                return result;
            }
            
            return null;
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
        public (int blue, int red) GetFactionCounts()
        {
            if (teamManager != null)
            {
                return (teamManager.BluePlayerCount, teamManager.RedPlayerCount);
            }
            return (0, 0);
        }
    }
}
