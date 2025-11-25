using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;
using ElitesAndPawns.Core;
using ElitesAndPawns.Networking;

namespace ElitesAndPawns.WarMap
{
    /// <summary>
    /// Manages the UI for the War Map screen.
    /// Displays faction status, node information, and strategic controls.
    /// </summary>
    public class WarMapUI : MonoBehaviour
    {
        #region Fields
        
        [Header("Main Panels")]
        [SerializeField] private GameObject warMapPanel;
        [SerializeField] private GameObject factionInfoPanel;
        [SerializeField] private GameObject nodeDetailsPanel;
        [SerializeField] private GameObject battlePanel;
        
        [Header("Faction Display")]
        [SerializeField] private Text blueTokensText;
        [SerializeField] private Text redTokensText;
        [SerializeField] private Text greenTokensText;
        [SerializeField] private Text currentTurnText;
        [SerializeField] private Text turnNumberText;
        
        [Header("Node Details")]
        [SerializeField] private Text selectedNodeName;
        [SerializeField] private Text selectedNodeType;
        [SerializeField] private Text selectedNodeControl;
        [SerializeField] private Image selectedNodeControlBar;
        [SerializeField] private Text selectedNodeTokenGeneration;
        [SerializeField] private Button attackButton;
        [SerializeField] private Button fortifyButton;
        [SerializeField] private Button reinforceButton;
        
        [Header("Battle Status")]
        [SerializeField] private GameObject battleStatusContainer;
        [SerializeField] private Text battleLocationText;
        [SerializeField] private Text battleFactionsText;
        [SerializeField] private Text battleTimerText;
        [SerializeField] private Button joinBattleButton;
        
        [Header("Victory Display")]
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private Text victoryText;
        
        [Header("Connection Lines")]
        [SerializeField] private LineRenderer connectionLinePrefab;
        [SerializeField] private Transform connectionContainer;
        
        [Header("Settings")]
        [SerializeField] private Color blueColor = Color.blue;
        [SerializeField] private Color redColor = Color.red;
        [SerializeField] private Color greenColor = Color.green;
        [SerializeField] private Color neutralColor = Color.gray;
        
        private WarMapNode selectedNode;
        private Team playerFaction = Team.Blue; // Will be set based on player
        private List<LineRenderer> connectionLines = new List<LineRenderer>();
        private Dictionary<int, WarMapManager.BattleSession> activeBattles = new Dictionary<int, WarMapManager.BattleSession>();
        
        #endregion
        
        #region Unity Lifecycle
        
        void Start()
        {
            InitializeUI();
            SubscribeToEvents();
            RefreshDisplay();
        }
        
        void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        
        void Update()
        {
            UpdateBattleTimers();
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeUI()
        {
            // Hide panels initially
            if (nodeDetailsPanel != null)
                nodeDetailsPanel.SetActive(false);
                
            if (battlePanel != null)
                battlePanel.SetActive(false);
                
            if (victoryPanel != null)
                victoryPanel.SetActive(false);
            
            // Set up buttons
            if (attackButton != null)
                attackButton.onClick.AddListener(OnAttackButtonClicked);
                
            if (fortifyButton != null)
                fortifyButton.onClick.AddListener(OnFortifyButtonClicked);
                
            if (reinforceButton != null)
                reinforceButton.onClick.AddListener(OnReinforceButtonClicked);
                
            if (joinBattleButton != null)
                joinBattleButton.onClick.AddListener(OnJoinBattleClicked);
            
            // Determine player faction (in a real game, this would come from player selection)
            DeterminePlayerFaction();
            
            // Draw connection lines between nodes
            DrawNodeConnections();
        }
        
        private void SubscribeToEvents()
        {
            // Token events
            TokenSystem.OnTokensChanged += OnTokensChanged;
            TokenSystem.OnTokenCycleCompleted += OnTokenCycleCompleted;
            
            // War map events
            WarMapManager.OnWarStateUpdated += OnWarStateUpdated;
            WarMapManager.OnBattleStarted += OnBattleStarted;
            WarMapManager.OnBattleCompleted += OnBattleCompleted;
            WarMapManager.OnWarEnded += OnWarEnded;
            
            // Node events
            WarMapNode.OnNodeCaptured += OnNodeCaptured;
            WarMapNode.OnNodeContested += OnNodeContested;
        }
        
        private void UnsubscribeFromEvents()
        {
            // Token events
            TokenSystem.OnTokensChanged -= OnTokensChanged;
            TokenSystem.OnTokenCycleCompleted -= OnTokenCycleCompleted;
            
            // War map events
            WarMapManager.OnWarStateUpdated -= OnWarStateUpdated;
            WarMapManager.OnBattleStarted -= OnBattleStarted;
            WarMapManager.OnBattleCompleted -= OnBattleCompleted;
            WarMapManager.OnWarEnded -= OnWarEnded;
            
            // Node events
            WarMapNode.OnNodeCaptured -= OnNodeCaptured;
            WarMapNode.OnNodeContested -= OnNodeContested;
        }
        
        #endregion
        
        #region Player Faction
        
        private void DeterminePlayerFaction()
        {
            // In a real implementation, this would be determined by:
            // 1. Player selection in lobby
            // 2. Team assignment from server
            // 3. Persistent player data
            
            // For now, determine based on network authority or default to Blue
            if (NetworkClient.active)
            {
                // Try to get from NetworkManager if it exists
                var networkManager = FindAnyObjectByType<ElitesNetworkManager>();
                if (networkManager != null)
                {
                    // This would need proper implementation in NetworkPlayer
                    playerFaction = Team.Blue; // Default for now
                }
            }
            
            Debug.Log($"[WarMapUI] Player faction set to: {playerFaction}");
        }
        
        #endregion
        
        #region Display Updates
        
        private void RefreshDisplay()
        {
            UpdateTokenDisplay();
            UpdateWarStateDisplay();
            UpdateNodeDetails();
            UpdateBattlePanel();
        }
        
        private void UpdateTokenDisplay()
        {
            if (TokenSystem.Instance == null)
                return;
            
            if (blueTokensText != null)
                blueTokensText.text = $"Blue: {TokenSystem.Instance.GetFactionTokens(Team.Blue)} Tokens";
                
            if (redTokensText != null)
                redTokensText.text = $"Red: {TokenSystem.Instance.GetFactionTokens(Team.Red)} Tokens";
                
            if (greenTokensText != null)
                greenTokensText.text = $"Green: {TokenSystem.Instance.GetFactionTokens(Team.Green)} Tokens";
        }
        
        private void UpdateWarStateDisplay()
        {
            if (WarMapManager.Instance == null)
                return;
            
            // Display real-time war state instead of turns
            if (currentTurnText != null)
            {
                currentTurnText.text = $"War State: {WarMapManager.Instance.CurrentState}";
                currentTurnText.color = Color.white;
            }
            
            if (turnNumberText != null)
            {
                int activeBattles = WarMapManager.Instance.ActiveBattleCount;
                turnNumberText.text = $"Active Battles: {activeBattles}";
            }
        }
        
        private void UpdateNodeDetails()
        {
            if (selectedNode == null)
            {
                if (nodeDetailsPanel != null)
                    nodeDetailsPanel.SetActive(false);
                return;
            }
            
            if (nodeDetailsPanel != null)
                nodeDetailsPanel.SetActive(true);
            
            // Update node information
            if (selectedNodeName != null)
                selectedNodeName.text = selectedNode.NodeName;
                
            if (selectedNodeType != null)
                selectedNodeType.text = $"Type: {selectedNode.Type}";
                
            if (selectedNodeControl != null)
            {
                selectedNodeControl.text = $"{selectedNode.ControllingFaction}: {selectedNode.ControlPercentage:F0}%";
                selectedNodeControl.color = GetFactionColor(selectedNode.ControllingFaction);
            }
            
            if (selectedNodeControlBar != null)
            {
                selectedNodeControlBar.fillAmount = selectedNode.ControlPercentage / 100f;
                selectedNodeControlBar.color = GetFactionColor(selectedNode.ControllingFaction);
            }
            
            if (selectedNodeTokenGeneration != null)
                selectedNodeTokenGeneration.text = $"Generates: {selectedNode.CalculateTokenGeneration()} tokens/cycle";
            
            // Update action buttons
            UpdateActionButtons();
        }
        
        private void UpdateActionButtons()
        {
            if (selectedNode == null || WarMapManager.Instance == null)
                return;
            
            bool isPlayerTurn = true; // Real-time: all factions can always act
            bool canAttack = selectedNode.CanBeAttackedBy(playerFaction) && isPlayerTurn;
            bool canFortify = selectedNode.ControllingFaction == playerFaction && isPlayerTurn;
            bool canReinforce = selectedNode.IsBattleActive && 
                                (selectedNode.ControllingFaction == playerFaction || 
                                 selectedNode.IsAdjacentToFaction(playerFaction));
            
            if (attackButton != null)
            {
                attackButton.interactable = canAttack;
                attackButton.GetComponentInChildren<Text>().text = $"Attack (100 Tokens)";
            }
            
            if (fortifyButton != null)
            {
                fortifyButton.interactable = canFortify;
                fortifyButton.GetComponentInChildren<Text>().text = $"Fortify (75 Tokens)";
            }
            
            if (reinforceButton != null)
            {
                reinforceButton.interactable = canReinforce;
                reinforceButton.GetComponentInChildren<Text>().text = $"Reinforce (50 Tokens)";
            }
        }
        
        private void UpdateBattlePanel()
        {
            bool hasBattles = activeBattles.Count > 0;
            
            if (battlePanel != null)
                battlePanel.SetActive(hasBattles);
            
            if (hasBattles && battleStatusContainer != null)
            {
                // Show first active battle (in full implementation, allow switching between battles)
                foreach (var battle in activeBattles.Values)
                {
                    if (battleLocationText != null)
                        battleLocationText.text = $"Battle at: {battle.NodeName}";
                        
                    if (battleFactionsText != null)
                        battleFactionsText.text = $"{battle.AttackingFaction} vs {battle.DefendingFaction}";
                    
                    // Show join button if player's faction is involved
                    bool canJoin = (battle.AttackingFaction == playerFaction || 
                                   battle.DefendingFaction == playerFaction);
                    
                    if (joinBattleButton != null)
                        joinBattleButton.interactable = canJoin;
                    
                    break; // Show only first battle for now
                }
            }
        }
        
        private void UpdateBattleTimers()
        {
            if (battleTimerText == null || activeBattles.Count == 0)
                return;
            
            // Update timer for active battles
            foreach (var battle in activeBattles.Values)
            {
                if (battle.IsActive)
                {
                    float elapsed = Time.time - battle.StartTime;
                    int minutes = Mathf.FloorToInt(elapsed / 60f);
                    int seconds = Mathf.FloorToInt(elapsed % 60f);
                    battleTimerText.text = $"Time: {minutes:00}:{seconds:00}";
                    break; // Show only first battle timer
                }
            }
        }
        
        #endregion
        
        #region Node Selection
        
        public void SelectNode(WarMapNode node)
        {
            selectedNode = node;
            UpdateNodeDetails();
            
            // Visual feedback - highlight selected node
            HighlightSelectedNode();
        }
        
        private void HighlightSelectedNode()
        {
            // Reset all node highlights
            foreach (var node in WarMapManager.Instance.Nodes)
            {
                // Add visual highlight logic here
                // For example, add an outline or scale effect
            }
            
            // Highlight selected node
            if (selectedNode != null)
            {
                // Add highlight effect to selected node
            }
        }
        
        #endregion
        
        #region Button Handlers
        
        private void OnAttackButtonClicked()
        {
            if (selectedNode == null || !selectedNode.CanBeAttackedBy(playerFaction))
                return;
            
            // Request attack through network
            if (NetworkClient.active && WarMapManager.Instance != null)
            {
                WarMapManager.Instance.CmdRequestBattle(selectedNode.NodeID, playerFaction);
            }
        }
        
        private void OnFortifyButtonClicked()
        {
            if (selectedNode == null || selectedNode.ControllingFaction != playerFaction)
                return;
            
            // Request fortification through network
            if (NetworkClient.active && TokenSystem.Instance != null)
            {
                // This would need a Command method in TokenSystem
                Debug.Log($"[WarMapUI] Requesting fortification of {selectedNode.NodeName}");
            }
        }
        
        private void OnReinforceButtonClicked()
        {
            if (selectedNode == null || !selectedNode.IsBattleActive)
                return;
            
            // Request reinforcements through network
            if (NetworkClient.active && TokenSystem.Instance != null)
            {
                // This would need a Command method in TokenSystem
                Debug.Log($"[WarMapUI] Requesting reinforcements for {selectedNode.NodeName}");
            }
        }
        
        private void OnJoinBattleClicked()
        {
            // Load the FPS battle scene
            // In a full implementation, this would:
            // 1. Save current war map state
            // 2. Load the battle scene with proper parameters
            // 3. Connect to the battle server
            
            Debug.Log("[WarMapUI] Joining battle...");
            
            // For now, just load the battle scene
            if (NetworkClient.active)
            {
                // This would need proper scene management
                UnityEngine.SceneManagement.SceneManager.LoadScene("NetworkTest");
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void OnTokensChanged(Team faction, int newAmount)
        {
            UpdateTokenDisplay();
        }
        
        private void OnTokenCycleCompleted()
        {
            RefreshDisplay();
            
            // Show notification
            Debug.Log("[WarMapUI] Token generation cycle completed");
        }
        
        private void OnWarStateUpdated(WarMapManager.WarState newState)
        {
            Debug.Log($"[WarMapUI] War state updated to: {newState}");
            RefreshDisplay();
        }
        
        private void OnBattleStarted(WarMapManager.BattleSession battle)
        {
            activeBattles[battle.NodeID] = battle;
            UpdateBattlePanel();
        }
        
        private void OnBattleCompleted(WarMapManager.BattleSession battle, BattleResult result)
        {
            if (activeBattles.ContainsKey(battle.NodeID))
                activeBattles.Remove(battle.NodeID);
                
            UpdateBattlePanel();
            RefreshDisplay();
            
            // Show battle results notification
            Debug.Log($"[WarMapUI] Battle completed at {battle.NodeName}. Winner: {result.WinnerFaction}");
        }
        
        private void OnWarEnded(Team winner)
        {
            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
                
                if (victoryText != null)
                {
                    if (winner == playerFaction)
                        victoryText.text = "VICTORY!\nYour faction has won the war!";
                    else
                        victoryText.text = $"DEFEAT\n{winner} faction has won the war.";
                        
                    victoryText.color = GetFactionColor(winner);
                }
            }
        }
        
        private void OnNodeCaptured(WarMapNode node, Team faction)
        {
            Debug.Log($"[WarMapUI] {node.NodeName} captured by {faction}");
            RefreshDisplay();
        }
        
        private void OnNodeContested(WarMapNode node, Team attackingFaction)
        {
            Debug.Log($"[WarMapUI] {node.NodeName} contested by {attackingFaction}");
            RefreshDisplay();
        }
        
        #endregion
        
        #region Visual Helpers
        
        private void DrawNodeConnections()
        {
            // Clear existing lines
            foreach (var line in connectionLines)
            {
                if (line != null)
                    Destroy(line.gameObject);
            }
            connectionLines.Clear();
            
            if (WarMapManager.Instance == null || connectionLinePrefab == null)
                return;
            
            // Draw lines between connected nodes
            HashSet<string> drawnConnections = new HashSet<string>();
            
            foreach (var node in WarMapManager.Instance.Nodes)
            {
                foreach (var connectedNode in node.ConnectedNodes)
                {
                    // Create unique key for this connection
                    string connectionKey = $"{Mathf.Min(node.NodeID, connectedNode.NodeID)}-{Mathf.Max(node.NodeID, connectedNode.NodeID)}";
                    
                    // Skip if already drawn
                    if (drawnConnections.Contains(connectionKey))
                        continue;
                        
                    drawnConnections.Add(connectionKey);
                    
                    // Create line renderer
                    LineRenderer line = Instantiate(connectionLinePrefab, connectionContainer);
                    line.SetPosition(0, node.transform.position);
                    line.SetPosition(1, connectedNode.transform.position);
                    
                    connectionLines.Add(line);
                }
            }
        }
        
        private Color GetFactionColor(Team faction)
        {
            switch (faction)
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
    }
}
