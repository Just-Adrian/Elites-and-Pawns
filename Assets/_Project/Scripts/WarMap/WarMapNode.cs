using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ElitesAndPawns.Core;

namespace ElitesAndPawns.WarMap
{
    /// <summary>
    /// Represents a single strategic node on the war map.
    /// Each node can be captured and controlled by a faction.
    /// </summary>
    [Serializable]
    public class WarMapNode : MonoBehaviour
    {
        #region Fields
        
        [Header("Node Configuration")]
        [SerializeField] private int nodeID;
        [SerializeField] private string nodeName = "Territory";
        [SerializeField] private NodeType nodeType = NodeType.Standard;
        [SerializeField] private int baseTokenGeneration = 10;
        
        [Header("Current State")]
        [SerializeField] private Team controllingFaction = Team.None;
        [SerializeField] private float controlPercentage = 0f;
        [SerializeField] private bool isContested = false;
        [SerializeField] private bool isBattleActive = false;
        
        [Header("Strategic Value")]
        [SerializeField] private int attackBonus = 0;
        [SerializeField] private int defenseBonus = 0;
        [SerializeField] private float tokenMultiplier = 1f;
        
        [Header("Connected Nodes")]
        [SerializeField] private List<int> connectedNodeIDs = new List<int>();
        private List<WarMapNode> connectedNodes = new List<WarMapNode>();
        
        [Header("UI References")]
        [SerializeField] private Image nodeIcon;
        [SerializeField] private Image controlBar;
        [SerializeField] private Text nodeNameText;
        [SerializeField] private Text controlPercentageText;
        [SerializeField] private GameObject contestedIndicator;
        [SerializeField] private GameObject battleIndicator;
        
        [Header("Visual Settings")]
        [SerializeField] private Color neutralColor = Color.gray;
        [SerializeField] private Color blueColor = Color.blue;
        [SerializeField] private Color redColor = Color.red;
        [SerializeField] private Color greenColor = Color.green;
        [SerializeField] private Color contestedColor = Color.yellow;
        
        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;
        
        #endregion
        
        #region Properties
        
        public int NodeID => nodeID;
        public string NodeName => nodeName;
        public NodeType Type => nodeType;
        public Team ControllingFaction => controllingFaction;
        public float ControlPercentage => controlPercentage;
        public bool IsContested => isContested;
        public bool IsBattleActive => isBattleActive;
        public int BaseTokenGeneration => baseTokenGeneration;
        public float TokenMultiplier => tokenMultiplier;
        public List<WarMapNode> ConnectedNodes => connectedNodes;
        public List<int> ConnectedNodeIDs => connectedNodeIDs;
        
        /// <summary>
        /// Attack bonus provided by this node (for future FPS integration).
        /// </summary>
        public int AttackBonus => attackBonus;
        
        /// <summary>
        /// Defense bonus provided by this node (for future FPS integration).
        /// </summary>
        public int DefenseBonus => defenseBonus;
        
        /// <summary>
        /// Calculate the actual token generation for this node.
        /// Returns 0 if contested, in battle, or uncontrolled.
        /// </summary>
        public int CalculateTokenGeneration()
        {
            if (controllingFaction == Team.None || isContested || isBattleActive)
                return 0;
                
            return Mathf.RoundToInt(baseTokenGeneration * tokenMultiplier * (controlPercentage / 100f));
        }
        
        /// <summary>
        /// Check if a faction can attack this node (optimized).
        /// Requirements: not own node, no active battle, and attacker controls an adjacent node.
        /// </summary>
        public bool CanBeAttackedBy(Team attackingFaction)
        {
            // Quick rejections first (no allocations, simple checks)
            if (attackingFaction == Team.None)
                return false;
            
            if (controllingFaction == attackingFaction)
                return false;
            
            if (isBattleActive)
                return false;
            
            // Check adjacency - early return on first match
            for (int i = 0; i < connectedNodes.Count; i++)
            {
                if (connectedNodes[i].controllingFaction == attackingFaction)
                    return true;
            }
            
            if (verboseLogging)
            {
                Debug.Log($"[Node {nodeName}] {attackingFaction} cannot attack - no adjacent controlled nodes");
            }
            
            return false;
        }
        
        /// <summary>
        /// Check if a faction can attack this node, with detailed reason output.
        /// Use this for UI feedback instead of CanBeAttackedBy for better performance.
        /// </summary>
        public bool CanBeAttackedBy(Team attackingFaction, out string reason)
        {
            reason = "";
            
            if (attackingFaction == Team.None)
            {
                reason = "Invalid faction";
                return false;
            }
            
            if (controllingFaction == attackingFaction)
            {
                reason = "Already controlled by your faction";
                return false;
            }
            
            if (isBattleActive)
            {
                reason = "Battle already in progress";
                return false;
            }
            
            // Check adjacency
            for (int i = 0; i < connectedNodes.Count; i++)
            {
                if (connectedNodes[i].controllingFaction == attackingFaction)
                    return true;
            }
            
            reason = "No adjacent controlled territory";
            return false;
        }
        
        #endregion
        
        #region Events
        
        public static event Action<WarMapNode, Team> OnNodeCaptured;
        public static event Action<WarMapNode, Team> OnNodeContested;
        public static event Action<WarMapNode> OnBattleStarted;
        public static event Action<WarMapNode, BattleResult> OnBattleEnded;
        
        #endregion
        
        #region Unity Lifecycle
        
        void Awake()
        {
            InitializeUI();
        }
        
        void Start()
        {
            ConnectToNodes();
            UpdateVisuals();
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Initialize this node with configuration data.
        /// </summary>
        public void Initialize(int id, string name, NodeType type, List<int> connections)
        {
            nodeID = id;
            nodeName = name;
            nodeType = type;
            connectedNodeIDs = new List<int>(connections);
            
            // Set strategic values based on node type
            switch (nodeType)
            {
                case NodeType.Capital:
                    baseTokenGeneration = 20;
                    attackBonus = 0;
                    defenseBonus = 10;
                    tokenMultiplier = 1.5f;
                    break;
                case NodeType.Strategic:
                    baseTokenGeneration = 15;
                    attackBonus = 5;
                    defenseBonus = 5;
                    tokenMultiplier = 1.25f;
                    break;
                case NodeType.Resource:
                    baseTokenGeneration = 25;
                    attackBonus = 0;
                    defenseBonus = 0;
                    tokenMultiplier = 2f;
                    break;
                case NodeType.Standard:
                default:
                    baseTokenGeneration = 10;
                    attackBonus = 0;
                    defenseBonus = 0;
                    tokenMultiplier = 1f;
                    break;
            }
            
            UpdateVisuals();
        }
        
        /// <summary>
        /// Set the control state of this node.
        /// </summary>
        public void SetControl(Team faction, float percentage)
        {
            Team previousFaction = controllingFaction;
            controllingFaction = faction;
            controlPercentage = Mathf.Clamp(percentage, 0f, 100f);
            
            if (controlPercentage >= 100f)
            {
                isContested = false;
            }
            
            if (previousFaction != faction && percentage >= 100f)
            {
                OnNodeCaptured?.Invoke(this, faction);
            }
            
            UpdateVisuals();
        }
        
        /// <summary>
        /// Mark this node as contested.
        /// </summary>
        public void SetContested(bool contested, Team attackingFaction = Team.None)
        {
            isContested = contested;
            
            if (contested)
            {
                OnNodeContested?.Invoke(this, attackingFaction);
            }
            
            UpdateVisuals();
        }
        
        /// <summary>
        /// Start a battle at this node.
        /// </summary>
        public void StartBattle(Team attackingFaction)
        {
            if (!CanBeAttackedBy(attackingFaction))
            {
                Debug.LogWarning($"[WarMapNode] {nodeName} cannot be attacked by {attackingFaction}");
                return;
            }
            
            isBattleActive = true;
            SetContested(true, attackingFaction);
            OnBattleStarted?.Invoke(this);
            
            UpdateVisuals();
        }
        
        /// <summary>
        /// End the battle at this node with the given result.
        /// </summary>
        public void EndBattle(BattleResult result)
        {
            isBattleActive = false;
            
            if (result.WinnerFaction != Team.None)
            {
                if (result.WinnerFaction != controllingFaction)
                {
                    // Attacker won
                    float newControl = Mathf.Max(0f, controlPercentage - result.ControlChange);
                    
                    if (newControl <= 0f)
                    {
                        SetControl(result.WinnerFaction, result.ControlChange);
                        SetContested(false);
                    }
                    else
                    {
                        SetControl(controllingFaction, newControl);
                        SetContested(true, result.WinnerFaction);
                    }
                }
                else
                {
                    // Defender won
                    float newControl = Mathf.Min(100f, controlPercentage + result.ControlChange);
                    SetControl(controllingFaction, newControl);
                    
                    if (newControl >= 100f)
                    {
                        SetContested(false);
                    }
                }
            }
            
            OnBattleEnded?.Invoke(this, result);
            UpdateVisuals();
        }
        
        /// <summary>
        /// Check if this node connects to another node.
        /// </summary>
        public bool IsConnectedTo(WarMapNode otherNode)
        {
            if (otherNode == null) return false;
            return connectedNodes.Contains(otherNode);
        }
        
        /// <summary>
        /// Check if this node connects to a node controlled by the given faction.
        /// </summary>
        public bool IsAdjacentToFaction(Team faction)
        {
            for (int i = 0; i < connectedNodes.Count; i++)
            {
                if (connectedNodes[i].controllingFaction == faction)
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// Force reconnection to other nodes. Call after dynamic node creation.
        /// </summary>
        public void RefreshConnections()
        {
            ConnectToNodes();
        }
        
        #endregion
        
        #region Private Methods
        
        /// <summary>
        /// Connect this node to other nodes in the scene based on connectedNodeIDs.
        /// </summary>
        private void ConnectToNodes()
        {
            connectedNodes.Clear();
            
            if (connectedNodeIDs.Count == 0)
                return;
            
            // Build a lookup dictionary for O(n) instead of O(n*m)
            WarMapNode[] allNodes = FindObjectsByType<WarMapNode>(FindObjectsSortMode.None);
            var nodeById = new Dictionary<int, WarMapNode>(allNodes.Length);
            
            foreach (var node in allNodes)
            {
                if (node != this && !nodeById.ContainsKey(node.nodeID))
                {
                    nodeById[node.nodeID] = node;
                }
            }
            
            // Connect using lookup
            foreach (int connectedID in connectedNodeIDs)
            {
                if (nodeById.TryGetValue(connectedID, out var connectedNode))
                {
                    connectedNodes.Add(connectedNode);
                }
            }
            
            if (verboseLogging)
            {
                Debug.Log($"[WarMapNode] {nodeName} connected to {connectedNodes.Count} nodes");
            }
        }
        
        private void InitializeUI()
        {
            if (nodeNameText != null)
                nodeNameText.text = nodeName;
                
            if (contestedIndicator != null)
                contestedIndicator.SetActive(false);
                
            if (battleIndicator != null)
                battleIndicator.SetActive(false);
        }
        
        private void UpdateVisuals()
        {
            Color targetColor = GetFactionColor();
            
            if (nodeIcon != null)
            {
                nodeIcon.color = isContested ? contestedColor : targetColor;
            }
            
            if (controlBar != null)
            {
                controlBar.fillAmount = controlPercentage / 100f;
                controlBar.color = targetColor;
            }
            
            if (nodeNameText != null)
                nodeNameText.text = $"{nodeName} ({nodeType})";
                
            if (controlPercentageText != null)
                controlPercentageText.text = $"{controlPercentage:F0}%";
            
            if (contestedIndicator != null)
                contestedIndicator.SetActive(isContested);
                
            if (battleIndicator != null)
                battleIndicator.SetActive(isBattleActive);
            
            // Update 3D renderer for test spheres
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = isContested ? contestedColor : targetColor;
            }
        }
        
        private Color GetFactionColor()
        {
            return controllingFaction switch
            {
                Team.Blue => blueColor,
                Team.Red => redColor,
                Team.Green => greenColor,
                _ => neutralColor
            };
        }
        
        #endregion
        
        #region Node Types
        
        public enum NodeType
        {
            Standard,
            Capital,
            Strategic,
            Resource
        }
        
        #endregion
    }
    
    /// <summary>
    /// Represents the result of a battle at a node.
    /// </summary>
    [Serializable]
    public class BattleResult
    {
        public Team WinnerFaction;
        public Team LoserFaction;
        public float ControlChange;
        public int TokensWon;
        public int TokensLost;
        public int PlayersParticipated;
        public float BattleDuration;
        public Dictionary<string, int> PlayerScores;
        
        public BattleResult()
        {
            PlayerScores = new Dictionary<string, int>();
        }
    }
}
