using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;
using ElitesAndPawns.Core;
using ElitesAndPawns.Networking;

namespace ElitesAndPawns.WarMap
{
    /// <summary>
    /// Main controller for the War Map system.
    /// Manages the REAL-TIME strategic layer of the game including node control, battles, and faction progression.
    /// All factions can act simultaneously without turns.
    /// </summary>
    public class WarMapManager : NetworkBehaviour
    {
        #region Singleton
        
        private static WarMapManager _instance;
        public static WarMapManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<WarMapManager>();
                }
                return _instance;
            }
        }
        
        #endregion
        
        #region Fields
        
        [Header("War Map Configuration")]
        [SerializeField] private WarMapConfiguration mapConfig;
        [SerializeField] private GameObject warMapNodePrefab;
        [SerializeField] private Transform nodeContainer;
        [SerializeField] private float nodeSpacing = 200f;
        
        [Header("Battle Configuration")]
        [SerializeField] private string battleSceneName = "NetworkTest";
        [SerializeField] private int battleInitiationCost = 100;
        [SerializeField] private int maxSimultaneousBattles = 3; // Multiple concurrent battles
        [SerializeField] private float battleTimeout = 1800f; // 30 minutes max battle time
        
        [Header("Victory Conditions")]
        [SerializeField] private int nodesRequiredForVictory = 4; // Control 4 of 5 nodes
        [SerializeField] private int tokensRequiredForVictory = 5000;
        [SerializeField] private float controlPercentageRequired = 80f; // 80% control of owned nodes
        
        [Header("Current War State")]
        private List<WarMapNode> warMapNodes = new List<WarMapNode>();
        private Dictionary<int, BattleSession> activeBattles = new Dictionary<int, BattleSession>();
        private bool warActive = false;
        
        // Network synced state
        [SyncVar(hook = nameof(OnWarStateChanged))]
        private WarState currentWarState = WarState.Preparation;
        
        [SyncVar]
        private Team winningFaction = Team.None;
        
        #endregion
        
        #region Properties
        
        public List<WarMapNode> Nodes => warMapNodes;
        public WarState CurrentState => currentWarState;
        public bool IsWarActive => warActive;
        public Team WinningFaction => winningFaction;
        public int ActiveBattleCount => activeBattles.Count;
        
        #endregion
        
        #region Events
        
        public static event Action<WarState> OnWarStateUpdated;
        public static event Action<WarMapNode, Team> OnBattleRequested;
        public static event Action<BattleSession> OnBattleStarted;
        public static event Action<BattleSession, BattleResult> OnBattleCompleted;
        public static event Action<Team> OnWarEnded;
        
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
            DontDestroyOnLoad(gameObject);
        }
        
        void Start()
        {
            if (isServer)
            {
                InitializeWarMap();
            }
        }
        
        void Update()
        {
            if (!isServer || !warActive)
                return;
                
            // Update active battles
            UpdateBattleSessions();
            
            // Check victory conditions
            CheckVictoryConditions();
        }
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Initialize the war map with nodes and connections
        /// </summary>
        [Server]
        private void InitializeWarMap()
        {
            Debug.Log("[WarMapManager] Initializing Real-Time War Map...");
            
            // Load or use default configuration
            if (mapConfig == null)
            {
                mapConfig = CreateDefaultMapConfiguration();
            }
            
            // Create nodes
            CreateWarMapNodes();
            
            // Set initial faction control
            SetInitialFactionControl();
            
            // Start the war
            StartWar();
        }
        
        /// <summary>
        /// Create the default 5-node map configuration
        /// </summary>
        private WarMapConfiguration CreateDefaultMapConfiguration()
        {
            var config = new WarMapConfiguration();
            
            // Node 0: Blue Capital (left)
            config.NodeConfigs.Add(new NodeConfig
            {
                NodeID = 0,
                NodeName = "Blue Stronghold",
                NodeType = WarMapNode.NodeType.Capital,
                Position = new Vector2(-400, 0),
                ConnectedNodes = new List<int> { 1, 2 }
            });
            
            // Node 1: Strategic Center-Top
            config.NodeConfigs.Add(new NodeConfig
            {
                NodeID = 1,
                NodeName = "Northern Outpost",
                NodeType = WarMapNode.NodeType.Strategic,
                Position = new Vector2(0, 200),
                ConnectedNodes = new List<int> { 0, 2, 3 }
            });
            
            // Node 2: Resource Center
            config.NodeConfigs.Add(new NodeConfig
            {
                NodeID = 2,
                NodeName = "Resource Hub",
                NodeType = WarMapNode.NodeType.Resource,
                Position = new Vector2(0, -200),
                ConnectedNodes = new List<int> { 0, 1, 3, 4 }
            });
            
            // Node 3: Strategic Center-Bottom
            config.NodeConfigs.Add(new NodeConfig
            {
                NodeID = 3,
                NodeName = "Southern Fort",
                NodeType = WarMapNode.NodeType.Strategic,
                Position = new Vector2(0, 0),
                ConnectedNodes = new List<int> { 1, 2, 4 }
            });
            
            // Node 4: Red Capital (right)
            config.NodeConfigs.Add(new NodeConfig
            {
                NodeID = 4,
                NodeName = "Red Fortress",
                NodeType = WarMapNode.NodeType.Capital,
                Position = new Vector2(400, 0),
                ConnectedNodes = new List<int> { 2, 3 }
            });
            
            return config;
        }
        
        /// <summary>
        /// Manually add nodes to the war map (for testing/external node creation)
        /// </summary>
        [Server]
        public void RegisterExistingNodes()
        {
            warMapNodes.Clear();
            
            WarMapNode[] existingNodes = FindObjectsByType<WarMapNode>(FindObjectsSortMode.None);
            if (existingNodes != null && existingNodes.Length > 0)
            {
                Debug.Log($"[WarMapManager] Registering {existingNodes.Length} existing nodes");
                warMapNodes.AddRange(existingNodes);
                warMapNodes.Sort((a, b) => a.NodeID.CompareTo(b.NodeID));
                
                // CRITICAL: Force connection setup for all nodes
                foreach (var node in warMapNodes)
                {
                    // Use reflection to call private method
                    var method = node.GetType().GetMethod("ConnectToNodes", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    method?.Invoke(node, null);
                }
                
                // Set initial faction control if not already set
                SetInitialFactionControl();
                
                Debug.Log($"[WarMapManager] All nodes connected and initialized");
            }
            else
            {
                Debug.LogWarning("[WarMapManager] No nodes found to register");
            }
        }
        
        /// <summary>
        /// Create war map nodes in the scene
        /// </summary>
        [Server]
        private void CreateWarMapNodes()
        {
            warMapNodes.Clear();
            
            // First, check if nodes already exist in the scene (e.g., created by TestHarness)
            WarMapNode[] existingNodes = FindObjectsByType<WarMapNode>(FindObjectsSortMode.None);
            if (existingNodes != null && existingNodes.Length > 0)
            {
                Debug.Log($"[WarMapManager] Found {existingNodes.Length} existing nodes, using those instead of creating new ones");
                warMapNodes.AddRange(existingNodes);
                
                // Sort by NodeID to ensure consistent order
                warMapNodes.Sort((a, b) => a.NodeID.CompareTo(b.NodeID));
                return;
            }
            
            // Only create nodes if they don't already exist
            if (warMapNodePrefab == null)
            {
                Debug.LogWarning("[WarMapManager] No node prefab assigned and no existing nodes found. Cannot create nodes.");
                return;
            }
            
            if (nodeContainer == null)
            {
                Debug.LogWarning("[WarMapManager] No node container assigned. Cannot create nodes.");
                return;
            }
            
            foreach (var nodeConfig in mapConfig.NodeConfigs)
            {
                // Create node GameObject
                GameObject nodeGO = Instantiate(warMapNodePrefab, nodeContainer);
                nodeGO.name = $"WarMapNode_{nodeConfig.NodeName}";
                
                // Position the node
                RectTransform rectTransform = nodeGO.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.anchoredPosition = nodeConfig.Position;
                }
                
                // Initialize the node
                WarMapNode node = nodeGO.GetComponent<WarMapNode>();
                if (node != null)
                {
                    node.Initialize(
                        nodeConfig.NodeID,
                        nodeConfig.NodeName,
                        nodeConfig.NodeType,
                        nodeConfig.ConnectedNodes
                    );
                    
                    warMapNodes.Add(node);
                }
                
                // Spawn on network
                NetworkServer.Spawn(nodeGO);
            }
            
            Debug.Log($"[WarMapManager] Created {warMapNodes.Count} war map nodes");
        }
        
        /// <summary>
        /// Set initial faction control of nodes
        /// </summary>
        [Server]
        private void SetInitialFactionControl()
        {
            // Blue starts with node 0 (their capital)
            if (warMapNodes.Count > 0)
                warMapNodes[0].SetControl(Team.Blue, 100f);
            
            // Red starts with node 4 (their capital)
            if (warMapNodes.Count > 4)
                warMapNodes[4].SetControl(Team.Red, 100f);
            
            // Nodes 1, 2, 3 start neutral
            for (int i = 1; i <= 3 && i < warMapNodes.Count; i++)
            {
                warMapNodes[i].SetControl(Team.None, 0f);
            }
            
            Debug.Log("[WarMapManager] Initial faction control set");
        }
        
        #endregion
        
        #region War Management
        
        /// <summary>
        /// Start the real-time war (all factions active simultaneously)
        /// </summary>
        [Server]
        public void StartWar()
        {
            warActive = true;
            currentWarState = WarState.Strategic;
            
            Debug.Log("[WarMapManager] Real-time war started! All factions can act simultaneously.");
        }
        
        /// <summary>
        /// End the war with a winner
        /// </summary>
        [Server]
        private void EndWar(Team winner)
        {
            warActive = false;
            winningFaction = winner;
            currentWarState = WarState.Ended;
            
            Debug.Log($"[WarMapManager] WAR ENDED! Winner: {winner}");
            OnWarEnded?.Invoke(winner);
            
            // Clean up battles - copy keys to avoid collection modification during iteration
            var battleNodeIDs = new List<int>(activeBattles.Keys);
            foreach (int nodeID in battleNodeIDs)
            {
                EndBattle(nodeID, new BattleResult { WinnerFaction = winner });
            }
            activeBattles.Clear();
        }
        
        #endregion
        
        #region Battle Management
        
        /// <summary>
        /// Request to start a battle at a node
        /// </summary>
        [Command(requiresAuthority = false)]
        public void CmdRequestBattle(int nodeID, Team attackingFaction)
        {
            var node = GetNodeByID(nodeID);
            if (node == null)
            {
                Debug.LogWarning($"[WarMapManager] Node {nodeID} not found");
                return;
            }
            
            // Validate the attack
            if (!node.CanBeAttackedBy(attackingFaction))
            {
                Debug.LogWarning($"[WarMapManager] {attackingFaction} cannot attack node {nodeID}");
                return;
            }
            
            // Check if we've reached max simultaneous battles
            if (activeBattles.Count >= maxSimultaneousBattles)
            {
                Debug.LogWarning($"[WarMapManager] Maximum simultaneous battles reached ({maxSimultaneousBattles})");
                return;
            }
            
            // Check if there's already a battle at this node
            if (activeBattles.ContainsKey(nodeID))
            {
                Debug.LogWarning($"[WarMapManager] Battle already active at node {nodeID}");
                return;
            }
            
            // Check if faction can afford the attack
            if (TokenSystem.Instance == null || !TokenSystem.Instance.SpendTokens(attackingFaction, battleInitiationCost, $"Battle initiation at {node.NodeName}"))
            {
                Debug.LogWarning($"[WarMapManager] {attackingFaction} cannot afford to attack (needs {battleInitiationCost} tokens)");
                return;
            }
            
            // Create battle session
            StartBattle(node, attackingFaction);
        }
        
        /// <summary>
        /// Start a battle at a node
        /// </summary>
        [Server]
        private void StartBattle(WarMapNode node, Team attackingFaction)
        {
            // Create battle session
            var battleSession = new BattleSession
            {
                NodeID = node.NodeID,
                NodeName = node.NodeName,
                AttackingFaction = attackingFaction,
                DefendingFaction = node.ControllingFaction,
                StartTime = Time.time,
                IsActive = true
            };
            
            // Store the battle session
            activeBattles[node.NodeID] = battleSession;
            
            // Update node state
            node.StartBattle(attackingFaction);
            
            // Update war state
            if (currentWarState == WarState.Strategic)
            {
                currentWarState = WarState.Battle;
            }
            
            OnBattleStarted?.Invoke(battleSession);
            OnBattleRequested?.Invoke(node, attackingFaction);
            
            Debug.Log($"[WarMapManager] Battle started at {node.NodeName}: {attackingFaction} vs {node.ControllingFaction} ({activeBattles.Count} active battles)");
            
            // In a full implementation, this would trigger loading the FPS battle scene
            // For now, we'll simulate it
            RpcNotifyBattleStart(node.NodeID, attackingFaction);
        }
        
        /// <summary>
        /// End a battle with results
        /// </summary>
        [Server]
        public void EndBattle(int nodeID, BattleResult result)
        {
            if (!activeBattles.ContainsKey(nodeID))
            {
                Debug.LogWarning($"[WarMapManager] No active battle at node {nodeID}");
                return;
            }
            
            var battleSession = activeBattles[nodeID];
            var node = GetNodeByID(nodeID);
            
            if (node != null)
            {
                // Apply battle results to the node
                node.EndBattle(result);
                
                // Note: Tokens are NOT awarded for winning battles.
                // Tokens only come from holding territory (production cycles).
            }
            
            // Complete the battle session
            battleSession.IsActive = false;
            battleSession.EndTime = Time.time;
            battleSession.Result = result;
            
            OnBattleCompleted?.Invoke(battleSession, result);
            
            // Remove from active battles
            activeBattles.Remove(nodeID);
            
            // Update war state if no more battles
            if (activeBattles.Count == 0 && currentWarState == WarState.Battle)
            {
                currentWarState = WarState.Strategic;
            }
            
            Debug.Log($"[WarMapManager] Battle ended at node {nodeID}. Winner: {result.WinnerFaction} ({activeBattles.Count} battles remaining)");
        }
        
        /// <summary>
        /// Update active battle sessions
        /// </summary>
        [Server]
        private void UpdateBattleSessions()
        {
            var battlesToEnd = new List<int>();
            
            foreach (var kvp in activeBattles)
            {
                var battle = kvp.Value;
                
                // Check for timeout
                if (Time.time - battle.StartTime > battleTimeout)
                {
                    Debug.Log($"[WarMapManager] Battle at node {kvp.Key} timed out");
                    battlesToEnd.Add(kvp.Key);
                }
            }
            
            // End timed out battles (defender wins on timeout)
            foreach (int nodeID in battlesToEnd)
            {
                var battle = activeBattles[nodeID];
                EndBattle(nodeID, new BattleResult
                {
                    WinnerFaction = battle.DefendingFaction,
                    LoserFaction = battle.AttackingFaction,
                    ControlChange = 10f // Small control gain for defender
                });
            }
        }
        
        #endregion
        
        #region Victory Conditions
        
        /// <summary>
        /// Check if any faction has achieved victory
        /// </summary>
        [Server]
        private void CheckVictoryConditions()
        {
            if (!warActive)
                return;
            
            // Count nodes controlled by each faction
            Dictionary<Team, int> nodeControl = new Dictionary<Team, int>
            {
                { Team.Blue, 0 },
                { Team.Red, 0 },
                { Team.Green, 0 }
            };
            
            Dictionary<Team, float> totalControl = new Dictionary<Team, float>
            {
                { Team.Blue, 0f },
                { Team.Red, 0f },
                { Team.Green, 0f }
            };
            
            foreach (var node in warMapNodes)
            {
                if (node.ControllingFaction != Team.None)
                {
                    nodeControl[node.ControllingFaction]++;
                    totalControl[node.ControllingFaction] += node.ControlPercentage;
                }
            }
            
            // Check victory conditions for each faction
            foreach (var faction in new[] { Team.Blue, Team.Red, Team.Green })
            {
                // Victory by node control
                if (nodeControl[faction] >= nodesRequiredForVictory)
                {
                    float averageControl = totalControl[faction] / nodeControl[faction];
                    if (averageControl >= controlPercentageRequired)
                    {
                        EndWar(faction);
                        return;
                    }
                }
                
                // Victory by token accumulation
                if (TokenSystem.Instance != null && TokenSystem.Instance.GetFactionTokens(faction) >= tokensRequiredForVictory)
                {
                    EndWar(faction);
                    return;
                }
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Get a node by its ID
        /// </summary>
        public WarMapNode GetNodeByID(int nodeID)
        {
            return warMapNodes.FirstOrDefault(n => n.NodeID == nodeID);
        }
        
        /// <summary>
        /// Get all nodes controlled by a faction
        /// </summary>
        public List<WarMapNode> GetFactionNodes(Team faction)
        {
            return warMapNodes.Where(n => n.ControllingFaction == faction).ToList();
        }
        
        /// <summary>
        /// Get all nodes that can be attacked by a faction
        /// </summary>
        public List<WarMapNode> GetAttackableNodes(Team faction)
        {
            return warMapNodes.Where(n => n.CanBeAttackedBy(faction)).ToList();
        }
        
        /// <summary>
        /// Calculate faction strength (for AI or balancing)
        /// </summary>
        public float CalculateFactionStrength(Team faction)
        {
            float strength = 0f;
            
            // Factor in controlled nodes
            var factionNodes = GetFactionNodes(faction);
            strength += factionNodes.Count * 100f;
            
            // Factor in control percentages
            foreach (var node in factionNodes)
            {
                strength += node.ControlPercentage;
                
                // Bonus for special nodes
                switch (node.Type)
                {
                    case WarMapNode.NodeType.Capital:
                        strength += 50f;
                        break;
                    case WarMapNode.NodeType.Strategic:
                        strength += 30f;
                        break;
                    case WarMapNode.NodeType.Resource:
                        strength += 40f;
                        break;
                }
            }
            
            // Factor in tokens
            if (TokenSystem.Instance != null)
            {
                strength += TokenSystem.Instance.GetFactionTokens(faction) * 0.1f;
            }
            
            return strength;
        }
        
        #endregion
        
        #region Network Callbacks
        
        private void OnWarStateChanged(WarState oldState, WarState newState)
        {
            Debug.Log($"[WarMapManager] War state changed: {oldState} → {newState}");
            OnWarStateUpdated?.Invoke(newState);
        }
        
        [ClientRpc]
        private void RpcNotifyBattleStart(int nodeID, Team attackingFaction)
        {
            Debug.Log($"[WarMapManager-Client] Battle notification: Node {nodeID} under attack by {attackingFaction}");
            // In a full implementation, this would show UI notifications
        }
        
        #endregion
        
        #region Data Classes
        
        /// <summary>
        /// Configuration for the war map
        /// </summary>
        [Serializable]
        public class WarMapConfiguration
        {
            public List<NodeConfig> NodeConfigs = new List<NodeConfig>();
        }
        
        /// <summary>
        /// Configuration for a single node
        /// </summary>
        [Serializable]
        public class NodeConfig
        {
            public int NodeID;
            public string NodeName;
            public WarMapNode.NodeType NodeType;
            public Vector2 Position;
            public List<int> ConnectedNodes;
        }
        
        /// <summary>
        /// Represents an active battle session
        /// </summary>
        [Serializable]
        public class BattleSession
        {
            public int NodeID;
            public string NodeName;
            public Team AttackingFaction;
            public Team DefendingFaction;
            public float StartTime;
            public float EndTime;
            public bool IsActive;
            public BattleResult Result;
            public List<string> ParticipatingPlayers = new List<string>();
        }
        
        /// <summary>
        /// States of the war
        /// </summary>
        public enum WarState
        {
            Preparation,    // Setting up the war
            Strategic,      // Strategic phase - all factions can act
            Battle,         // Active battles happening
            Processing,     // Processing battle results
            Ended          // War is over
        }
        
        #endregion
    }
}
