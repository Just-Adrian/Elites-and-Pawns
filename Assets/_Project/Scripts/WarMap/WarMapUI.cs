using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;
using ElitesAndPawns.Core;

namespace ElitesAndPawns.WarMap
{
    /// <summary>
    /// Main UI controller for the War Map.
    /// Handles node selection, squad management panel, and drag-drop squad movement.
    /// </summary>
    public class WarMapUI : MonoBehaviour
    {
        #region Configuration
        
        [Header("UI Configuration")]
        [SerializeField] private int squadMenuWidth = 280;
        [SerializeField] private int nodeInfoWidth = 300;
        
        [Header("Colors")]
        [SerializeField] private Color blueColor = new Color(0.2f, 0.5f, 1f);
        [SerializeField] private Color redColor = new Color(1f, 0.3f, 0.3f);
        [SerializeField] private Color greenColor = new Color(0.3f, 0.8f, 0.3f);
        [SerializeField] private Color neutralColor = Color.gray;
        
        #endregion
        
        #region State
        
        // Current faction being controlled (for dev testing)
        private FactionType localPlayerFaction = FactionType.Blue;
        
        // Selected node
        private WarMapNode selectedNode;
        private WarMapNode hoveredNode;
        
        // Squad menu
        private Dictionary<int, bool> squadDropdownOpen = new Dictionary<int, bool>();
        
        // Drag state
        private bool isDraggingSquad;
        private int draggingSquadIndex = -1;
        private PlayerSquadManager draggingSquadManager;
        private Vector2 dragStartPosition;
        
        // References
        private PlayerSquadManager localSquadManager;
        private WarMapCamera warMapCamera;
        
        // GUI styles
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private GUIStyle buttonStyle;
        private GUIStyle labelStyle;
        private GUIStyle dropdownStyle;
        private bool stylesInitialized;
        
        // Cached state for GUI consistency
        private Dictionary<int, bool> cachedDropdownState = new Dictionary<int, bool>();
        private int cachedSquadCount = 0;
        private bool guiStateCached = false;
        
        // Scroll positions
        private Vector2 squadMenuScroll;
        private Vector2 nodeInfoScroll;
        
        #endregion
        
        #region Unity Lifecycle
        
        void Start()
        {
            warMapCamera = WarMapCamera.Instance;
            
            // Don't search immediately - wait for network to be ready
            // FindLocalSquadManager will be called from OnGUI when needed
            Debug.Log("[WarMapUI] Started. Will search for squad manager when network is ready.");
        }
        
        void Update()
        {
            HandleNodeSelection();
            HandleDragAndDrop();
            
            // Periodically try to find squad manager if not found
            if (localSquadManager == null && Time.frameCount % 30 == 0)
            {
                FindLocalSquadManager();
            }
        }
        
        void OnGUI()
        {
            InitializeStyles();
            
            // Cache GUI state during Layout event to ensure consistency during Repaint
            if (Event.current.type == EventType.Layout)
            {
                CacheGUIState();
            }
            
            // Draw squad menu on right side
            DrawSquadMenu();
            
            // Draw node info panel on left side (when node selected)
            if (selectedNode != null)
            {
                DrawNodeInfoPanel();
            }
            
            // Draw drag indicator
            if (isDraggingSquad)
            {
                DrawDragIndicator();
            }
            
            // Draw top bar with faction info
            DrawTopBar();
        }
        
        void CacheGUIState()
        {
            // Cache dropdown states
            cachedDropdownState.Clear();
            foreach (var kvp in squadDropdownOpen)
            {
                cachedDropdownState[kvp.Key] = kvp.Value;
            }
            
            // Cache squad count
            if (localSquadManager != null)
            {
                cachedSquadCount = localSquadManager.SquadCount;
            }
            else
            {
                cachedSquadCount = 0;
            }
            
            guiStateCached = true;
        }
        
        #endregion
        
        #region Node Selection
        
        void HandleNodeSelection()
        {
            // Don't process clicks if dragging
            if (isDraggingSquad) return;
            
            // Raycast to find node under mouse
            hoveredNode = GetNodeUnderMouse();
            
            // Left click to select
            if (Input.GetMouseButtonDown(0) && !IsMouseOverUI())
            {
                if (hoveredNode != null)
                {
                    SelectNode(hoveredNode);
                }
                else
                {
                    // Click on empty space deselects
                    DeselectNode();
                }
            }
        }
        
        WarMapNode GetNodeUnderMouse()
        {
            if (warMapCamera == null) return null;
            
            Camera cam = warMapCamera.GetComponent<Camera>();
            if (cam == null) return null;
            
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                return hit.collider.GetComponent<WarMapNode>() ?? 
                       hit.collider.GetComponentInParent<WarMapNode>();
            }
            
            return null;
        }
        
        void SelectNode(WarMapNode node)
        {
            selectedNode = node;
            warMapCamera?.FocusOnNode(node);
            Debug.Log($"[WarMapUI] Selected node: {node.NodeName}");
        }
        
        void DeselectNode()
        {
            selectedNode = null;
        }
        
        bool IsMouseOverUI()
        {
            Vector2 mousePos = Input.mousePosition;
            mousePos.y = Screen.height - mousePos.y; // Flip for GUI coordinates
            
            // Check squad menu area
            Rect squadMenuRect = new Rect(Screen.width - squadMenuWidth - 10, 50, squadMenuWidth + 10, Screen.height - 60);
            if (squadMenuRect.Contains(mousePos)) return true;
            
            // Check node info panel
            if (selectedNode != null)
            {
                Rect nodeInfoRect = new Rect(10, 50, nodeInfoWidth, Screen.height - 60);
                if (nodeInfoRect.Contains(mousePos)) return true;
            }
            
            // Check top bar
            Rect topBarRect = new Rect(0, 0, Screen.width, 45);
            if (topBarRect.Contains(mousePos)) return true;
            
            return false;
        }
        
        #endregion
        
        #region Drag and Drop
        
        void HandleDragAndDrop()
        {
            if (isDraggingSquad)
            {
                // Check for drop
                if (Input.GetMouseButtonUp(0))
                {
                    CompleteDrag();
                }
                
                // Check for cancel (right click or escape)
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                {
                    CancelDrag();
                }
            }
        }
        
        void StartDrag(PlayerSquadManager manager, int squadIndex)
        {
            isDraggingSquad = true;
            draggingSquadIndex = squadIndex;
            draggingSquadManager = manager;
            dragStartPosition = Input.mousePosition;
            
            Debug.Log($"[WarMapUI] Started dragging squad {squadIndex}");
        }
        
        void CompleteDrag()
        {
            if (!isDraggingSquad || draggingSquadManager == null) return;
            
            // Check if dropped on a node
            WarMapNode targetNode = GetNodeUnderMouse();
            
            if (targetNode != null)
            {
                // Try to move the squad to this node
                bool success = draggingSquadManager.ServerMoveSquad(draggingSquadIndex, targetNode.NodeID);
                
                if (success)
                {
                    Debug.Log($"[WarMapUI] Moved squad {draggingSquadIndex} to {targetNode.NodeName}");
                }
                else
                {
                    Debug.LogWarning($"[WarMapUI] Failed to move squad {draggingSquadIndex} to {targetNode.NodeName}");
                }
            }
            
            CancelDrag();
        }
        
        void CancelDrag()
        {
            isDraggingSquad = false;
            draggingSquadIndex = -1;
            draggingSquadManager = null;
        }
        
        #endregion
        
        #region GUI Drawing
        
        void InitializeStyles()
        {
            if (stylesInitialized) return;
            
            headerStyle = new GUIStyle(GUI.skin.box);
            headerStyle.fontSize = 14;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = Color.white;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.textColor = Color.white;
            
            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.fontSize = 12;
            
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = Color.white;
            labelStyle.fontSize = 12;
            
            dropdownStyle = new GUIStyle(GUI.skin.box);
            dropdownStyle.normal.background = MakeTex(2, 2, new Color(0.3f, 0.3f, 0.35f));
            dropdownStyle.normal.textColor = Color.white;
            
            stylesInitialized = true;
        }
        
        Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
        
        void DrawTopBar()
        {
            GUILayout.BeginArea(new Rect(0, 0, Screen.width, 45));
            GUILayout.BeginHorizontal(boxStyle);
            
            // Faction selector (for dev testing)
            GUILayout.Label("Faction:", labelStyle, GUILayout.Width(50));
            
            GUI.backgroundColor = localPlayerFaction == FactionType.Blue ? Color.cyan : Color.gray;
            if (GUILayout.Button("🔵 Blue", buttonStyle, GUILayout.Width(70)))
            {
                SwitchFaction(FactionType.Blue);
            }
            
            GUI.backgroundColor = localPlayerFaction == FactionType.Red ? Color.red : Color.gray;
            if (GUILayout.Button("🔴 Red", buttonStyle, GUILayout.Width(70)))
            {
                SwitchFaction(FactionType.Red);
            }
            GUI.backgroundColor = Color.white;
            
            GUILayout.Space(20);
            
            // Token count
            if (TokenSystem.Instance != null)
            {
                int tokens = TokenSystem.Instance.GetFactionTokens(localPlayerFaction);
                GUILayout.Label($"💰 Tokens: {tokens}", labelStyle, GUILayout.Width(120));
            }
            
            GUILayout.FlexibleSpace();
            
            // Quick action buttons
            if (GUILayout.Button("+500 Blue", buttonStyle, GUILayout.Width(80)))
            {
                TokenSystem.Instance?.AddTokens(FactionType.Blue, 500, "Debug");
            }
            if (GUILayout.Button("+500 Red", buttonStyle, GUILayout.Width(80)))
            {
                TokenSystem.Instance?.AddTokens(FactionType.Red, 500, "Debug");
            }
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Reset View", buttonStyle, GUILayout.Width(80)))
            {
                warMapCamera?.ResetView();
            }
            
            // Help text
            GUILayout.Label("WASD: Pan | Scroll: Zoom | LClick: Select | Drag squad ≡ to node", labelStyle);
            
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
        
        void SwitchFaction(FactionType newFaction)
        {
            if (localPlayerFaction == newFaction) return;
            
            localPlayerFaction = newFaction;
            localSquadManager = null; // Force re-find
            squadDropdownOpen.Clear();
            
            Debug.Log($"[WarMapUI] Switched to {newFaction} faction");
        }
        
        void DrawSquadMenu()
        {
            Rect menuRect = new Rect(Screen.width - squadMenuWidth - 10, 50, squadMenuWidth, Screen.height - 60);
            
            GUILayout.BeginArea(menuRect);
            GUILayout.BeginVertical(boxStyle);
            
            // Header with faction icon
            string factionIcon = localPlayerFaction == FactionType.Blue ? "🔵" : "🔴";
            GUILayout.Box($"{factionIcon} YOUR SQUADS ({localPlayerFaction})", headerStyle);
            
            squadMenuScroll = GUILayout.BeginScrollView(squadMenuScroll);
            
            // Cache manager reference to avoid changes during GUI calls
            var manager = localSquadManager;
            
            if (manager == null)
            {
                // Only search during Layout event to avoid state changes during Repaint
                if (Event.current.type == EventType.Layout)
                {
                    FindLocalSquadManager();
                }
                
                // Show detailed connection status
                if (NetworkClient.isConnected)
                {
                    if (NetworkClient.localPlayer != null)
                    {
                        var psm = NetworkClient.localPlayer.GetComponent<PlayerSquadManager>();
                        if (psm == null)
                        {
                            GUILayout.Label("ERROR: Player missing PlayerSquadManager!", labelStyle);
                        }
                        else if (psm.Faction == FactionType.None)
                        {
                            GUILayout.Label($"Waiting for faction sync... (Squads: {psm.SquadCount})", labelStyle);
                        }
                        else
                        {
                            GUILayout.Label($"Found: {psm.Faction}, {psm.SquadCount} squads", labelStyle);
                            // If we get here, FindLocalSquadManager should have succeeded
                            // Force another attempt
                            if (Event.current.type == EventType.Layout)
                            {
                                localSquadManager = psm;
                                localPlayerFaction = psm.Faction;
                            }
                        }
                    }
                    else
                    {
                        GUILayout.Label("Connected. Waiting for player spawn...", labelStyle);
                    }
                }
                else
                {
                    GUILayout.Label("Not connected to server.", labelStyle);
                }
            }
            else
            {
                // Cache squad count to ensure consistency between Layout and Repaint
                int squadCount = manager.SquadCount;
                
                // Summary
                GUILayout.Label($"Total: {manager.TotalManpower}/{manager.TotalManpowerCapacity} Manpower", labelStyle);
                
                // Quick actions - always draw both buttons
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Resupply All", buttonStyle, GUILayout.Width(squadMenuWidth / 2 - 10)))
                {
                    ResupplyAllSquads();
                }
                if (GUILayout.Button("Recall All", buttonStyle, GUILayout.Width(squadMenuWidth / 2 - 10)))
                {
                    RecallAllSquads();
                }
                GUILayout.EndHorizontal();
                
                GUILayout.Space(5);
                
                // Draw each squad - use cached count for consistency
                int displayCount = guiStateCached ? cachedSquadCount : squadCount;
                
                if (displayCount == 0)
                {
                    GUILayout.Label("No squads available", labelStyle);
                }
                else
                {
                    for (int i = 0; i < displayCount; i++)
                    {
                        DrawSquadEntry(manager, i);
                    }
                }
            }
            
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        void ResupplyAllSquads()
        {
            if (localSquadManager == null) return;
            
            for (int i = 0; i < localSquadManager.SquadCount; i++)
            {
                var squad = localSquadManager.GetSquad(i);
                if (squad != null && squad.CanResupply && !squad.IsMoving)
                {
                    localSquadManager.ServerResupplySquad(i, squad.MaxManpower);
                }
            }
        }
        
        void RecallAllSquads()
        {
            if (localSquadManager == null) return;
            
            int capitalId = localPlayerFaction == FactionType.Blue ? 0 : 4;
            
            for (int i = 0; i < localSquadManager.SquadCount; i++)
            {
                var squad = localSquadManager.GetSquad(i);
                if (squad != null && !squad.IsMoving && squad.CurrentNodeId != capitalId)
                {
                    localSquadManager.ServerMoveSquad(i, capitalId);
                }
            }
        }
        
        void DrawSquadEntry(PlayerSquadManager manager, int index)
        {
            var squad = manager.GetSquad(index);
            if (squad == null) return;
            
            // Use cached dropdown state for GUI consistency
            bool isOpen = cachedDropdownState.ContainsKey(index) ? cachedDropdownState[index] : false;
            
            // Ensure dropdown state exists
            if (!squadDropdownOpen.ContainsKey(index))
                squadDropdownOpen[index] = false;
            
            // Squad header (clickable to expand)
            GUILayout.BeginVertical(dropdownStyle);
            
            // Header row with drag handle
            GUILayout.BeginHorizontal();
            
            // Drag handle (visual indicator)
            GUILayout.Box("≡", GUILayout.Width(20), GUILayout.Height(25));
            
            // Check for drag start on the handle
            Rect lastRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && lastRect.Contains(Event.current.mousePosition))
            {
                if (!squad.IsMoving)
                {
                    StartDrag(manager, index);
                    Event.current.Use();
                }
            }
            
            // Status icon
            string statusIcon = GetSquadStatusIcon(squad);
            GUILayout.Label(statusIcon, labelStyle, GUILayout.Width(25));
            
            // Squad name and manpower
            string squadName = $"Squad {index + 1}";
            string mpText = $"{squad.Manpower}/{squad.MaxManpower}";
            
            if (GUILayout.Button($"{squadName} [{mpText}]", buttonStyle, GUILayout.ExpandWidth(true)))
            {
                // Toggle dropdown (will take effect next frame)
                squadDropdownOpen[index] = !squadDropdownOpen[index];
            }
            
            GUILayout.EndHorizontal();
            
            // Location info
            string locationText;
            if (squad.IsMoving)
            {
                locationText = $"  → Node {squad.DestinationNodeId} ({squad.MovementProgress:P0})";
            }
            else
            {
                var node = WarMapManager.Instance?.GetNodeByID(squad.CurrentNodeId);
                locationText = $"  @ {node?.NodeName ?? $"Node {squad.CurrentNodeId}"}";
            }
            GUILayout.Label(locationText, labelStyle);
            
            // Dropdown content - use cached state
            if (isOpen)
            {
                DrawSquadDropdown(manager, squad, index);
            }
            
            GUILayout.EndVertical();
            GUILayout.Space(5);
        }
        
        void DrawSquadDropdown(PlayerSquadManager manager, Squad squad, int index)
        {
            GUILayout.BeginVertical();
            GUILayout.Space(5);
            
            bool isContested = IsSquadOnContestedNode(squad);
            
            // Resupply button
            bool canResupply = squad.CanResupply && !squad.IsMoving && !IsSquadEngagedUI(squad);
            GUI.enabled = canResupply;
            
            int resupplyCost = squad.ResupplyCapacity;
            string resupplyText = canResupply ? $"Resupply (+{resupplyCost} MP) [Cost: {resupplyCost}]" : "Cannot Resupply";
            
            if (GUILayout.Button(resupplyText, buttonStyle))
            {
                manager.ServerResupplySquad(index, squad.MaxManpower);
            }
            
            GUI.enabled = true;
            
            // Movement options depend on contested status
            if (isContested)
            {
                // On contested node - only retreat option
                GUILayout.Label("⚠ CONTESTED - Retreat only!", labelStyle);
                
                bool canRetreat = !squad.IsMoving && squad.PreviousNodeId >= 0;
                GUI.enabled = canRetreat;
                
                var retreatNode = WarMapManager.Instance?.GetNodeByID(squad.PreviousNodeId);
                string retreatName = retreatNode?.NodeName ?? $"Node {squad.PreviousNodeId}";
                
                if (GUILayout.Button($"🏃 Retreat to {retreatName}", buttonStyle))
                {
                    manager.ServerMoveSquad(index, squad.PreviousNodeId);
                }
                
                GUI.enabled = true;
            }
            else
            {
                // Normal movement options
                
                // Retreat to capital button
                bool canRetreat = !squad.IsMoving && squad.Manpower > 0;
                GUI.enabled = canRetreat;
                
                int capitalId = localPlayerFaction == FactionType.Blue ? 0 : 4;
                if (squad.CurrentNodeId != capitalId)
                {
                    if (GUILayout.Button("Retreat to Capital", buttonStyle))
                    {
                        manager.ServerMoveSquad(index, capitalId);
                    }
                }
                
                GUI.enabled = true;
                
                // Quick move buttons (to connected nodes)
                if (!squad.IsMoving)
                {
                    var currentNode = WarMapManager.Instance?.GetNodeByID(squad.CurrentNodeId);
                    if (currentNode != null && currentNode.ConnectedNodes != null && currentNode.ConnectedNodes.Count > 0)
                    {
                        GUILayout.Label("Move to:", labelStyle);
                        
                        // Use vertical layout with wrapping instead of horizontal to avoid empty group
                        int buttonsPerRow = 5;
                        int buttonCount = 0;
                        bool inHorizontal = false;
                        
                        foreach (var connectedNode in currentNode.ConnectedNodes)
                        {
                            if (buttonCount % buttonsPerRow == 0)
                            {
                                if (inHorizontal) GUILayout.EndHorizontal();
                                GUILayout.BeginHorizontal();
                                inHorizontal = true;
                            }
                            
                            if (GUILayout.Button($"{connectedNode.NodeID}", buttonStyle, GUILayout.Width(35)))
                            {
                                manager.ServerMoveSquad(index, connectedNode.NodeID);
                            }
                            buttonCount++;
                        }
                        
                        if (inHorizontal) GUILayout.EndHorizontal();
                    }
                }
            }
            
            GUILayout.Space(5);
            GUILayout.EndVertical();
        }
        
        bool IsSquadOnContestedNode(Squad squad)
        {
            if (squad.IsMoving) return false;
            if (CaptureController.Instance == null) return false;
            
            var capture = CaptureController.Instance.GetCaptureAttempt(squad.CurrentNodeId);
            return capture != null && capture.State == CaptureState.Contested;
        }
        
        void DrawNodeInfoPanel()
        {
            Rect panelRect = new Rect(10, 50, nodeInfoWidth, Screen.height - 60);
            
            GUILayout.BeginArea(panelRect);
            GUILayout.BeginVertical(boxStyle);
            
            // Node header
            string ownerIcon = GetFactionIcon(selectedNode.ControllingFaction);
            GUILayout.Box($"{ownerIcon} {selectedNode.NodeName}", headerStyle);
            
            nodeInfoScroll = GUILayout.BeginScrollView(nodeInfoScroll);
            
            // Basic info
            GUILayout.Label($"Node ID: {selectedNode.NodeID}", labelStyle);
            GUILayout.Label($"Type: {selectedNode.Type}", labelStyle);
            GUILayout.Label($"Owner: {selectedNode.ControllingFaction}", labelStyle);
            GUILayout.Label($"Control: {selectedNode.ControlPercentage:F0}%", labelStyle);
            
            GUILayout.Space(10);
            
            // Status flags
            if (selectedNode.IsContested)
            {
                GUILayout.Box("⚔ CONTESTED", headerStyle);
            }
            if (selectedNode.IsBattleActive)
            {
                GUILayout.Box("💥 BATTLE ACTIVE", headerStyle);
            }
            
            // Capture timer
            if (CaptureController.Instance != null)
            {
                var capture = CaptureController.Instance.GetCaptureAttempt(selectedNode.NodeID);
                if (capture != null)
                {
                    GUILayout.Space(5);
                    GUILayout.Label($"─── Capture Status ───", labelStyle);
                    GUILayout.Label($"State: {capture.State}", labelStyle);
                    GUILayout.Label($"Attacker: {capture.AttackingFaction}", labelStyle);
                    
                    if (capture.State == CaptureState.Capturing)
                    {
                        float remaining = CaptureController.Instance.GetRemainingCaptureTime(selectedNode.NodeID);
                        GUILayout.Label($"Time remaining: {remaining:F1}s", labelStyle);
                        
                        // Progress bar
                        Rect barRect = GUILayoutUtility.GetRect(nodeInfoWidth - 30, 15);
                        GUI.Box(barRect, "");
                        Rect fillRect = barRect;
                        fillRect.width *= capture.CaptureProgress;
                        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
                    }
                }
            }
            
            GUILayout.Space(10);
            
            // Squads at node
            GUILayout.Label($"─── Forces at Node ───", labelStyle);
            
            if (NodeOccupancy.Instance != null)
            {
                var squadsHere = NodeOccupancy.Instance.GetSquadsAtNode(selectedNode.NodeID);
                
                if (squadsHere.Count == 0)
                {
                    GUILayout.Label("  No squads present", labelStyle);
                }
                else
                {
                    foreach (var sq in squadsHere)
                    {
                        string icon = GetFactionIcon(sq.Faction);
                        GUILayout.Label($"  {icon} {sq.OwnerDisplayName}: {sq.Manpower} MP", labelStyle);
                    }
                }
                
                // Incoming
                var incoming = NodeOccupancy.Instance.GetSquadsEnRouteToNode(selectedNode.NodeID);
                if (incoming.Count > 0)
                {
                    GUILayout.Space(5);
                    GUILayout.Label("Incoming:", labelStyle);
                    foreach (var sq in incoming)
                    {
                        string icon = GetFactionIcon(sq.Faction);
                        GUILayout.Label($"  {icon} {sq.OwnerDisplayName}: {sq.Manpower} MP (ETA: {sq.ETA:F1}s)", labelStyle);
                    }
                }
                
                // Spawn tickets
                GUILayout.Space(5);
                int myTickets = NodeOccupancy.Instance.GetFactionManpowerAtNode(selectedNode.NodeID, localPlayerFaction);
                GUILayout.Label($"Your spawn tickets: {myTickets}", labelStyle);
            }
            
            GUILayout.Space(10);
            
            // FPS Battle - Join buttons when contested
            GUILayout.Label($"─── FPS Battle ───", labelStyle);
            
            if (selectedNode.IsContested)
            {
                // Node is contested - show join buttons!
                GUILayout.Box("⚔ Battle Available!", headerStyle);
                
                // Get spawn tickets for each faction
                int blueTickets = NodeOccupancy.Instance?.GetFactionManpowerAtNode(selectedNode.NodeID, FactionType.Blue) ?? 0;
                int redTickets = NodeOccupancy.Instance?.GetFactionManpowerAtNode(selectedNode.NodeID, FactionType.Red) ?? 0;
                
                GUILayout.Label($"🔵 Blue tickets: {blueTickets}", labelStyle);
                GUILayout.Label($"🔴 Red tickets: {redTickets}", labelStyle);
                
                GUILayout.Space(5);
                
                // Join buttons
                GUILayout.BeginHorizontal();
                
                GUI.backgroundColor = Color.cyan;
                GUI.enabled = blueTickets > 0;
                if (GUILayout.Button("⚔ Join Blue", buttonStyle, GUILayout.Height(35)))
                {
                    LaunchFPSBattle(selectedNode.NodeID, FactionType.Blue);
                }
                
                GUI.backgroundColor = Color.red;
                GUI.enabled = redTickets > 0;
                if (GUILayout.Button("⚔ Join Red", buttonStyle, GUILayout.Height(35)))
                {
                    LaunchFPSBattle(selectedNode.NodeID, FactionType.Red);
                }
                
                GUI.backgroundColor = Color.white;
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                
                GUILayout.Space(3);
                GUILayout.Label("(Launches FPS in new window)", labelStyle);
            }
            else
            {
                GUILayout.Label("  No battle - node not contested", labelStyle);
                GUILayout.Label("  (Move opposing squads here)", labelStyle);
            }
            
            GUILayout.Space(10);
            
            // Connected nodes
            GUILayout.Label($"─── Connected Nodes ───", labelStyle);
            foreach (var connected in selectedNode.ConnectedNodes)
            {
                string connIcon = GetFactionIcon(connected.ControllingFaction);
                if (GUILayout.Button($"{connIcon} [{connected.NodeID}] {connected.NodeName}", buttonStyle))
                {
                    SelectNode(connected);
                }
            }
            
            GUILayout.EndScrollView();
            
            // Close button
            if (GUILayout.Button("Close [X]", buttonStyle))
            {
                DeselectNode();
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        void DrawDragIndicator()
        {
            if (draggingSquadManager == null || draggingSquadIndex < 0) return;
            
            var squad = draggingSquadManager.GetSquad(draggingSquadIndex);
            if (squad == null) return;
            
            // Draw indicator at mouse position
            Vector2 mousePos = Event.current.mousePosition;
            
            Rect indicatorRect = new Rect(mousePos.x + 15, mousePos.y - 10, 150, 40);
            
            GUI.Box(indicatorRect, $"Squad {draggingSquadIndex + 1}\n{squad.Manpower} MP", boxStyle);
            
            // Highlight target node if hovering
            if (hoveredNode != null)
            {
                // Could draw additional feedback here
            }
        }
        
        #endregion
        
        #region Helpers
        
        void FindLocalSquadManager()
        {
            // In multiplayer, find the local player's manager
            if (NetworkClient.localPlayer != null)
            {
                var squadManager = NetworkClient.localPlayer.GetComponent<PlayerSquadManager>();
                
                if (squadManager != null)
                {
                    // Check if the manager is initialized (faction synced)
                    if (squadManager.Faction == FactionType.None)
                    {
                        // Manager exists but not initialized yet - wait
                        Debug.Log("[WarMapUI] Squad manager found but not initialized yet (Faction=None). Waiting...");
                        return;
                    }
                    
                    localSquadManager = squadManager;
                    
                    // Sync UI faction with player's actual faction
                    localPlayerFaction = localSquadManager.Faction;
                    Debug.Log($"[WarMapUI] Found local player's squad manager. Faction: {localPlayerFaction}, Squads: {localSquadManager.SquadCount}");
                    return;
                }
                else
                {
                    Debug.LogWarning("[WarMapUI] Local player exists but has no PlayerSquadManager component!");
                }
            }
            
            // Fallback for testing: find by faction
            var allManagers = FindObjectsByType<PlayerSquadManager>(FindObjectsSortMode.None);
            foreach (var manager in allManagers)
            {
                if (manager.Faction == localPlayerFaction && manager.Faction != FactionType.None)
                {
                    localSquadManager = manager;
                    Debug.Log($"[WarMapUI] Found squad manager by faction search. Faction: {localPlayerFaction}");
                    break;
                }
            }
            
            if (localSquadManager == null && NetworkClient.isConnected)
            {
                // Only log this occasionally to avoid spam
                if (Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[WarMapUI] Still searching... NetworkClient.localPlayer: {(NetworkClient.localPlayer != null ? "exists" : "null")}, AllManagers: {allManagers.Length}");
                }
            }
        }
        
        string GetFactionIcon(FactionType faction)
        {
            return faction switch
            {
                FactionType.Blue => "🔵",
                FactionType.Red => "🔴",
                FactionType.Green => "🟢",
                _ => "⚪"
            };
        }
        
        string GetSquadStatusIcon(Squad squad)
        {
            if (squad.IsMoving) return "🚶";
            if (squad.Manpower == 0) return "💀";
            if (squad.Manpower < squad.MaxManpower) return "⚠";
            return "✓";
        }
        
        bool IsSquadEngagedUI(Squad squad)
        {
            if (squad.IsMoving) return false;
            if (CaptureController.Instance == null) return false;
            
            var capture = CaptureController.Instance.GetCaptureAttempt(squad.CurrentNodeId);
            if (capture == null) return false;
            
            return capture.State == CaptureState.Capturing || capture.State == CaptureState.Contested;
        }
        
        void LaunchFPSBattle(int nodeId, FactionType faction)
        {
            if (FPSLauncher.Instance != null)
            {
                string playerName = $"{faction}Soldier";
                FPSLauncher.Instance.LaunchFPS(nodeId, faction, playerName);
                Debug.Log($"[WarMapUI] Launching FPS for battle at node {nodeId} as {faction}");
            }
            else
            {
                Debug.LogError("[WarMapUI] FPSLauncher not found! It should be created by WarMapTestHarness.");
            }
        }
        
        #endregion
    }
}
