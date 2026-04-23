using UnityEngine;
using Mirror;
using ElitesAndPawns.Core;

namespace ElitesAndPawns.Networking
{
    /// <summary>
    /// Handles spawning the correct player prefab based on client type (RTS vs FPS).
    /// Attach to the same GameObject as NetworkManager in BOTH scenes.
    /// </summary>
    public class PlayerSpawnHandler : NetworkBehaviour
    {
        [Header("Player Prefabs")]
        [Tooltip("The FPS player prefab (with CharacterController, weapons, etc.)")]
        [SerializeField] private GameObject fpsPlayerPrefab;
        
        [Tooltip("The RTS/WarMap player prefab (with PlayerSquadManager)")]
        [SerializeField] private GameObject rtsPlayerPrefab;
        
        [Header("Spawn Points")]
        [SerializeField] private Transform[] fpsSpawnPoints;
        
        public static PlayerSpawnHandler Instance { get; private set; }
        
        void Awake()
        {
            Instance = this;
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Register handler for spawn requests
            NetworkServer.RegisterHandler<SpawnRequestMessage>(OnSpawnRequest);
            Debug.Log("[PlayerSpawnHandler] Server started, registered spawn request handler");
        }
        
        /// <summary>
        /// Called on server when a client requests to spawn as a specific player type.
        /// </summary>
        void OnSpawnRequest(NetworkConnectionToClient conn, SpawnRequestMessage msg)
        {
            Debug.Log($"[PlayerSpawnHandler] Spawn request from {conn.connectionId}: Type={msg.playerType}, Faction={msg.faction}, Node={msg.nodeId}");
            
            // Don't spawn if they already have a player
            if (conn.identity != null)
            {
                Debug.LogWarning($"[PlayerSpawnHandler] Connection {conn.connectionId} already has a player!");
                return;
            }
            
            GameObject prefab = null;
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;
            
            if (msg.playerType == PlayerType.FPS)
            {
                prefab = fpsPlayerPrefab;
                
                // Find spawn point for this faction
                spawnPos = GetFPSSpawnPosition(msg.faction, msg.nodeId);
                
                Debug.Log($"[PlayerSpawnHandler] Spawning FPS player at {spawnPos}");
            }
            else if (msg.playerType == PlayerType.RTS)
            {
                prefab = rtsPlayerPrefab;
                // RTS players don't need a physical position
                Debug.Log("[PlayerSpawnHandler] Spawning RTS player");
            }
            
            if (prefab == null)
            {
                Debug.LogError($"[PlayerSpawnHandler] No prefab assigned for player type: {msg.playerType}");
                return;
            }
            
            // Spawn the player
            GameObject player = Instantiate(prefab, spawnPos, spawnRot);
            
            // Set up faction if the player has a component that needs it
            var fpsPlayer = player.GetComponent<FPSPlayerSetup>();
            if (fpsPlayer != null)
            {
                fpsPlayer.SetFaction(msg.faction);
                fpsPlayer.SetNodeId(msg.nodeId);
            }
            
            // Spawn on network and assign to this connection
            NetworkServer.AddPlayerForConnection(conn, player);
            
            Debug.Log($"[PlayerSpawnHandler] Spawned {msg.playerType} player for connection {conn.connectionId}");
        }
        
        Vector3 GetFPSSpawnPosition(FactionType faction, int nodeId)
        {
            // Convert FactionType to FactionType for SpawnPoint compatibility
            FactionType factionType = faction switch
            {
                FactionType.Blue => FactionType.Blue,
                FactionType.Red => FactionType.Red,
                FactionType.Green => FactionType.Green,
                _ => FactionType.None
            };
            
            // Try to find a SpawnPoint for this faction
            var spawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
            
            foreach (var sp in spawnPoints)
            {
                if (sp.IsActive && sp.TeamOwner == factionType)
                {
                    return sp.GetSpawnPosition();
                }
            }
            
            // Fallback: use configured spawn points
            if (fpsSpawnPoints != null && fpsSpawnPoints.Length > 0)
            {
                int index = faction == FactionType.Blue ? 0 : Mathf.Min(1, fpsSpawnPoints.Length - 1);
                if (fpsSpawnPoints[index] != null)
                {
                    return fpsSpawnPoints[index].position;
                }
            }
            
            // Last resort: default positions
            return faction == FactionType.Blue ? new Vector3(-5, 1, 0) : new Vector3(5, 1, 0);
        }
        
        /// <summary>
        /// Call this from a client to request spawning as a specific player type.
        /// </summary>
        public static void RequestSpawn(PlayerType type, FactionType faction, int nodeId)
        {
            if (!NetworkClient.isConnected)
            {
                Debug.LogError("[PlayerSpawnHandler] Cannot request spawn - not connected!");
                return;
            }
            
            var msg = new SpawnRequestMessage
            {
                playerType = type,
                faction = faction,
                nodeId = nodeId
            };
            
            Debug.Log($"[PlayerSpawnHandler] Sending spawn request: Type={type}, Faction={faction}, Node={nodeId}");
            NetworkClient.Send(msg);
        }
    }
    
    /// <summary>
    /// Type of player to spawn.
    /// </summary>
    public enum PlayerType : byte
    {
        RTS,
        FPS
    }
    
    /// <summary>
    /// Message sent from client to server to request spawning.
    /// </summary>
    public struct SpawnRequestMessage : NetworkMessage
    {
        public PlayerType playerType;
        public FactionType faction;
        public int nodeId;
    }
}
