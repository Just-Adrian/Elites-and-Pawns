using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using ElitesAndPawns.Core;

namespace ElitesAndPawns.WarMap
{
    /// <summary>
    /// Tracks which squads are present at each war map node.
    /// Handles spawn ticket selection from available squads with tracking.
    /// Server-authoritative system that aggregates data from all PlayerSquadManagers.
    /// </summary>
    public class NodeOccupancy : NetworkBehaviour
    {
        #region Singleton
        
        private static NodeOccupancy _instance;
        public static NodeOccupancy Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<NodeOccupancy>();
                }
                return _instance;
            }
        }
        
        #endregion
        
        #region Fields
        
        [Header("Configuration")]
        [SerializeField] private bool trackSpawnHistory = true;
        [SerializeField] private int maxSpawnHistoryPerNode = 100;
        
        /// <summary>
        /// Per-node occupancy data. Key = NodeID.
        /// </summary>
        private Dictionary<int, NodeOccupancyData> nodeOccupancy = new Dictionary<int, NodeOccupancyData>();
        
        /// <summary>
        /// Spawn history for debugging and post-battle analysis.
        /// Key = NodeID, Value = list of spawn records.
        /// </summary>
        private Dictionary<int, List<SpawnRecord>> spawnHistory = new Dictionary<int, List<SpawnRecord>>();
        
        /// <summary>
        /// Cache of all active PlayerSquadManagers for quick lookup.
        /// </summary>
        private List<PlayerSquadManager> activeSquadManagers = new List<PlayerSquadManager>();
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when a spawn ticket is consumed from a squad.
        /// Parameters: nodeId, squadId, spawningPlayerNetId, remainingManpower
        /// </summary>
        public static event Action<int, string, uint, int> OnSpawnTicketConsumed;
        
        /// <summary>
        /// Fired when a node runs out of friendly manpower.
        /// Parameters: nodeId, faction
        /// </summary>
        public static event Action<int, Team> OnNodeManpowerDepleted;
        
        /// <summary>
        /// Fired when squads arrive or leave a node.
        /// Parameters: nodeId, faction, totalManpower
        /// </summary>
        public static event Action<int, Team, int> OnNodeOccupancyChanged;
        
        #endregion
        
        #region Unity Lifecycle
        
        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }
        
        void Start()
        {
            if (isServer)
            {
                InitializeNodeOccupancy();
            }
        }
        
        void Update()
        {
            if (!isServer)
                return;
            
            // Periodically refresh occupancy data (could optimize with events instead)
            // For now, refresh every frame to catch squad movements
            RefreshOccupancyData();
        }
        
        #endregion
        
        #region Initialization
        
        [Server]
        private void InitializeNodeOccupancy()
        {
            // Initialize occupancy data for all nodes
            if (WarMapManager.Instance != null)
            {
                foreach (var node in WarMapManager.Instance.Nodes)
                {
                    nodeOccupancy[node.NodeID] = new NodeOccupancyData(node.NodeID);
                    spawnHistory[node.NodeID] = new List<SpawnRecord>();
                }
            }
            
            Debug.Log($"[NodeOccupancy] Initialized for {nodeOccupancy.Count} nodes");
        }
        
        /// <summary>
        /// Reinitialize occupancy tracking for all current nodes.
        /// Call this after nodes are created/registered.
        /// </summary>
        [Server]
        public void ReinitializeForNodes()
        {
            nodeOccupancy.Clear();
            spawnHistory.Clear();
            InitializeNodeOccupancy();
        }
        
        /// <summary>
        /// Ensure a specific node has occupancy tracking.
        /// </summary>
        [Server]
        public void EnsureNodeTracking(int nodeId)
        {
            if (!nodeOccupancy.ContainsKey(nodeId))
            {
                nodeOccupancy[nodeId] = new NodeOccupancyData(nodeId);
                spawnHistory[nodeId] = new List<SpawnRecord>();
                Debug.Log($"[NodeOccupancy] Added tracking for node {nodeId}");
            }
        }
        
        /// <summary>
        /// Register a PlayerSquadManager to be tracked.
        /// </summary>
        [Server]
        public void RegisterSquadManager(PlayerSquadManager manager)
        {
            if (!activeSquadManagers.Contains(manager))
            {
                activeSquadManagers.Add(manager);
                Debug.Log($"[NodeOccupancy] Registered squad manager for {manager.DisplayName}");
            }
        }
        
        /// <summary>
        /// Unregister a PlayerSquadManager (when player disconnects).
        /// </summary>
        [Server]
        public void UnregisterSquadManager(PlayerSquadManager manager)
        {
            activeSquadManagers.Remove(manager);
            Debug.Log($"[NodeOccupancy] Unregistered squad manager for {manager.DisplayName}");
        }
        
        #endregion
        
        #region Occupancy Queries
        
        /// <summary>
        /// Get the total manpower for a faction at a specific node.
        /// </summary>
        public int GetFactionManpowerAtNode(int nodeId, Team faction)
        {
            if (!nodeOccupancy.TryGetValue(nodeId, out var data))
                return 0;
            
            return data.GetFactionManpower(faction);
        }
        
        /// <summary>
        /// Get all squads present at a node (stationary, not in transit).
        /// </summary>
        public List<SquadPresence> GetSquadsAtNode(int nodeId)
        {
            if (!nodeOccupancy.TryGetValue(nodeId, out var data))
                return new List<SquadPresence>();
            
            return new List<SquadPresence>(data.PresentSquads);
        }
        
        /// <summary>
        /// Get all squads en route to a node.
        /// </summary>
        public List<SquadPresence> GetSquadsEnRouteToNode(int nodeId)
        {
            if (!nodeOccupancy.TryGetValue(nodeId, out var data))
                return new List<SquadPresence>();
            
            return new List<SquadPresence>(data.IncomingSquads);
        }
        
        /// <summary>
        /// Check if a faction has any manpower at a node.
        /// </summary>
        public bool HasFactionPresence(int nodeId, Team faction)
        {
            return GetFactionManpowerAtNode(nodeId, faction) > 0;
        }
        
        /// <summary>
        /// Check if a node has opposing factions present (contested).
        /// </summary>
        public bool IsNodeContested(int nodeId)
        {
            if (!nodeOccupancy.TryGetValue(nodeId, out var data))
                return false;
            
            bool hasBlue = data.GetFactionManpower(Team.Blue) > 0;
            bool hasRed = data.GetFactionManpower(Team.Red) > 0;
            bool hasGreen = data.GetFactionManpower(Team.Green) > 0;
            
            int factionsPresent = (hasBlue ? 1 : 0) + (hasRed ? 1 : 0) + (hasGreen ? 1 : 0);
            return factionsPresent > 1;
        }
        
        /// <summary>
        /// Get occupancy summary for a node.
        /// </summary>
        public NodeOccupancyData GetNodeOccupancy(int nodeId)
        {
            if (nodeOccupancy.TryGetValue(nodeId, out var data))
                return data;
            return null;
        }
        
        #endregion
        
        #region Spawn Ticket System
        
        /// <summary>
        /// Request a spawn ticket for a player at a node.
        /// Randomly selects from available squads of the player's faction.
        /// Tracks which squad provided the ticket for later analysis.
        /// </summary>
        /// <param name="nodeId">Node where spawn is requested</param>
        /// <param name="faction">Faction requesting spawn</param>
        /// <param name="spawningPlayerNetId">Network ID of the player spawning</param>
        /// <param name="selectedSquadId">Output: ID of the squad that provided the ticket</param>
        /// <param name="squadOwnerNetId">Output: Network ID of the player who owns the squad</param>
        /// <returns>True if spawn ticket was granted</returns>
        [Server]
        public bool RequestSpawnTicket(int nodeId, Team faction, uint spawningPlayerNetId, 
                                        out string selectedSquadId, out uint squadOwnerNetId)
        {
            selectedSquadId = "";
            squadOwnerNetId = 0;
            
            if (!nodeOccupancy.TryGetValue(nodeId, out var data))
            {
                Debug.LogWarning($"[NodeOccupancy] No occupancy data for node {nodeId}");
                return false;
            }
            
            // Get all squads of the faction with available manpower
            var availableSquads = new List<SquadPresence>();
            foreach (var presence in data.PresentSquads)
            {
                if (presence.Faction == faction && presence.Manpower > 0)
                {
                    availableSquads.Add(presence);
                }
            }
            
            if (availableSquads.Count == 0)
            {
                Debug.Log($"[NodeOccupancy] No available manpower for {faction} at node {nodeId}");
                OnNodeManpowerDepleted?.Invoke(nodeId, faction);
                return false;
            }
            
            // Random selection weighted by manpower (squads with more manpower more likely to be picked)
            // This distributes the load somewhat evenly
            int totalManpower = 0;
            foreach (var squad in availableSquads)
                totalManpower += squad.Manpower;
            
            int roll = UnityEngine.Random.Range(0, totalManpower);
            int cumulative = 0;
            SquadPresence selectedSquad = availableSquads[0];
            
            foreach (var squad in availableSquads)
            {
                cumulative += squad.Manpower;
                if (roll < cumulative)
                {
                    selectedSquad = squad;
                    break;
                }
            }
            
            // Consume manpower from the selected squad
            if (!ConsumeSquadManpower(selectedSquad.SquadId, selectedSquad.OwnerNetId))
            {
                Debug.LogError($"[NodeOccupancy] Failed to consume manpower from squad {selectedSquad.SquadId}");
                return false;
            }
            
            selectedSquadId = selectedSquad.SquadId;
            squadOwnerNetId = selectedSquad.OwnerNetId;
            
            // Record spawn for tracking
            if (trackSpawnHistory)
            {
                RecordSpawn(nodeId, selectedSquadId, squadOwnerNetId, spawningPlayerNetId);
            }
            
            // Get remaining manpower for event
            int remainingManpower = selectedSquad.Manpower - 1;
            OnSpawnTicketConsumed?.Invoke(nodeId, selectedSquadId, spawningPlayerNetId, remainingManpower);
            
            Debug.Log($"[NodeOccupancy] Spawn ticket granted at node {nodeId}: " +
                     $"Player {spawningPlayerNetId} using squad {selectedSquadId} (owner: {squadOwnerNetId})");
            
            return true;
        }
        
        /// <summary>
        /// Consume manpower from a specific squad.
        /// </summary>
        [Server]
        private bool ConsumeSquadManpower(string squadId, uint ownerNetId)
        {
            // Find the PlayerSquadManager that owns this squad
            foreach (var manager in activeSquadManagers)
            {
                if (manager.netId == ownerNetId)
                {
                    return manager.ConsumeManpower(squadId, 1);
                }
            }
            
            Debug.LogWarning($"[NodeOccupancy] Could not find manager for owner {ownerNetId}");
            return false;
        }
        
        /// <summary>
        /// Record a spawn for history tracking.
        /// </summary>
        [Server]
        private void RecordSpawn(int nodeId, string squadId, uint squadOwnerNetId, uint spawningPlayerNetId)
        {
            if (!spawnHistory.ContainsKey(nodeId))
                spawnHistory[nodeId] = new List<SpawnRecord>();
            
            var record = new SpawnRecord
            {
                Timestamp = Time.time,
                SquadId = squadId,
                SquadOwnerNetId = squadOwnerNetId,
                SpawningPlayerNetId = spawningPlayerNetId
            };
            
            spawnHistory[nodeId].Add(record);
            
            // Trim history if too long
            while (spawnHistory[nodeId].Count > maxSpawnHistoryPerNode)
            {
                spawnHistory[nodeId].RemoveAt(0);
            }
        }
        
        /// <summary>
        /// Get spawn history for a node (useful for post-battle analysis).
        /// </summary>
        public List<SpawnRecord> GetSpawnHistory(int nodeId)
        {
            if (spawnHistory.TryGetValue(nodeId, out var history))
                return new List<SpawnRecord>(history);
            return new List<SpawnRecord>();
        }
        
        /// <summary>
        /// Clear spawn history for a node (e.g., when battle ends).
        /// </summary>
        [Server]
        public void ClearSpawnHistory(int nodeId)
        {
            if (spawnHistory.ContainsKey(nodeId))
                spawnHistory[nodeId].Clear();
        }
        
        #endregion
        
        #region Occupancy Refresh
        
        /// <summary>
        /// Refresh occupancy data from all active PlayerSquadManagers.
        /// </summary>
        [Server]
        private void RefreshOccupancyData()
        {
            // Reset all node data
            foreach (var data in nodeOccupancy.Values)
            {
                data.PresentSquads.Clear();
                data.IncomingSquads.Clear();
            }
            
            // Debug: Check if we have any managers
            if (activeSquadManagers.Count == 0 && Time.frameCount % 120 == 0)
            {
                Debug.LogWarning("[NodeOccupancy] No active squad managers registered!");
            }
            
            // Aggregate from all managers
            foreach (var manager in activeSquadManagers)
            {
                if (manager == null) continue;
                
                foreach (var squad in manager.Squads)
                {
                    // Skip squads with no manpower
                    if (squad.Manpower <= 0) continue;
                    
                    var presence = new SquadPresence
                    {
                        SquadId = squad.SquadId,
                        OwnerNetId = squad.OwnerNetId,
                        OwnerDisplayName = squad.OwnerDisplayName,
                        Faction = squad.Faction,
                        Manpower = squad.Manpower
                    };
                    
                    if (squad.IsMoving)
                    {
                        // Add to incoming squads of destination
                        if (nodeOccupancy.TryGetValue(squad.DestinationNodeId, out var destData))
                        {
                            presence.ETA = squad.TimeToArrival;
                            destData.IncomingSquads.Add(presence);
                        }
                    }
                    else
                    {
                        // Add to present squads at current node
                        if (nodeOccupancy.TryGetValue(squad.CurrentNodeId, out var nodeData))
                        {
                            nodeData.PresentSquads.Add(presence);
                        }
                        else if (Time.frameCount % 120 == 0)
                        {
                            Debug.LogWarning($"[NodeOccupancy] No tracking for node {squad.CurrentNodeId}! Squad {squad.SquadId} not counted.");
                        }
                    }
                }
            }
            
            // Fire events for any significant changes (could optimize with dirty flags)
        }
        
        #endregion
        
        #region Debug
        
        /// <summary>
        /// Get a debug summary of all node occupancy.
        /// </summary>
        public string GetOccupancySummary()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== NODE OCCUPANCY ===");
            
            foreach (var kvp in nodeOccupancy)
            {
                var data = kvp.Value;
                sb.AppendLine($"Node {kvp.Key}:");
                sb.AppendLine($"  Blue: {data.GetFactionManpower(Team.Blue)} manpower");
                sb.AppendLine($"  Red: {data.GetFactionManpower(Team.Red)} manpower");
                sb.AppendLine($"  Squads: {data.PresentSquads.Count} present, {data.IncomingSquads.Count} incoming");
            }
            
            return sb.ToString();
        }
        
        #endregion
    }
    
    #region Data Classes
    
    /// <summary>
    /// Tracks occupancy data for a single node.
    /// </summary>
    [Serializable]
    public class NodeOccupancyData
    {
        public int NodeId;
        public List<SquadPresence> PresentSquads = new List<SquadPresence>();
        public List<SquadPresence> IncomingSquads = new List<SquadPresence>();
        
        public NodeOccupancyData(int nodeId)
        {
            NodeId = nodeId;
        }
        
        /// <summary>
        /// Get total manpower for a faction at this node.
        /// </summary>
        public int GetFactionManpower(Team faction)
        {
            int total = 0;
            foreach (var squad in PresentSquads)
            {
                if (squad.Faction == faction)
                    total += squad.Manpower;
            }
            return total;
        }
        
        /// <summary>
        /// Get total incoming manpower for a faction.
        /// </summary>
        public int GetFactionIncomingManpower(Team faction)
        {
            int total = 0;
            foreach (var squad in IncomingSquads)
            {
                if (squad.Faction == faction)
                    total += squad.Manpower;
            }
            return total;
        }
    }
    
    /// <summary>
    /// Represents a squad's presence at a node.
    /// </summary>
    [Serializable]
    public struct SquadPresence
    {
        public string SquadId;
        public uint OwnerNetId;
        public string OwnerDisplayName;
        public Team Faction;
        public int Manpower;
        public float ETA; // For incoming squads
    }
    
    /// <summary>
    /// Record of a spawn ticket being used.
    /// </summary>
    [Serializable]
    public struct SpawnRecord
    {
        /// <summary>Time.time when spawn occurred.</summary>
        public float Timestamp;
        
        /// <summary>Which squad provided the spawn ticket.</summary>
        public string SquadId;
        
        /// <summary>Network ID of the player who owns the squad.</summary>
        public uint SquadOwnerNetId;
        
        /// <summary>Network ID of the player who spawned.</summary>
        public uint SpawningPlayerNetId;
    }
    
    #endregion
}
