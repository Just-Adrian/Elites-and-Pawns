using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using ElitesAndPawns.Core;

namespace ElitesAndPawns.WarMap
{
    /// <summary>
    /// Manages the squads owned by a single player.
    /// Each player can control up to MaxSquadsPerPlayer squads independently.
    /// Attached to the player's NetworkPlayer object.
    /// </summary>
    public class PlayerSquadManager : NetworkBehaviour
    {
        #region Constants
        
        /// <summary>
        /// Maximum number of squads a player can control.
        /// </summary>
        public const int MaxSquadsPerPlayer = 3;
        
        /// <summary>
        /// Default manpower capacity per squad.
        /// </summary>
        public const int DefaultMaxManpower = 8;
        
        #endregion
        
        #region Fields
        
        [Header("Configuration")]
        [SerializeField] private int maxSquads = MaxSquadsPerPlayer;
        [SerializeField] private int defaultManpowerPerSquad = DefaultMaxManpower;
        
        [Header("Player Info")]
        [SyncVar]
        private Team playerFaction = Team.None;
        
        [SyncVar]
        private string playerDisplayName = "";
        
        /// <summary>
        /// Synchronized list of squad data for this player.
        /// Using struct for network efficiency.
        /// </summary>
        private readonly SyncList<SquadSyncData> syncedSquads = new SyncList<SquadSyncData>();
        
        /// <summary>
        /// Local cache of Squad objects built from sync data.
        /// </summary>
        private List<Squad> localSquads = new List<Squad>();
        
        /// <summary>
        /// Flag to track if local cache needs rebuild.
        /// </summary>
        private bool squadsCacheDirty = true;
        
        /// <summary>
        /// Flag to track if this manager has been properly initialized.
        /// </summary>
        private bool isManagerInitialized = false;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// The faction this player belongs to.
        /// </summary>
        public Team Faction => playerFaction;
        
        /// <summary>
        /// Display name of this player.
        /// </summary>
        public string DisplayName => playerDisplayName;
        
        /// <summary>
        /// Number of squads this player currently has.
        /// </summary>
        public int SquadCount => syncedSquads?.Count ?? 0;
        
        /// <summary>
        /// Get all squads owned by this player (read-only).
        /// </summary>
        public IReadOnlyList<Squad> Squads
        {
            get
            {
                if (syncedSquads == null)
                    return new List<Squad>();
                if (squadsCacheDirty)
                    RebuildSquadCache();
                return localSquads;
            }
        }
        
        /// <summary>
        /// Total manpower across all squads.
        /// </summary>
        public int TotalManpower
        {
            get
            {
                if (syncedSquads == null) return 0;
                int total = 0;
                foreach (var data in syncedSquads)
                    total += data.Manpower;
                return total;
            }
        }
        
        /// <summary>
        /// Total manpower capacity across all squads.
        /// </summary>
        public int TotalManpowerCapacity
        {
            get
            {
                if (syncedSquads == null) return 0;
                int total = 0;
                foreach (var data in syncedSquads)
                    total += data.MaxManpower;
                return total;
            }
        }
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when a squad is created for this player.
        /// </summary>
        public event Action<Squad> OnSquadCreated;
        
        /// <summary>
        /// Fired when a squad's data changes (manpower, position, etc.).
        /// </summary>
        public event Action<Squad> OnSquadUpdated;
        
        /// <summary>
        /// Fired when a squad is destroyed (out of manpower, disbanded, etc.).
        /// </summary>
        public event Action<string> OnSquadDestroyed;
        
        /// <summary>
        /// Fired when any squad starts moving.
        /// </summary>
        public event Action<Squad, int, int> OnSquadMovementStarted; // squad, fromNode, toNode
        
        /// <summary>
        /// Fired when any squad arrives at destination.
        /// </summary>
        public event Action<Squad, int> OnSquadArrived; // squad, nodeId
        
        #endregion
        
        #region Unity Lifecycle
        
        void Awake()
        {
            // Ensure local cache is initialized
            if (localSquads == null)
            {
                localSquads = new List<Squad>();
            }
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            // SyncList is initialized by Mirror's Weaver - no action needed
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Subscribe to sync list changes (if available)
            if (syncedSquads != null)
            {
                syncedSquads.Callback += OnSyncedSquadsChanged;
            }
            
            // Build initial cache
            RebuildSquadCache();
        }
        
        public override void OnStopClient()
        {
            base.OnStopClient();
            
            if (syncedSquads != null)
            {
                syncedSquads.Callback -= OnSyncedSquadsChanged;
            }
        }
        
        void Update()
        {
            // Guard against Update being called before NetworkBehaviour is ready
            if (syncedSquads == null || !isManagerInitialized)
                return;
            
            // Only server processes movement completion
            if (!isServer || syncedSquads.Count == 0)
                return;
            
            ProcessSquadMovements();
        }
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Initialize this player's squad manager with faction and name.
        /// Called by server when player joins a faction.
        /// </summary>
        [Server]
        public void Initialize(Team faction, string displayName, int startingNodeId)
        {
            if (syncedSquads == null)
            {
                Debug.LogError($"[PlayerSquadManager] Cannot initialize - syncedSquads is null. Is NetworkBehaviour spawned?");
                return;
            }
            
            playerFaction = faction;
            playerDisplayName = displayName;
            
            Debug.Log($"[PlayerSquadManager] Initialized for {displayName} ({faction}) at node {startingNodeId}");
            
            // Create initial squads at faction's starting position
            CreateInitialSquads(startingNodeId);
            
            isManagerInitialized = true;
        }
        
        /// <summary>
        /// Create the player's initial set of squads.
        /// </summary>
        [Server]
        private void CreateInitialSquads(int startingNodeId)
        {
            if (syncedSquads == null)
            {
                Debug.LogError("[PlayerSquadManager] syncedSquads is null in CreateInitialSquads");
                return;
            }
            
            for (int i = 0; i < maxSquads; i++)
            {
                var squad = new Squad(
                    netId,
                    playerDisplayName ?? "",
                    playerFaction,
                    i,
                    startingNodeId,
                    defaultManpowerPerSquad
                );
                
                // Squads start empty - player must resupply
                squad.Manpower = 0;
                
                var syncData = squad.ToSyncData();
                syncedSquads.Add(syncData);
                
                Debug.Log($"[PlayerSquadManager] Created squad {squad.SquadId} for {playerDisplayName}");
            }
            
            squadsCacheDirty = true;
        }
        
        #endregion
        
        #region Squad Access
        
        /// <summary>
        /// Get a specific squad by index (0 to MaxSquadsPerPlayer-1).
        /// </summary>
        public Squad GetSquad(int index)
        {
            if (index < 0 || index >= syncedSquads.Count)
                return null;
            
            if (squadsCacheDirty)
                RebuildSquadCache();
            
            return localSquads[index];
        }
        
        /// <summary>
        /// Get a specific squad by its ID.
        /// </summary>
        public Squad GetSquadById(string squadId)
        {
            if (squadsCacheDirty)
                RebuildSquadCache();
            
            foreach (var squad in localSquads)
            {
                if (squad.SquadId == squadId)
                    return squad;
            }
            return null;
        }
        
        /// <summary>
        /// Get all squads currently at a specific node.
        /// </summary>
        public List<Squad> GetSquadsAtNode(int nodeId)
        {
            if (squadsCacheDirty)
                RebuildSquadCache();
            
            var result = new List<Squad>();
            foreach (var squad in localSquads)
            {
                if (squad.CurrentNodeId == nodeId && !squad.IsMoving)
                    result.Add(squad);
            }
            return result;
        }
        
        /// <summary>
        /// Get all squads currently moving to a specific node.
        /// </summary>
        public List<Squad> GetSquadsMovingToNode(int nodeId)
        {
            if (squadsCacheDirty)
                RebuildSquadCache();
            
            var result = new List<Squad>();
            foreach (var squad in localSquads)
            {
                if (squad.DestinationNodeId == nodeId && squad.IsMoving)
                    result.Add(squad);
            }
            return result;
        }
        
        #endregion
        
        #region Squad Commands (Client -> Server)
        
        /// <summary>
        /// Request to move a squad to a different node.
        /// </summary>
        [Command]
        public void CmdMoveSquad(int squadIndex, int targetNodeId)
        {
            if (squadIndex < 0 || squadIndex >= syncedSquads.Count)
            {
                Debug.LogWarning($"[PlayerSquadManager] Invalid squad index: {squadIndex}");
                return;
            }
            
            var squadData = syncedSquads[squadIndex];
            var squad = Squad.FromSyncData(squadData);
            
            // Validate movement
            if (!squad.CanInitiateMovement())
            {
                Debug.LogWarning($"[PlayerSquadManager] Squad {squad.SquadId} cannot move right now");
                TargetMoveSquadFailed(connectionToClient, squadIndex, "Squad cannot move right now");
                return;
            }
            
            // Validate connectivity through WarMapManager
            if (!ValidateMovePath(squad.CurrentNodeId, targetNodeId))
            {
                Debug.LogWarning($"[PlayerSquadManager] Invalid move path: {squad.CurrentNodeId} -> {targetNodeId}");
                TargetMoveSquadFailed(connectionToClient, squadIndex, "Nodes are not connected");
                return;
            }
            
            // Calculate travel time
            float travelTime = CalculateTravelTime(squad.CurrentNodeId, targetNodeId);
            
            // Update squad movement
            squad.StartMovement(targetNodeId, travelTime);
            
            // Sync the update
            syncedSquads[squadIndex] = squad.ToSyncData();
            squadsCacheDirty = true;
            
            // Notify
            RpcSquadMovementStarted(squadIndex, squad.CurrentNodeId, targetNodeId);
            
            Debug.Log($"[PlayerSquadManager] Squad {squad.SquadId} moving {squad.CurrentNodeId} -> {targetNodeId} (ETA: {travelTime:F1}s)");
        }
        
        /// <summary>
        /// Request to cancel a squad's movement.
        /// </summary>
        [Command]
        public void CmdCancelSquadMovement(int squadIndex)
        {
            if (squadIndex < 0 || squadIndex >= syncedSquads.Count)
                return;
            
            var squadData = syncedSquads[squadIndex];
            var squad = Squad.FromSyncData(squadData);
            
            if (squad.CancelMovement())
            {
                syncedSquads[squadIndex] = squad.ToSyncData();
                squadsCacheDirty = true;
                
                RpcSquadMovementCancelled(squadIndex);
                Debug.Log($"[PlayerSquadManager] Squad {squad.SquadId} movement cancelled");
            }
            else
            {
                TargetMoveSquadFailed(connectionToClient, squadIndex, "Cannot cancel - past point of no return");
            }
        }
        
        /// <summary>
        /// Request to resupply a squad from the faction token pool.
        /// </summary>
        [Command]
        public void CmdResupplySquad(int squadIndex, int amount)
        {
            if (squadIndex < 0 || squadIndex >= syncedSquads.Count)
                return;
            
            var squadData = syncedSquads[squadIndex];
            var squad = Squad.FromSyncData(squadData);
            
            // Can't resupply while moving
            if (squad.IsMoving)
            {
                TargetResupplyFailed(connectionToClient, squadIndex, "Cannot resupply while moving");
                return;
            }
            
            // Can't resupply if already full
            if (!squad.CanResupply)
            {
                TargetResupplyFailed(connectionToClient, squadIndex, "Squad is at full capacity");
                return;
            }
            
            // Check faction has enough tokens
            if (TokenSystem.Instance == null)
            {
                TargetResupplyFailed(connectionToClient, squadIndex, "Token system unavailable");
                return;
            }
            
            // Clamp to what the squad can hold
            int actualAmount = Mathf.Min(amount, squad.ResupplyCapacity);
            
            // Try to spend faction tokens (1:1 ratio)
            if (!TokenSystem.Instance.SpendTokens(playerFaction, actualAmount, $"Resupply squad {squad.SquadId}"))
            {
                TargetResupplyFailed(connectionToClient, squadIndex, "Insufficient faction tokens");
                return;
            }
            
            // Add manpower to squad
            squad.AddManpower(actualAmount);
            syncedSquads[squadIndex] = squad.ToSyncData();
            squadsCacheDirty = true;
            
            RpcSquadResupplied(squadIndex, actualAmount);
            Debug.Log($"[PlayerSquadManager] Squad {squad.SquadId} resupplied +{actualAmount} (now {squad.Manpower}/{squad.MaxManpower})");
        }
        
        #endregion
        
        #region Server-Side Squad Management (for testing/direct server calls)
        
        /// <summary>
        /// Server-side resupply that bypasses Command authority checks.
        /// Use this from test harnesses or server-authoritative systems.
        /// </summary>
        [Server]
        public bool ServerResupplySquad(int squadIndex, int amount)
        {
            if (squadIndex < 0 || squadIndex >= syncedSquads.Count)
                return false;
            
            var squadData = syncedSquads[squadIndex];
            var squad = Squad.FromSyncData(squadData);
            
            if (squad.IsMoving || !squad.CanResupply)
                return false;
            
            if (TokenSystem.Instance == null)
                return false;
            
            int actualAmount = Mathf.Min(amount, squad.ResupplyCapacity);
            
            if (!TokenSystem.Instance.SpendTokens(playerFaction, actualAmount, $"Resupply squad {squad.SquadId}"))
                return false;
            
            squad.AddManpower(actualAmount);
            syncedSquads[squadIndex] = squad.ToSyncData();
            squadsCacheDirty = true;
            
            RpcSquadResupplied(squadIndex, actualAmount);
            Debug.Log($"[PlayerSquadManager] Squad {squad.SquadId} resupplied +{actualAmount} (now {squad.Manpower}/{squad.MaxManpower})");
            return true;
        }
        
        /// <summary>
        /// Server-side move that bypasses Command authority checks.
        /// Use this from test harnesses or server-authoritative systems.
        /// </summary>
        [Server]
        public bool ServerMoveSquad(int squadIndex, int targetNodeId)
        {
            if (squadIndex < 0 || squadIndex >= syncedSquads.Count)
                return false;
            
            var squadData = syncedSquads[squadIndex];
            var squad = Squad.FromSyncData(squadData);
            
            if (!squad.CanInitiateMovement())
                return false;
            
            if (!ValidateMovePath(squad.CurrentNodeId, targetNodeId))
                return false;
            
            float travelTime = CalculateTravelTime(squad.CurrentNodeId, targetNodeId);
            
            squad.StartMovement(targetNodeId, travelTime);
            
            syncedSquads[squadIndex] = squad.ToSyncData();
            squadsCacheDirty = true;
            
            RpcSquadMovementStarted(squadIndex, squad.CurrentNodeId, targetNodeId);
            Debug.Log($"[PlayerSquadManager] Squad {squad.SquadId} moving {squad.CurrentNodeId} -> {targetNodeId} (ETA: {travelTime:F1}s)");
            return true;
        }
        
        /// <summary>
        /// Process squad movements and complete arrivals.
        /// Called every frame on server.
        /// </summary>
        [Server]
        private void ProcessSquadMovements()
        {
            for (int i = 0; i < syncedSquads.Count; i++)
            {
                var squadData = syncedSquads[i];
                
                // Skip if not moving
                if (squadData.MovementState != (int)SquadMovementState.Moving)
                    continue;
                
                // Check if arrived
                if (Time.time >= squadData.MovementArrivalTime)
                {
                    var squad = Squad.FromSyncData(squadData);
                    int arrivedAt = squad.DestinationNodeId;
                    
                    squad.CompleteMovement();
                    
                    syncedSquads[i] = squad.ToSyncData();
                    squadsCacheDirty = true;
                    
                    RpcSquadArrived(i, arrivedAt);
                    Debug.Log($"[PlayerSquadManager] Squad {squad.SquadId} arrived at node {arrivedAt}");
                }
            }
        }
        
        /// <summary>
        /// Consume manpower from a specific squad (called when player spawns in FPS).
        /// </summary>
        /// <param name="squadId">ID of the squad to consume from</param>
        /// <param name="amount">Amount of manpower to consume (usually 1)</param>
        /// <returns>True if successful</returns>
        [Server]
        public bool ConsumeManpower(string squadId, int amount = 1)
        {
            for (int i = 0; i < syncedSquads.Count; i++)
            {
                if (syncedSquads[i].SquadId == squadId)
                {
                    var squad = Squad.FromSyncData(syncedSquads[i]);
                    
                    if (squad.Manpower < amount)
                        return false;
                    
                    squad.RemoveManpower(amount);
                    syncedSquads[i] = squad.ToSyncData();
                    squadsCacheDirty = true;
                    
                    RpcSquadManpowerChanged(i, squad.Manpower);
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// Update a squad's data directly (server authority).
        /// </summary>
        [Server]
        public void UpdateSquad(int squadIndex, Squad updatedSquad)
        {
            if (squadIndex < 0 || squadIndex >= syncedSquads.Count)
                return;
            
            syncedSquads[squadIndex] = updatedSquad.ToSyncData();
            squadsCacheDirty = true;
        }
        
        #endregion
        
        #region Movement Validation & Calculation
        
        /// <summary>
        /// Validate that a path between two nodes exists and is valid.
        /// </summary>
        private bool ValidateMovePath(int fromNodeId, int toNodeId)
        {
            if (WarMapManager.Instance == null)
                return false;
            
            var fromNode = WarMapManager.Instance.GetNodeByID(fromNodeId);
            var toNode = WarMapManager.Instance.GetNodeByID(toNodeId);
            
            if (fromNode == null || toNode == null)
                return false;
            
            // Check direct connectivity
            return fromNode.IsConnectedTo(toNode);
        }
        
        /// <summary>
        /// Calculate travel time between two nodes.
        /// </summary>
        private float CalculateTravelTime(int fromNodeId, int toNodeId)
        {
            // Base travel time
            const float baseTime = 15f; // 15 seconds base travel
            
            if (WarMapManager.Instance == null)
                return baseTime;
            
            var fromNode = WarMapManager.Instance.GetNodeByID(fromNodeId);
            var toNode = WarMapManager.Instance.GetNodeByID(toNodeId);
            
            if (fromNode == null || toNode == null)
                return baseTime;
            
            // Calculate distance-based time (if nodes have world positions)
            float distance = Vector3.Distance(fromNode.transform.position, toNode.transform.position);
            
            // 3 units per second travel speed
            const float travelSpeed = 3f;
            float travelTime = distance / travelSpeed;
            
            // Minimum 10 seconds, maximum 60 seconds
            return Mathf.Clamp(travelTime, 10f, 60f);
        }
        
        #endregion
        
        #region Network Callbacks
        
        /// <summary>
        /// Called when syncedSquads changes.
        /// </summary>
        private void OnSyncedSquadsChanged(SyncList<SquadSyncData>.Operation op, int index, 
                                            SquadSyncData oldItem, SquadSyncData newItem)
        {
            squadsCacheDirty = true;
            
            switch (op)
            {
                case SyncList<SquadSyncData>.Operation.OP_ADD:
                    OnSquadCreated?.Invoke(Squad.FromSyncData(newItem));
                    break;
                    
                case SyncList<SquadSyncData>.Operation.OP_SET:
                    OnSquadUpdated?.Invoke(Squad.FromSyncData(newItem));
                    break;
                    
                case SyncList<SquadSyncData>.Operation.OP_REMOVEAT:
                    OnSquadDestroyed?.Invoke(oldItem.SquadId);
                    break;
            }
        }
        
        /// <summary>
        /// Rebuild the local squad cache from sync data.
        /// </summary>
        private void RebuildSquadCache()
        {
            if (localSquads == null)
                localSquads = new List<Squad>();
            
            localSquads.Clear();
            
            if (syncedSquads == null)
            {
                squadsCacheDirty = false;
                return;
            }
            
            foreach (var data in syncedSquads)
            {
                localSquads.Add(Squad.FromSyncData(data));
            }
            squadsCacheDirty = false;
        }
        
        #endregion
        
        #region Client RPCs
        
        [ClientRpc]
        private void RpcSquadMovementStarted(int squadIndex, int fromNode, int toNode)
        {
            if (squadsCacheDirty)
                RebuildSquadCache();
            
            if (squadIndex < localSquads.Count)
            {
                OnSquadMovementStarted?.Invoke(localSquads[squadIndex], fromNode, toNode);
            }
        }
        
        [ClientRpc]
        private void RpcSquadArrived(int squadIndex, int nodeId)
        {
            squadsCacheDirty = true;
            
            if (squadIndex < localSquads.Count)
            {
                RebuildSquadCache();
                OnSquadArrived?.Invoke(localSquads[squadIndex], nodeId);
            }
        }
        
        [ClientRpc]
        private void RpcSquadMovementCancelled(int squadIndex)
        {
            squadsCacheDirty = true;
            Debug.Log($"[PlayerSquadManager] Squad movement cancelled notification");
        }
        
        [ClientRpc]
        private void RpcSquadResupplied(int squadIndex, int amount)
        {
            squadsCacheDirty = true;
            Debug.Log($"[PlayerSquadManager] Squad resupplied +{amount}");
        }
        
        [ClientRpc]
        private void RpcSquadManpowerChanged(int squadIndex, int newManpower)
        {
            squadsCacheDirty = true;
        }
        
        #endregion
        
        #region Targeted Client RPCs (Failure messages)
        
        [TargetRpc]
        private void TargetMoveSquadFailed(NetworkConnection target, int squadIndex, string reason)
        {
            Debug.LogWarning($"[PlayerSquadManager] Move failed for squad {squadIndex}: {reason}");
            // UI can hook into this via event if needed
        }
        
        [TargetRpc]
        private void TargetResupplyFailed(NetworkConnection target, int squadIndex, string reason)
        {
            Debug.LogWarning($"[PlayerSquadManager] Resupply failed for squad {squadIndex}: {reason}");
        }
        
        #endregion
    }
}
