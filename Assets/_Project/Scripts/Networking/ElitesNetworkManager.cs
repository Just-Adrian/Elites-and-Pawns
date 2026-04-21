using System;
using Mirror;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ElitesAndPawns.Core;
using ElitesAndPawns.WarMap;

namespace ElitesAndPawns.Networking
{
    /// <summary>
    /// Custom Network Manager for Elites and Pawns True.
    /// Handles player connections, spawning, and faction assignment.
    /// 
    /// Supports dedicated server mode:
    ///   - Auto-starts as server when launched with -batchmode
    ///   - Command line args: -port [port] -maxplayers [count]
    /// </summary>
    public class ElitesNetworkManager : NetworkManager
    {
        [Header("Elites Configuration")]
        [SerializeField] private int maxPlayersPerTeam = 8; // 8v8 = 16 total
        [SerializeField] private bool autoAssignFaction = true;
        [SerializeField] private bool autoRegisterProjectiles = true;
        [SerializeField] private bool useTeamSpawnPoints = true;

        [Header("Dedicated Server")]
        [SerializeField] private bool autoStartServerInBatchMode = true;
        [SerializeField] private ushort defaultPort = 7777;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = true;
        
        /// <summary>
        /// True if running as dedicated server (no graphics).
        /// </summary>
        public static bool IsDedicatedServer { get; private set; }
        
        /// <summary>
        /// True if running in headless/batch mode.
        /// </summary>
        public static bool IsHeadless => Application.isBatchMode;

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
            
            // Parse command line arguments
            ParseCommandLineArgs();
            
            // Auto-register projectile prefabs if enabled
            if (autoRegisterProjectiles)
            {
                AutoRegisterProjectilePrefabs();
            }
            
            // Debug: Log detailed info
            Debug.Log($"[ElitesNetworkManager] Awake called on GameObject: {gameObject.name}");
            Debug.Log($"[ElitesNetworkManager] InstanceID: {GetInstanceID()}");
            Debug.Log($"[ElitesNetworkManager] Scene: {gameObject.scene.name}");
            Debug.Log($"[ElitesNetworkManager] IsHeadless: {IsHeadless}");
            Debug.Log($"[ElitesNetworkManager] Is this the singleton? {NetworkManager.singleton == this}");
            
            // Check if we're a duplicate
            if (NetworkManager.singleton != null && NetworkManager.singleton != this)
            {
                Debug.LogError($"[ElitesNetworkManager] DUPLICATE DETECTED! Singleton is: {NetworkManager.singleton.name} (ID: {NetworkManager.singleton.GetInstanceID()})");
                Debug.LogError($"[ElitesNetworkManager] This instance: {gameObject.name} (ID: {GetInstanceID()})");
            }
        }
        
        /// <summary>
        /// Start is called before the first frame update.
        /// Auto-starts server for dedicated server builds.
        /// </summary>
        new void Start()
        {
            // Auto-start as server in batch/headless mode OR if this is a Server build
            #if UNITY_SERVER
            bool isServerBuild = true;
            #else
            bool isServerBuild = false;
            #endif
            
            if ((IsHeadless || isServerBuild) && autoStartServerInBatchMode)
            {
                IsDedicatedServer = true;
                
                // Console output for headless mode
                Console.WriteLine("========================================");
                Console.WriteLine("  ELITES AND PAWNS - DEDICATED SERVER");
                Console.WriteLine("========================================");
                Console.WriteLine($"  Port: {defaultPort}");
                Console.WriteLine($"  Max Players: {maxPlayersPerTeam * 2}");
                Console.WriteLine("========================================");
                
                Debug.Log("[ElitesNetworkManager] ========================================");
                Debug.Log("[ElitesNetworkManager]   DEDICATED SERVER MODE");
                Debug.Log($"[ElitesNetworkManager]   Port: {defaultPort}");
                Debug.Log($"[ElitesNetworkManager]   Max Players: {maxPlayersPerTeam * 2}");
                Debug.Log("[ElitesNetworkManager] ========================================");
                
                // Set port on transport
                SetTransportPort(defaultPort);
                
                // Start server (not host - no local player needed)
                StartServer();
                
                Console.WriteLine("Server started. Waiting for connections...");
                Console.WriteLine("Press Ctrl+C to stop.");
                Debug.Log("[ElitesNetworkManager] Server started. Waiting for connections...");
            }
        }
        
        private void ParseCommandLineArgs()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLower();
                bool hasNext = i + 1 < args.Length;
                
                switch (arg)
                {
                    case "-port":
                        if (hasNext && ushort.TryParse(args[++i], out ushort port))
                        {
                            defaultPort = port;
                            Debug.Log($"[ElitesNetworkManager] Port set to {port}");
                        }
                        break;
                        
                    case "-maxplayers":
                        if (hasNext && int.TryParse(args[++i], out int maxPlayers))
                        {
                            maxConnections = maxPlayers;
                            Debug.Log($"[ElitesNetworkManager] Max players set to {maxPlayers}");
                        }
                        break;
                }
            }
        }
        
        private void SetTransportPort(ushort port)
        {
            var transport = Transport.active;
            if (transport == null)
            {
                Debug.LogError("[ElitesNetworkManager] No transport found!");
                return;
            }
            
            // Try field first (KCP uses lowercase 'port')
            var portField = transport.GetType().GetField("port") ??
                           transport.GetType().GetField("Port");
            if (portField != null)
            {
                portField.SetValue(transport, port);
                Debug.Log($"[ElitesNetworkManager] Transport port set to {port}");
                return;
            }
            
            // Try property
            var portProp = transport.GetType().GetProperty("Port");
            if (portProp != null && portProp.CanWrite)
            {
                portProp.SetValue(transport, port);
                Debug.Log($"[ElitesNetworkManager] Transport port set to {port}");
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
            
            Debug.Log($"[ElitesNetworkManager] ========== OnStartServer - ElitesNetworkManager is ACTIVE ==========");
            Debug.Log($"[ElitesNetworkManager] This confirms ElitesNetworkManager (not base NetworkManager) is running");
            
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
            Debug.Log($"[ElitesNetworkManager] ========== OnServerAddPlayer CALLED for connection {conn.connectionId} ==========");
            
            // Refresh spawn points cache (in case FPS scene was loaded)
            CacheSpawnPoints();
            
            // Get balanced team assignment
            FactionType faction = FactionType.Blue;
            if (autoAssignFaction && teamManager != null)
            {
                faction = teamManager.GetBalancedTeam();
            }
            
            // Check if we have a player prefab
            if (playerPrefab == null)
            {
                Debug.LogError("[ElitesNetworkManager] No playerPrefab assigned! Cannot spawn player.");
                return;
            }
            
            // Log prefab info for debugging
            bool hasPSM = playerPrefab.GetComponent<PlayerSquadManager>() != null;
            Debug.Log($"[ElitesNetworkManager] Player prefab: {playerPrefab.name}, has PlayerSquadManager: {hasPSM}");
            
            // Get spawn position for the team
            Transform startPos = GetTeamSpawnPosition(faction);
            
            // Spawn player
            GameObject player;
            if (startPos != null)
            {
                player = Instantiate(playerPrefab, startPos.position, startPos.rotation);
                if (debugMode)
                {
                    Debug.Log($"[ElitesNetworkManager] Spawning player at spawn point: {startPos.position}");
                }
            }
            else
            {
                // Fallback: spawn at a default position
                // Blue team spawns at (-10, 1, 0), Red at (10, 1, 0)
                Vector3 fallbackPos = faction == FactionType.Blue 
                    ? new Vector3(-10f, 1f, 0f) 
                    : new Vector3(10f, 1f, 0f);
                    
                player = Instantiate(playerPrefab, fallbackPos, Quaternion.identity);
                
                if (debugMode)
                {
                    Debug.LogWarning($"[ElitesNetworkManager] No spawn points found! Using fallback position: {fallbackPos}");
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
                
                // Initialize PlayerSquadManager if we're in WarMap context
                if (player.TryGetComponent<PlayerSquadManager>(out var squadManager))
                {
                    // Get starting node for this faction
                    int startingNode = GetFactionStartingNode(faction);
                    string displayName = $"Player_{conn.connectionId}";
                    
                    // Convert FactionType to Team
                    Team team = faction == FactionType.Blue ? Team.Blue : 
                                faction == FactionType.Red ? Team.Red : Team.Green;
                    
                    // FUTURE PROGRESSION: Load player profile before initialization
                    // See: Assets/_Project/Documentation/PROGRESSION_INTEGRATION.md
                    // 
                    // PlayerProfile profile = await ProfilePersistence.LoadProfile(playerId);
                    // if (profile == null) profile = PlayerProfile.CreateDefault(playerId);
                    // squadManager.Initialize(team, displayName, startingNode, profile);
                    
                    // Current: Fixed squad configuration
                    Debug.Log($"[ElitesNetworkManager] About to initialize PlayerSquadManager for connection {conn.connectionId}");
                    Debug.Log($"[ElitesNetworkManager]   Team: {team}, DisplayName: {displayName}, StartNode: {startingNode}");
                    
                    squadManager.Initialize(team, displayName, startingNode);
                    
                    Debug.Log($"[ElitesNetworkManager] Initialize complete. Faction now: {squadManager.Faction}, SquadCount: {squadManager.SquadCount}");
                    
                    // Register with NodeOccupancy (if present)
                    var nodeOccupancy = FindAnyObjectByType<NodeOccupancy>();
                    if (nodeOccupancy != null)
                    {
                        nodeOccupancy.RegisterSquadManager(squadManager);
                        Debug.Log($"[ElitesNetworkManager] Registered squad manager with NodeOccupancy");
                    }
                    
                    if (debugMode)
                    {
                        Debug.Log($"[ElitesNetworkManager] Initialized PlayerSquadManager for {displayName} at node {startingNode}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[ElitesNetworkManager] Player prefab has no PlayerSquadManager component!");
                }

                if (debugMode)
                {
                    Debug.Log($"[ElitesNetworkManager] Player spawned and connected. Assigned to {faction} faction. " +
                              $"Team counts - Blue: {teamManager?.BluePlayerCount ?? 0}, " +
                              $"Red: {teamManager?.RedPlayerCount ?? 0}");
                }
            }
            else
            {
                Debug.LogWarning("[ElitesNetworkManager] Spawned player has no NetworkPlayer component!");
            }
            
            Debug.Log($"[ElitesNetworkManager] ========== OnServerAddPlayer COMPLETE for connection {conn.connectionId} ==========");
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
                SpawnPoint spawnPoint = teamSpawns[UnityEngine.Random.Range(0, teamSpawns.Count)];
                
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
        
        /// <summary>
        /// Get the starting node ID for a faction.
        /// Used to initialize PlayerSquadManager when players join the WarMap.
        /// </summary>
        private int GetFactionStartingNode(FactionType faction)
        {
            // Try to get starting node from WarMapManager
            if (WarMapManager.Instance != null)
            {
                Team team = faction == FactionType.Blue ? Team.Blue : 
                            faction == FactionType.Red ? Team.Red : Team.Green;
                
                // Find the faction's home node
                var nodes = WarMapManager.Instance.Nodes;
                foreach (var node in nodes)
                {
                    if (node.ControllingFaction == team)
                    {
                        return node.NodeID;
                    }
                }
                
                // Fallback: return first node
                if (nodes.Count > 0)
                {
                    return nodes[0].NodeID;
                }
            }
            
            // Default: node 0 for Blue, node 4 for Red (assuming 5-node linear map)
            return faction == FactionType.Blue ? 0 : 4;
        }
    }
}
