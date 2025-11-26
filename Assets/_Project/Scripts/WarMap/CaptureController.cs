using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using ElitesAndPawns.Core;

namespace ElitesAndPawns.WarMap
{
    /// <summary>
    /// Manages capture timers for war map nodes.
    /// Handles the 60-second uncontested capture mechanic and contested state detection.
    /// When a faction has squads at a non-allied node:
    /// - If uncontested: 60-second timer starts, node captured when complete
    /// - If contested (enemy squads present or arrive): timer paused, FPS battle prepared
    /// </summary>
    public class CaptureController : NetworkBehaviour
    {
        #region Singleton
        
        private static CaptureController _instance;
        public static CaptureController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<CaptureController>();
                }
                return _instance;
            }
        }
        
        #endregion
        
        #region Fields
        
        [Header("Capture Configuration")]
        [SerializeField] private float uncontestedCaptureTime = 60f;
        [SerializeField] private float captureCheckInterval = 0.5f;
        
        /// <summary>
        /// Active capture attempts. Key = NodeID.
        /// </summary>
        private Dictionary<int, CaptureAttempt> activeCaptureAttempts = new Dictionary<int, CaptureAttempt>();
        
        /// <summary>
        /// Time until next capture state check.
        /// </summary>
        private float nextCheckTime;
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when a capture attempt starts.
        /// Parameters: nodeId, attackingFaction
        /// </summary>
        public static event Action<int, Team> OnCaptureStarted;
        
        /// <summary>
        /// Fired when capture progress updates.
        /// Parameters: nodeId, attackingFaction, progress (0-1)
        /// </summary>
        public static event Action<int, Team, float> OnCaptureProgress;
        
        /// <summary>
        /// Fired when a node is captured (uncontested victory).
        /// Parameters: nodeId, newOwner
        /// </summary>
        public static event Action<int, Team> OnNodeCapturedUncontested;
        
        /// <summary>
        /// Fired when a capture becomes contested (enemy squads arrive).
        /// Parameters: nodeId, attackingFaction, defendingFaction
        /// </summary>
        public static event Action<int, Team, Team> OnCaptureContested;
        
        /// <summary>
        /// Fired when a capture attempt is cancelled (attacker withdrew).
        /// Parameters: nodeId
        /// </summary>
        public static event Action<int> OnCaptureCancelled;
        
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
        
        void Update()
        {
            if (!isServer)
                return;
            
            // Periodic check for capture state changes
            if (Time.time >= nextCheckTime)
            {
                CheckAllNodes();
                nextCheckTime = Time.time + captureCheckInterval;
            }
            
            // Update active capture timers
            UpdateCaptureTimers();
        }
        
        #endregion
        
        #region Capture State Checking
        
        /// <summary>
        /// Check all nodes for potential capture attempts.
        /// </summary>
        [Server]
        private void CheckAllNodes()
        {
            if (WarMapManager.Instance == null)
            {
                Debug.LogWarning("[CaptureController] WarMapManager.Instance is null!");
                return;
            }
            
            if (NodeOccupancy.Instance == null)
            {
                Debug.LogWarning("[CaptureController] NodeOccupancy.Instance is null!");
                return;
            }
            
            var nodes = WarMapManager.Instance.Nodes;
            if (nodes == null || nodes.Count == 0)
            {
                // Only log once per second to avoid spam
                if (Time.frameCount % 60 == 0)
                    Debug.LogWarning($"[CaptureController] No nodes found! Nodes list: {(nodes == null ? "null" : nodes.Count.ToString())}");
                return;
            }
            
            foreach (var node in nodes)
            {
                CheckNodeCaptureState(node);
            }
        }
        
        /// <summary>
        /// Check a single node's capture state.
        /// </summary>
        [Server]
        private void CheckNodeCaptureState(WarMapNode node)
        {
            int nodeId = node.NodeID;
            Team nodeOwner = node.ControllingFaction;
            
            // Get faction presence at this node
            int blueManpower = NodeOccupancy.Instance.GetFactionManpowerAtNode(nodeId, Team.Blue);
            int redManpower = NodeOccupancy.Instance.GetFactionManpowerAtNode(nodeId, Team.Red);
            int greenManpower = NodeOccupancy.Instance.GetFactionManpowerAtNode(nodeId, Team.Green);
            
            // Debug: Log if any manpower detected at non-owned node
            if ((blueManpower > 0 && nodeOwner != Team.Blue) ||
                (redManpower > 0 && nodeOwner != Team.Red))
            {
                Debug.Log($"[CaptureController] Node {nodeId} ({node.NodeName}): Owner={nodeOwner}, Blue={blueManpower}, Red={redManpower}");
            }
            
            // Determine which factions are present (excluding owner if they have troops)
            var presentFactions = new List<Team>();
            if (blueManpower > 0 && nodeOwner != Team.Blue) presentFactions.Add(Team.Blue);
            if (redManpower > 0 && nodeOwner != Team.Red) presentFactions.Add(Team.Red);
            if (greenManpower > 0 && nodeOwner != Team.Green) presentFactions.Add(Team.Green);
            
            // Also check if owner has defenders
            int ownerManpower = 0;
            switch (nodeOwner)
            {
                case Team.Blue: ownerManpower = blueManpower; break;
                case Team.Red: ownerManpower = redManpower; break;
                case Team.Green: ownerManpower = greenManpower; break;
            }
            
            // Handle different scenarios
            if (presentFactions.Count == 0)
            {
                // No attackers present - cancel any capture attempt
                if (activeCaptureAttempts.ContainsKey(nodeId))
                {
                    CancelCapture(nodeId);
                }
                return;
            }
            
            if (presentFactions.Count == 1)
            {
                Team attacker = presentFactions[0];
                
                // Single attacker present
                if (ownerManpower > 0)
                {
                    // Defender has troops - contested!
                    HandleContestedNode(nodeId, attacker, nodeOwner);
                }
                else
                {
                    // No defenders - start or continue uncontested capture
                    HandleUncontestedCapture(nodeId, attacker, node);
                }
            }
            else
            {
                // Multiple attackers (rare three-way fight)
                // For now, pick the faction with most manpower as primary attacker
                Team primaryAttacker = Team.None;
                int maxManpower = 0;
                
                foreach (var faction in presentFactions)
                {
                    int mp = NodeOccupancy.Instance.GetFactionManpowerAtNode(nodeId, faction);
                    if (mp > maxManpower)
                    {
                        maxManpower = mp;
                        primaryAttacker = faction;
                    }
                }
                
                // Multi-faction battle
                HandleMultiFactionContest(nodeId, presentFactions, nodeOwner);
            }
        }
        
        #endregion
        
        #region Capture Handling
        
        /// <summary>
        /// Handle uncontested capture (timer-based OR instant if contest was won).
        /// </summary>
        [Server]
        private void HandleUncontestedCapture(int nodeId, Team attacker, WarMapNode node)
        {
            if (activeCaptureAttempts.TryGetValue(nodeId, out var attempt))
            {
                // Check if this was previously contested
                if (attempt.State == CaptureState.Contested)
                {
                    // Contest resolved - attacker wins! Capture instantly.
                    Debug.Log($"[CaptureController] Node {nodeId}: Contest resolved! {attacker} captures instantly (defenders withdrew)!");
                    CompleteCapture(nodeId, attacker);
                    return;
                }
                
                // Capture already in progress
                if (attempt.AttackingFaction == attacker && 
                    attempt.State == CaptureState.Capturing)
                {
                    // Same attacker, continue capture
                    return;
                }
                else
                {
                    // Different attacker - cancel and start new
                    CancelCapture(nodeId);
                }
            }
            
            // Start new capture attempt (60-second timer for fresh captures)
            var newAttempt = new CaptureAttempt
            {
                NodeId = nodeId,
                AttackingFaction = attacker,
                DefendingFaction = node.ControllingFaction,
                StartTime = Time.time,
                CaptureProgress = 0f,
                State = CaptureState.Capturing
            };
            
            activeCaptureAttempts[nodeId] = newAttempt;
            
            OnCaptureStarted?.Invoke(nodeId, attacker);
            RpcNotifyCaptureStarted(nodeId, attacker);
            
            Debug.Log($"[CaptureController] Node {nodeId}: {attacker} started capture (uncontested, 60s timer)");
        }
        
        /// <summary>
        /// Handle contested capture (FPS battle needed).
        /// </summary>
        [Server]
        private void HandleContestedNode(int nodeId, Team attacker, Team defender)
        {
            if (activeCaptureAttempts.TryGetValue(nodeId, out var attempt))
            {
                if (attempt.State == CaptureState.Contested)
                {
                    // Already contested - check if one side has been eliminated
                    int attackerMP = NodeOccupancy.Instance.GetFactionManpowerAtNode(nodeId, attacker);
                    int defenderMP = NodeOccupancy.Instance.GetFactionManpowerAtNode(nodeId, defender);
                    
                    if (attackerMP <= 0 && defenderMP > 0)
                    {
                        // Attacker eliminated - defender holds
                        Debug.Log($"[CaptureController] Node {nodeId}: Attacker {attacker} eliminated! {defender} holds.");
                        CancelCapture(nodeId);
                        return;
                    }
                    else if (defenderMP <= 0 && attackerMP > 0)
                    {
                        // Defender eliminated - attacker captures instantly!
                        Debug.Log($"[CaptureController] Node {nodeId}: Defender {defender} eliminated! {attacker} captures instantly!");
                        CompleteCapture(nodeId, attacker);
                        return;
                    }
                    // Both still have troops - remain contested
                    return;
                }
                
                // Transition from capturing to contested
                attempt.State = CaptureState.Contested;
                attempt.ContestedTime = Time.time;
                
                Debug.Log($"[CaptureController] Node {nodeId}: Capture contested! {attacker} vs {defender}");
            }
            else
            {
                // New contested state
                var newAttempt = new CaptureAttempt
                {
                    NodeId = nodeId,
                    AttackingFaction = attacker,
                    DefendingFaction = defender,
                    StartTime = Time.time,
                    CaptureProgress = 0f,
                    State = CaptureState.Contested,
                    ContestedTime = Time.time
                };
                
                activeCaptureAttempts[nodeId] = newAttempt;
            }
            
            // Mark node as contested in war map
            var node = WarMapManager.Instance?.GetNodeByID(nodeId);
            if (node != null && !node.IsContested)
            {
                node.SetContested(true, attacker);
            }
            
            OnCaptureContested?.Invoke(nodeId, attacker, defender);
            RpcNotifyCaptureContested(nodeId, attacker, defender);
            
            // Prepare for FPS battle (this would trigger battle scene loading)
            PrepareFPSBattle(nodeId, attacker, defender);
        }
        
        /// <summary>
        /// Handle multi-faction contest (three-way fight).
        /// </summary>
        [Server]
        private void HandleMultiFactionContest(int nodeId, List<Team> factions, Team originalOwner)
        {
            // For multi-faction, we need special handling
            // For now, treat as contested with highest manpower as primary attacker
            Debug.Log($"[CaptureController] Node {nodeId}: Multi-faction contest! {string.Join(", ", factions)}");
            
            // Mark node as heavily contested
            var node = WarMapManager.Instance?.GetNodeByID(nodeId);
            if (node != null)
            {
                node.SetContested(true, factions[0]); // Primary attacker
            }
            
            // Create contested attempt
            var attempt = new CaptureAttempt
            {
                NodeId = nodeId,
                AttackingFaction = factions[0],
                DefendingFaction = originalOwner != Team.None ? originalOwner : factions[1],
                StartTime = Time.time,
                State = CaptureState.Contested,
                ContestedTime = Time.time,
                IsMultiFaction = true
            };
            
            activeCaptureAttempts[nodeId] = attempt;
        }
        
        /// <summary>
        /// Cancel an active capture attempt.
        /// </summary>
        [Server]
        private void CancelCapture(int nodeId)
        {
            if (activeCaptureAttempts.Remove(nodeId))
            {
                var node = WarMapManager.Instance?.GetNodeByID(nodeId);
                if (node != null && node.IsContested)
                {
                    node.SetContested(false);
                }
                
                OnCaptureCancelled?.Invoke(nodeId);
                RpcNotifyCaptureCancelled(nodeId);
                
                Debug.Log($"[CaptureController] Node {nodeId}: Capture cancelled");
            }
        }
        
        /// <summary>
        /// Complete a capture (uncontested victory or contest won).
        /// </summary>
        [Server]
        private void CompleteCapture(int nodeId, Team newOwner)
        {
            var node = WarMapManager.Instance?.GetNodeByID(nodeId);
            if (node == null)
                return;
            
            Team previousOwner = node.ControllingFaction;
            
            // Retreat any losing faction's squads before changing ownership
            if (previousOwner != Team.None && previousOwner != newOwner)
            {
                RetreatSquadsFromNode(nodeId, previousOwner);
            }
            
            // Transfer control
            node.SetControl(newOwner, 100f);
            node.SetContested(false);
            
            // Remove capture attempt
            activeCaptureAttempts.Remove(nodeId);
            
            OnNodeCapturedUncontested?.Invoke(nodeId, newOwner);
            RpcNotifyNodeCaptured(nodeId, newOwner);
            
            Debug.Log($"[CaptureController] Node {nodeId}: Captured by {newOwner}!");
        }
        
        /// <summary>
        /// Force squads of a faction to retreat from a node to nearest allied node.
        /// </summary>
        [Server]
        private void RetreatSquadsFromNode(int nodeId, Team retreatingFaction)
        {
            if (NodeOccupancy.Instance == null || WarMapManager.Instance == null)
                return;
            
            // Find nearest allied node for retreat
            int retreatNodeId = FindNearestAlliedNode(nodeId, retreatingFaction);
            if (retreatNodeId == -1)
            {
                Debug.LogWarning($"[CaptureController] No retreat destination for {retreatingFaction} from node {nodeId}!");
                return;
            }
            
            // Get all squad managers and retreat their squads
            var squadsAtNode = NodeOccupancy.Instance.GetSquadsAtNode(nodeId);
            foreach (var squadPresence in squadsAtNode)
            {
                if (squadPresence.Faction != retreatingFaction)
                    continue;
                
                // Find the PlayerSquadManager that owns this squad
                var allManagers = FindObjectsByType<PlayerSquadManager>(FindObjectsSortMode.None);
                foreach (var manager in allManagers)
                {
                    if (manager.netId == squadPresence.OwnerNetId)
                    {
                        // Find squad index by ID
                        for (int i = 0; i < manager.SquadCount; i++)
                        {
                            var squad = manager.GetSquad(i);
                            if (squad != null && squad.SquadId == squadPresence.SquadId)
                            {
                                manager.ServerMoveSquad(i, retreatNodeId);
                                Debug.Log($"[CaptureController] Retreating {squadPresence.SquadId} to node {retreatNodeId}");
                                break;
                            }
                        }
                        break;
                    }
                }
            }
        }
        
        /// <summary>
        /// Find the nearest connected node controlled by a faction.
        /// </summary>
        private int FindNearestAlliedNode(int fromNodeId, Team faction)
        {
            var fromNode = WarMapManager.Instance?.GetNodeByID(fromNodeId);
            if (fromNode == null)
                return -1;
            
            // Check connected nodes first
            float nearestDistance = float.MaxValue;
            int nearestNodeId = -1;
            
            foreach (var connectedNode in fromNode.ConnectedNodes)
            {
                if (connectedNode.ControllingFaction == faction)
                {
                    float distance = Vector3.Distance(fromNode.transform.position, connectedNode.transform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestNodeId = connectedNode.NodeID;
                    }
                }
            }
            
            // If no directly connected allied node, search all nodes (BFS would be better but this works)
            if (nearestNodeId == -1)
            {
                foreach (var node in WarMapManager.Instance.Nodes)
                {
                    if (node.ControllingFaction == faction && node.NodeID != fromNodeId)
                    {
                        float distance = Vector3.Distance(fromNode.transform.position, node.transform.position);
                        if (distance < nearestDistance)
                        {
                            nearestDistance = distance;
                            nearestNodeId = node.NodeID;
                        }
                    }
                }
            }
            
            return nearestNodeId;
        }
        
        #endregion
        
        #region Timer Updates
        
        /// <summary>
        /// Update all active capture timers.
        /// </summary>
        [Server]
        private void UpdateCaptureTimers()
        {
            var completedCaptures = new List<int>();
            
            foreach (var kvp in activeCaptureAttempts)
            {
                var attempt = kvp.Value;
                
                // Only update timer for uncontested captures
                if (attempt.State != CaptureState.Capturing)
                    continue;
                
                // Update progress
                float elapsed = Time.time - attempt.StartTime;
                attempt.CaptureProgress = Mathf.Clamp01(elapsed / uncontestedCaptureTime);
                
                // Fire progress event periodically (not every frame)
                if (Mathf.FloorToInt(elapsed) != Mathf.FloorToInt(elapsed - Time.deltaTime))
                {
                    OnCaptureProgress?.Invoke(attempt.NodeId, attempt.AttackingFaction, attempt.CaptureProgress);
                    RpcUpdateCaptureProgress(attempt.NodeId, attempt.CaptureProgress);
                }
                
                // Check for completion
                if (attempt.CaptureProgress >= 1f)
                {
                    completedCaptures.Add(kvp.Key);
                }
            }
            
            // Complete captures outside of iteration
            foreach (int nodeId in completedCaptures)
            {
                var attempt = activeCaptureAttempts[nodeId];
                CompleteCapture(nodeId, attempt.AttackingFaction);
            }
        }
        
        #endregion
        
        #region FPS Battle Preparation
        
        /// <summary>
        /// Prepare a node for FPS battle.
        /// </summary>
        [Server]
        private void PrepareFPSBattle(int nodeId, Team attacker, Team defender)
        {
            // This integrates with WarMapManager's battle system
            if (WarMapManager.Instance != null)
            {
                // The battle will be initiated when players choose to engage
                // For now, mark the node as battle-ready
                
                var node = WarMapManager.Instance.GetNodeByID(nodeId);
                if (node != null && !node.IsBattleActive)
                {
                    // Don't auto-start battle, let players initiate
                    // Or we could auto-start after a grace period
                    Debug.Log($"[CaptureController] Node {nodeId} ready for FPS battle: {attacker} vs {defender}");
                }
            }
        }
        
        #endregion
        
        #region Queries
        
        /// <summary>
        /// Get the capture attempt for a node (if any).
        /// </summary>
        public CaptureAttempt GetCaptureAttempt(int nodeId)
        {
            if (activeCaptureAttempts.TryGetValue(nodeId, out var attempt))
                return attempt;
            return null;
        }
        
        /// <summary>
        /// Check if a node has an active capture attempt.
        /// </summary>
        public bool IsCapturing(int nodeId)
        {
            return activeCaptureAttempts.ContainsKey(nodeId);
        }
        
        /// <summary>
        /// Get remaining time until capture completes (for uncontested captures).
        /// Returns -1 if contested or not capturing.
        /// </summary>
        public float GetRemainingCaptureTime(int nodeId)
        {
            if (activeCaptureAttempts.TryGetValue(nodeId, out var attempt))
            {
                if (attempt.State == CaptureState.Capturing)
                {
                    float elapsed = Time.time - attempt.StartTime;
                    return Mathf.Max(0f, uncontestedCaptureTime - elapsed);
                }
            }
            return -1f;
        }
        
        /// <summary>
        /// Get all active capture attempts.
        /// </summary>
        public Dictionary<int, CaptureAttempt> GetAllCaptureAttempts()
        {
            return new Dictionary<int, CaptureAttempt>(activeCaptureAttempts);
        }
        
        #endregion
        
        #region Network RPCs
        
        [ClientRpc]
        private void RpcNotifyCaptureStarted(int nodeId, Team attacker)
        {
            Debug.Log($"[CaptureController-Client] Node {nodeId}: {attacker} started capture");
            OnCaptureStarted?.Invoke(nodeId, attacker);
        }
        
        [ClientRpc]
        private void RpcNotifyCaptureContested(int nodeId, Team attacker, Team defender)
        {
            Debug.Log($"[CaptureController-Client] Node {nodeId}: Contested! {attacker} vs {defender}");
            OnCaptureContested?.Invoke(nodeId, attacker, defender);
        }
        
        [ClientRpc]
        private void RpcNotifyCaptureCancelled(int nodeId)
        {
            Debug.Log($"[CaptureController-Client] Node {nodeId}: Capture cancelled");
            OnCaptureCancelled?.Invoke(nodeId);
        }
        
        [ClientRpc]
        private void RpcNotifyNodeCaptured(int nodeId, Team newOwner)
        {
            Debug.Log($"[CaptureController-Client] Node {nodeId}: Captured by {newOwner}!");
            OnNodeCapturedUncontested?.Invoke(nodeId, newOwner);
        }
        
        [ClientRpc]
        private void RpcUpdateCaptureProgress(int nodeId, float progress)
        {
            // UI can subscribe to OnCaptureProgress event
            OnCaptureProgress?.Invoke(nodeId, Team.None, progress);
        }
        
        #endregion
    }
    
    #region Data Classes
    
    /// <summary>
    /// Represents an active capture attempt at a node.
    /// </summary>
    [Serializable]
    public class CaptureAttempt
    {
        public int NodeId;
        public Team AttackingFaction;
        public Team DefendingFaction;
        public CaptureState State;
        public float StartTime;
        public float CaptureProgress; // 0-1
        public float ContestedTime; // When it became contested
        public bool IsMultiFaction;
        
        /// <summary>
        /// Time remaining for uncontested capture (in seconds).
        /// </summary>
        public float RemainingTime(float captureTime)
        {
            if (State != CaptureState.Capturing)
                return -1f;
            return Mathf.Max(0f, captureTime - (Time.time - StartTime));
        }
    }
    
    /// <summary>
    /// State of a capture attempt.
    /// </summary>
    public enum CaptureState
    {
        /// <summary>Timer counting down, no defenders present.</summary>
        Capturing,
        
        /// <summary>Defenders arrived, FPS battle needed.</summary>
        Contested,
        
        /// <summary>FPS battle is in progress.</summary>
        BattleInProgress,
        
        /// <summary>Capture completed successfully.</summary>
        Completed,
        
        /// <summary>Capture was cancelled (attacker withdrew).</summary>
        Cancelled
    }
    
    #endregion
}
