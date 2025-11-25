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
        [SerializeField] private int baseTokenGeneration = 10; // Tokens generated per cycle
        
        [Header("Current State")]
        [SerializeField] private Team controllingFaction = Team.None;
        [SerializeField] private float controlPercentage = 0f; // 0-100%
        [SerializeField] private bool isContested = false;
        [SerializeField] private bool isBattleActive = false;
        
        [Header("Strategic Value")]
        #pragma warning disable 0414 // Assigned but never used - planned for combat calculations
        [SerializeField] private int attackBonus = 0; // Bonus to attack rolls
        [SerializeField] private int defenseBonus = 0; // Bonus to defense
        #pragma warning restore 0414
        [SerializeField] private float tokenMultiplier = 1f; // Multiplier for token generation
        
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
        
        /// <summary>
        /// Calculate the actual token generation for this node
        /// </summary>
        public int CalculateTokenGeneration()
        {
            if (controllingFaction == Team.None || isContested || isBattleActive)
                return 0;
                
            return Mathf.RoundToInt(baseTokenGeneration * tokenMultiplier * (controlPercentage / 100f));
        }
        
        /// <summary>
        /// Check if a faction can attack this node
        /// </summary>
        public bool CanBeAttackedBy(Team attackingFaction)
        {
            // Can't attack your own node
            if (controllingFaction == attackingFaction)
            {
                Debug.Log($"[Node {nodeName}] Cannot attack - {attackingFaction} already controls this node");
                return false;
            }
                
            // Can't attack if battle is already active
            if (isBattleActive)
            {
                Debug.Log($"[Node {nodeName}] Cannot attack - battle already active");
                return false;
            }
                
            // Check if attacker controls any connected node
            Debug.Log($"[Node {nodeName}] Checking {connectedNodes.Count} connected nodes for {attackingFaction} control");
            foreach (var connectedNode in connectedNodes)
            {
                Debug.Log($"[Node {nodeName}] Connected node {connectedNode.NodeName} controlled by {connectedNode.controllingFaction}");
                if (connectedNode.controllingFaction == attackingFaction)
                {
                    Debug.Log($"[Node {nodeName}] CAN attack - {attackingFaction} controls connected node {connectedNode.NodeName}");
                    return true;
                }
            }
            
            Debug.Log($"[Node {nodeName}] Cannot attack - {attackingFaction} controls no connected nodes");
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
            // Connect to other nodes after all nodes are spawned
            ConnectToNodes();
            UpdateVisuals();
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Initialize this node with data
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
                    tokenMultiplier = 2f;
                    break;
                case NodeType.Standard:
                default:
                    baseTokenGeneration = 10;
                    tokenMultiplier = 1f;
                    break;
            }
            
            UpdateVisuals();
        }
        
        /// <summary>
        /// Set the control state of this node
        /// </summary>
        public void SetControl(Team faction, float percentage)
        {
            Team previousFaction = controllingFaction;
            controllingFaction = faction;
            controlPercentage = Mathf.Clamp(percentage, 0f, 100f);
            
            // If control is 100%, the node is no longer contested
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
        /// Mark this node as contested
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
        /// Start a battle at this node
        /// </summary>
        public void StartBattle(Team attackingFaction)
        {
            if (!CanBeAttackedBy(attackingFaction))
            {
                Debug.LogWarning($"Node {nodeName} cannot be attacked by {attackingFaction}!");
                return;
            }
            
            isBattleActive = true;
            SetContested(true, attackingFaction);
            OnBattleStarted?.Invoke(this);
            
            UpdateVisuals();
        }
        
        /// <summary>
        /// End the battle at this node with the given result
        /// </summary>
        public void EndBattle(BattleResult result)
        {
            isBattleActive = false;
            
            // Apply battle result
            if (result.WinnerFaction != Team.None)
            {
                if (result.WinnerFaction != controllingFaction)
                {
                    // Attacker won - change control
                    float newControl = Mathf.Max(0f, controlPercentage - result.ControlChange);
                    
                    if (newControl <= 0f)
                    {
                        // Complete takeover - node is no longer contested
                        SetControl(result.WinnerFaction, result.ControlChange);
                        SetContested(false);
                    }
                    else
                    {
                        // Partial control loss - still contested
                        SetControl(controllingFaction, newControl);
                        SetContested(true, result.WinnerFaction);
                    }
                }
                else
                {
                    // Defender won - strengthen control
                    float newControl = Mathf.Min(100f, controlPercentage + result.ControlChange);
                    SetControl(controllingFaction, newControl);
                    
                    // If defender reached 100%, no longer contested
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
        /// Check if this node connects to another node
        /// </summary>
        public bool IsConnectedTo(WarMapNode otherNode)
        {
            return connectedNodes.Contains(otherNode);
        }
        
        /// <summary>
        /// Check if this node connects to a node controlled by the given faction
        /// </summary>
        public bool IsAdjacentToFaction(Team faction)
        {
            foreach (var node in connectedNodes)
            {
                if (node.controllingFaction == faction)
                    return true;
            }
            return false;
        }
        
        #endregion
        
        #region Private Methods
        
        /// <summary>
        /// Connect this node to other nodes in the scene
        /// </summary>
        private void ConnectToNodes()
        {
            connectedNodes.Clear();
            WarMapNode[] allNodes = FindObjectsByType<WarMapNode>(FindObjectsSortMode.None);
            
            foreach (int connectedID in connectedNodeIDs)
            {
                foreach (var node in allNodes)
                {
                    if (node.nodeID == connectedID && node != this)
                    {
                        connectedNodes.Add(node);
                        break;
                    }
                }
            }
        }
        
        /// <summary>
        /// Initialize UI components
        /// </summary>
        private void InitializeUI()
        {
            if (nodeNameText != null)
                nodeNameText.text = nodeName;
                
            if (contestedIndicator != null)
                contestedIndicator.SetActive(false);
                
            if (battleIndicator != null)
                battleIndicator.SetActive(false);
        }
        
        /// <summary>
        /// Update the visual representation of the node
        /// </summary>
        private void UpdateVisuals()
        {
            // Update node color based on faction
            Color targetColor = GetFactionColor();
            
            if (nodeIcon != null)
            {
                if (isContested)
                    nodeIcon.color = contestedColor;
                else
                    nodeIcon.color = targetColor;
            }
            
            // Update control bar
            if (controlBar != null)
            {
                controlBar.fillAmount = controlPercentage / 100f;
                controlBar.color = targetColor;
            }
            
            // Update text displays
            if (nodeNameText != null)
                nodeNameText.text = $"{nodeName} ({nodeType})";
                
            if (controlPercentageText != null)
                controlPercentageText.text = $"{controlPercentage:F0}%";
            
            // Update indicators
            if (contestedIndicator != null)
                contestedIndicator.SetActive(isContested);
                
            if (battleIndicator != null)
                battleIndicator.SetActive(isBattleActive);
            
            // CRITICAL: Also update Renderer for test sphere nodes
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                if (isContested)
                    renderer.material.color = contestedColor;
                else
                    renderer.material.color = targetColor;
            }
        }
        
        /// <summary>
        /// Get the color for the current controlling faction
        /// </summary>
        private Color GetFactionColor()
        {
            switch (controllingFaction)
            {
                case Team.Blue:
                    return blueColor;
                case Team.Red:
                    return redColor;
                case Team.Green:
                    return greenColor;
                default:
                    return neutralColor;
            }
        }
        
        #endregion
        
        #region Node Types
        
        public enum NodeType
        {
            Standard,   // Normal territory
            Capital,    // Faction starting point, high defense
            Strategic,  // Provides combat bonuses
            Resource    // High token generation
        }
        
        #endregion
    }
    
    /// <summary>
    /// Represents the result of a battle
    /// </summary>
    [Serializable]
    public class BattleResult
    {
        public Team WinnerFaction;
        public Team LoserFaction;
        public float ControlChange; // How much control percentage changes
        public int TokensWon;
        public int TokensLost;
        public int PlayersParticipated;
        public float BattleDuration; // In seconds
        public Dictionary<string, int> PlayerScores; // Player contributions
        
        public BattleResult()
        {
            PlayerScores = new Dictionary<string, int>();
        }
    }
}
