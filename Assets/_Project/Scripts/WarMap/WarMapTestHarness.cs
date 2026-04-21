using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;
using ElitesAndPawns.Core;

namespace ElitesAndPawns.WarMap
{
    /// <summary>
    /// Test harness for the War Map system including squad management,
    /// node occupancy, capture timers, and spawn ticket mechanics.
    /// 
    /// SETUP: Requires these GameObjects pre-placed in scene with NetworkIdentity:
    /// - WarMapManager
    /// - TokenSystem
    /// - NodeOccupancy
    /// - CaptureController
    /// And NetworkManager with a Player Prefab that has PlayerSquadManager.
    /// </summary>
    public class WarMapTestHarness : MonoBehaviour
    {
        [Header("Test Configuration")]
        [SerializeField] private bool autoInitialize = true;
        [SerializeField] private bool showDebugGUI = true;
        [SerializeField] private int guiWidth = 320;
        [SerializeField] private bool createTestNodes = true;
        
        [Header("Quick Actions")]
        [SerializeField] private Team testFaction = Team.Blue;
        [SerializeField] private int testNodeID = 0;
        [SerializeField] private int testSquadIndex = 0;
        
        [Header("Prefabs")]
        [Tooltip("Assign the NetworkPlayer prefab from Assets/_Project/Prefabs/WarMap")]
        [SerializeField] private GameObject testPlayerPrefab;
        
        [Tooltip("Assign the WarMapNode prefab from Assets/_Project/Prefabs/WarMap")]
        [SerializeField] private GameObject warMapNodePrefab;
        
        [Header("Interactive Mode")]
        [SerializeField] private bool useInteractiveUI = true;
        [SerializeField] private bool autoCreateCamera = true;
        
        // References to pre-placed managers (found at runtime)
        private WarMapManager warMapManager;
        private TokenSystem tokenSystem;
        private NodeOccupancy nodeOccupancy;
        private CaptureController captureController;
        
        // Test players (created for testing, real game uses NetworkManager's prefab)
        private Dictionary<Team, PlayerSquadManager> testSquadManagers = new Dictionary<Team, PlayerSquadManager>();
        
        private bool isInitialized = false;
        private NetworkManager networkManager;
        
        // GUI state
        private Vector2 scrollPosition;
        private bool showSquadDetails = true;
        private bool showNodeOccupancy = true;
        private bool showCaptureTimers = true;
        
        // GUI styles
        private GUIStyle blackLabelStyle;
        private GUIStyle blackBoxStyle;
        private GUIStyle blackButtonStyle;
        private GUIStyle blackToggleStyle;
        private bool stylesInitialized = false;
        
        #region Initialization
        
        /// <summary>
        /// Determines if this instance should run as a client.
        /// </summary>
        private bool ShouldRunAsClient()
        {
            // Check command line for explicit flags
            string[] args = System.Environment.GetCommandLineArgs();
            foreach (string arg in args)
            {
                if (arg.ToLower() == "-client")
                {
                    Debug.Log("[WarMapTest] -client flag detected");
                    return true;
                }
                if (arg.ToLower() == "-host" || arg.ToLower() == "-server")
                {
                    Debug.Log("[WarMapTest] -host/-server flag detected");
                    return false;
                }
            }
            
            #if UNITY_EDITOR
            // In editor: default to host
            return false;
            #else
            // In builds: default to CLIENT (assumes server is running)
            Debug.Log("[WarMapTest] Build detected - defaulting to CLIENT mode");
            return true;
            #endif
        }
        
        void Start()
        {
            if (autoInitialize)
            {
                StartCoroutine(InitializeTestSystem());
            }
        }
        
        IEnumerator InitializeTestSystem()
        {
            Debug.Log("[WarMapTest] Initializing test system...");
            
            // Find NetworkManager
            networkManager = FindAnyObjectByType<NetworkManager>();
            if (networkManager == null)
            {
                Debug.LogError("[WarMapTest] No NetworkManager in scene!");
                yield break;
            }
            
            // Check if we should be a client or server
            bool shouldBeClient = ShouldRunAsClient();
            bool isHeadless = Application.isBatchMode;
            
            // Start appropriate network mode
            if (!NetworkServer.active && !NetworkClient.active)
            {
                if (isHeadless)
                {
                    // Headless = dedicated server
                    Debug.Log("[WarMapTest] Starting as DEDICATED SERVER...");
                    networkManager.StartServer();
                }
                else if (shouldBeClient)
                {
                    // Client mode - connect to server
                    Debug.Log($"[WarMapTest] Starting as CLIENT, connecting to {networkManager.networkAddress}...");
                    networkManager.StartClient();
                    
                    // Wait for connection
                    float timeout = 5f;
                    while (!NetworkClient.isConnected && timeout > 0)
                    {
                        yield return new WaitForSeconds(0.1f);
                        timeout -= 0.1f;
                    }
                    
                    if (!NetworkClient.isConnected)
                    {
                        Debug.LogError("[WarMapTest] Failed to connect to server!");
                        yield break;
                    }
                    Debug.Log("[WarMapTest] Connected to server!");
                }
                else
                {
                    // Default: start as host (for editor testing)
                    Debug.Log("[WarMapTest] Starting as HOST...");
                    networkManager.StartHost();
                }
                yield return new WaitForSeconds(0.3f);
            }
            
            // Find pre-placed managers
            warMapManager = FindAnyObjectByType<WarMapManager>();
            tokenSystem = FindAnyObjectByType<TokenSystem>();
            nodeOccupancy = FindAnyObjectByType<NodeOccupancy>();
            captureController = FindAnyObjectByType<CaptureController>();
            
            // Validate managers exist
            if (warMapManager == null) Debug.LogError("[WarMapTest] WarMapManager not found in scene!");
            if (tokenSystem == null) Debug.LogError("[WarMapTest] TokenSystem not found in scene!");
            if (nodeOccupancy == null) Debug.LogError("[WarMapTest] NodeOccupancy not found in scene!");
            if (captureController == null) Debug.LogError("[WarMapTest] CaptureController not found in scene!");
            
            // Wait for network objects to spawn
            yield return new WaitForSeconds(0.2f);
            
            // SERVER ONLY: Create test nodes and squads
            if (NetworkServer.active)
            {
                // Create test nodes if needed
                if (createTestNodes)
                {
                    CreateTestNodes();
                    yield return null;
                    
                    // Register nodes with manager
                    if (warMapManager != null)
                    {
                        warMapManager.RegisterExistingNodes();
                        Debug.Log($"[WarMapTest] Registered {warMapManager.Nodes.Count} nodes");
                    }
                    
                    // Reinitialize NodeOccupancy now that nodes exist
                    if (nodeOccupancy != null)
                    {
                        nodeOccupancy.ReinitializeForNodes();
                    }
                }
                
                yield return new WaitForSeconds(0.2f);
                
                // Create test squad managers for Blue and Red
                CreateTestSquadManager(Team.Blue, "BlueCommander", 0);
                CreateTestSquadManager(Team.Red, "RedCommander", 4);
            }
            else
            {
                // CLIENT: Wait for server to spawn networked objects
                Debug.Log("[WarMapTest] Client waiting for server objects...");
                yield return new WaitForSeconds(1.0f);
                
                // Try to find nodes spawned by server
                var nodes = FindObjectsByType<WarMapNode>(FindObjectsSortMode.None);
                Debug.Log($"[WarMapTest] Client found {nodes.Length} nodes");
                
                // Refresh node connections on client side
                foreach (var node in nodes)
                {
                    node.RefreshConnections();
                }
                
                // Create visual connection lines on client
                if (nodes.Length > 0)
                {
                    CreateConnectionLines();
                }
            }
            
            isInitialized = true;
            
            // Setup interactive mode
            if (useInteractiveUI)
            {
                SetupInteractiveMode();
            }
            
            Debug.Log("[WarMapTest] ✓ Test system ready!");
        }
        
        void SetupInteractiveMode()
        {
            // Create war map camera if needed (both client and server)
            if (autoCreateCamera && WarMapCamera.Instance == null)
            {
                GameObject camGO = new GameObject("WarMapCamera");
                var warMapCam = camGO.AddComponent<WarMapCamera>();
                Debug.Log("[WarMapTest] Created WarMapCamera");
            }
            
            // Disable main camera if it exists (to avoid duplicate cameras)
            var mainCam = Camera.main;
            if (mainCam != null && mainCam.GetComponent<WarMapCamera>() == null)
            {
                mainCam.gameObject.SetActive(false);
                Debug.Log("[WarMapTest] Disabled existing main camera");
            }
            
            // Create war map UI (both client and server)
            if (FindAnyObjectByType<WarMapUI>() == null)
            {
                GameObject uiGO = new GameObject("WarMapUI");
                uiGO.AddComponent<WarMapUI>();
                Debug.Log("[WarMapTest] Created WarMapUI");
            }
            
            // SERVER ONLY: Create networked BattleSceneBridge
            if (NetworkServer.active && FindAnyObjectByType<BattleSceneBridge>() == null)
            {
                GameObject bridgeGO = new GameObject("BattleSceneBridge");
                bridgeGO.AddComponent<BattleSceneBridge>();
                bridgeGO.AddComponent<NetworkIdentity>();
                NetworkServer.Spawn(bridgeGO);
                Debug.Log("[WarMapTest] Created BattleSceneBridge");
            }
            
            // Create BattleUI (both client and server)
            if (FindAnyObjectByType<BattleUI>() == null)
            {
                GameObject battleUIGO = new GameObject("BattleUI");
                battleUIGO.AddComponent<BattleUI>();
                Debug.Log("[WarMapTest] Created BattleUI");
            }
            
            // Create FPSLauncher for launching FPS client (both client and server)
            if (FindAnyObjectByType<FPSLauncher>() == null)
            {
                GameObject launcherGO = new GameObject("FPSLauncher");
                launcherGO.AddComponent<FPSLauncher>();
                Debug.Log("[WarMapTest] Created FPSLauncher");
            }
            
            // Disable the old debug GUI when using interactive mode
            showDebugGUI = false;
        }
        
        void CreateTestSquadManager(Team faction, string displayName, int startNodeId)
        {
            GameObject playerGO;
            
            // Use prefab if assigned, otherwise try to load from Resources
            if (testPlayerPrefab != null)
            {
                playerGO = Instantiate(testPlayerPrefab);
                playerGO.name = $"TestPlayer_{faction}";
            }
            else
            {
                // Fallback: try to load from Resources
                var prefab = Resources.Load<GameObject>("WarMap/NetworkPlayer");
                if (prefab != null)
                {
                    playerGO = Instantiate(prefab);
                    playerGO.name = $"TestPlayer_{faction}";
                }
                else
                {
                    Debug.LogError("[WarMapTest] No testPlayerPrefab assigned! Please assign the NetworkPlayer prefab in the inspector.");
                    return;
                }
            }
            
            var squadManager = playerGO.GetComponent<PlayerSquadManager>();
            if (squadManager == null)
            {
                Debug.LogError($"[WarMapTest] NetworkPlayer prefab is missing PlayerSquadManager component!");
                Destroy(playerGO);
                return;
            }
            
            NetworkServer.Spawn(playerGO);
            
            // Initialize after a short delay to ensure NetworkBehaviour is ready
            StartCoroutine(InitializeSquadManagerDelayed(squadManager, faction, displayName, startNodeId));
            
            testSquadManagers[faction] = squadManager;
        }
        
        IEnumerator InitializeSquadManagerDelayed(PlayerSquadManager manager, Team faction, string name, int nodeId)
        {
            // Wait for Mirror to fully initialize the NetworkBehaviour
            yield return new WaitForSeconds(0.3f);
            
            if (manager == null)
            {
                Debug.LogError($"[WarMapTest] Squad manager destroyed before init: {name}");
                yield break;
            }
            
            manager.Initialize(faction, name, nodeId);
            
            // Register with NodeOccupancy
            if (nodeOccupancy != null)
            {
                nodeOccupancy.RegisterSquadManager(manager);
            }
            
            Debug.Log($"[WarMapTest] ✓ {name} ready at node {nodeId}");
        }
        
        void CreateTestNodes()
        {
            if (!NetworkServer.active)
                return;
            
            // Check if nodes already exist
            if (FindObjectsByType<WarMapNode>(FindObjectsSortMode.None).Length > 0)
            {
                Debug.Log("[WarMapTest] Nodes already exist, skipping creation");
                return;
            }
            
            // Check if prefab is assigned
            if (warMapNodePrefab == null)
            {
                Debug.LogError("[WarMapTest] WarMapNode prefab not assigned! Use Tools → Elites and Pawns → Create WarMapNode Prefab, then assign it to WarMapTestHarness.");
                return;
            }
            
            Debug.Log("[WarMapTest] Creating test nodes...");
            
            // Positions spread out for top-down view
            Vector3[] positions = {
                new Vector3(-8, 0, 0),   // 0: Blue Capital
                new Vector3(-3, 0, 5),   // 1: Northern
                new Vector3(0, 0, 0),    // 2: Center
                new Vector3(-3, 0, -5),  // 3: Southern
                new Vector3(8, 0, 0)     // 4: Red Capital
            };
            
            string[] names = { "Blue Capital", "Northern Outpost", "Central Hub", "Southern Fort", "Red Capital" };
            
            WarMapNode.NodeType[] types = {
                WarMapNode.NodeType.Capital,
                WarMapNode.NodeType.Strategic,
                WarMapNode.NodeType.Resource,
                WarMapNode.NodeType.Strategic,
                WarMapNode.NodeType.Capital
            };
            
            // Connectivity: 0-1-2-4, 0-2, 0-3-2-4, 1-4, 3-4
            List<int>[] connections = {
                new List<int> { 1, 2, 3 },     // 0 connects to 1, 2, 3
                new List<int> { 0, 2, 4 },     // 1 connects to 0, 2, 4
                new List<int> { 0, 1, 3, 4 },  // 2 connects to all
                new List<int> { 0, 2, 4 },     // 3 connects to 0, 2, 4
                new List<int> { 1, 2, 3 }      // 4 connects to 1, 2, 3
            };
            
            for (int i = 0; i < 5; i++)
            {
                // Instantiate from prefab (this is what Mirror can replicate!)
                GameObject nodeGO = Instantiate(warMapNodePrefab);
                nodeGO.name = $"Node_{i}_{names[i]}";
                nodeGO.transform.position = positions[i];
                
                var node = nodeGO.GetComponent<WarMapNode>();
                if (node == null)
                {
                    Debug.LogError($"[WarMapTest] Prefab missing WarMapNode component!");
                    Destroy(nodeGO);
                    continue;
                }
                
                // Spawn on network BEFORE initializing (so SyncVars work)
                NetworkServer.Spawn(nodeGO);
                
                // Now initialize (this will sync via SyncVars)
                node.Initialize(i, names[i], types[i], connections[i]);
                
                // Set initial control
                if (i == 0)
                    node.SetControl(Team.Blue, 100f);
                else if (i == 4)
                    node.SetControl(Team.Red, 100f);
                else
                    node.SetControl(Team.None, 0f);
            }
            
            // Create visual connection lines
            CreateConnectionLines();
            
            Debug.Log("[WarMapTest] Created 5 test nodes");
        }
        
        void CreateConnectionLines()
        {
            // Skip visual creation on headless servers (no shaders available)
            if (Application.isBatchMode)
            {
                Debug.Log("[WarMapTest] Skipping connection lines on headless server");
                return;
            }
            
            // Create a parent for all lines (or find existing)
            GameObject lineParent = GameObject.Find("ConnectionLines");
            if (lineParent != null)
            {
                // Already created, skip
                return;
            }
            lineParent = new GameObject("ConnectionLines");
            
            var nodes = FindObjectsByType<WarMapNode>(FindObjectsSortMode.None);
            HashSet<string> drawnConnections = new HashSet<string>();
            
            foreach (var node in nodes)
            {
                foreach (int connectedId in node.ConnectedNodeIDs)
                {
                    // Create unique key for this connection
                    int minId = Mathf.Min(node.NodeID, connectedId);
                    int maxId = Mathf.Max(node.NodeID, connectedId);
                    string key = $"{minId}-{maxId}";
                    
                    if (drawnConnections.Contains(key)) continue;
                    drawnConnections.Add(key);
                    
                    // Find connected node
                    WarMapNode connectedNode = null;
                    foreach (var n in nodes)
                    {
                        if (n.NodeID == connectedId)
                        {
                            connectedNode = n;
                            break;
                        }
                    }
                    
                    if (connectedNode == null) continue;
                    
                    // Create line
                    GameObject lineGO = new GameObject($"Line_{key}");
                    lineGO.transform.parent = lineParent.transform;
                    
                    LineRenderer line = lineGO.AddComponent<LineRenderer>();
                    line.positionCount = 2;
                    line.SetPosition(0, node.transform.position);
                    line.SetPosition(1, connectedNode.transform.position);
                    line.startWidth = 0.1f;
                    line.endWidth = 0.1f;
                    
                    // Safely create material (shader might not exist in some builds)
                    var shader = Shader.Find("Sprites/Default");
                    if (shader != null)
                    {
                        line.material = new Material(shader);
                        line.startColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                        line.endColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                    }
                    else
                    {
                        // Fallback - use Unity's built-in line material
                        line.material = new Material(Shader.Find("Hidden/Internal-Colored"));
                        if (line.material != null)
                        {
                            line.startColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                            line.endColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                        }
                    }
                }
            }
        }
        
        #endregion
        
        #region Update & Visuals
        
        void Update()
        {
            if (!isInitialized)
                return;
            
            // Update node colors
            var allNodes = FindObjectsByType<WarMapNode>(FindObjectsSortMode.None);
            foreach (var node in allNodes)
            {
                UpdateNodeVisual(node);
            }
        }
        
        void UpdateNodeVisual(WarMapNode node)
        {
            var renderer = node.GetComponent<Renderer>();
            if (renderer == null) return;
            
            Color color = node.ControllingFaction switch
            {
                Team.Blue => Color.blue,
                Team.Red => Color.red,
                Team.Green => Color.green,
                _ => Color.gray
            };
            
            if (node.IsContested) color = Color.yellow;
            if (node.IsBattleActive) color = Color.magenta;
            
            // Pulse effect for capturing nodes
            if (captureController != null)
            {
                var capture = captureController.GetCaptureAttempt(node.NodeID);
                if (capture != null && capture.State == CaptureState.Capturing)
                {
                    float pulse = Mathf.PingPong(Time.time * 2f, 1f);
                    color = Color.Lerp(color, Color.white, pulse * 0.5f);
                }
            }
            
            renderer.material.color = color;
        }
        
        #endregion
        
        #region GUI
        
        void InitializeGUIStyles()
        {
            if (stylesInitialized) return;
            
            blackLabelStyle = new GUIStyle(GUI.skin.label);
            blackLabelStyle.normal.textColor = Color.black;
            
            blackBoxStyle = new GUIStyle(GUI.skin.box);
            blackBoxStyle.normal.textColor = Color.black;
            
            blackButtonStyle = new GUIStyle(GUI.skin.button);
            blackButtonStyle.normal.textColor = Color.black;
            
            blackToggleStyle = new GUIStyle(GUI.skin.toggle);
            blackToggleStyle.normal.textColor = Color.black;
            
            stylesInitialized = true;
        }
        
        void OnGUI()
        {
            if (!showDebugGUI || !isInitialized)
                return;
            
            InitializeGUIStyles();
            
            GUILayout.BeginArea(new Rect(10, 10, guiWidth, Screen.height - 20));
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            
            DrawHeader();
            DrawNetworkStatus();
            DrawTokenSection();
            DrawSelectors();
            DrawSquadSection();
            DrawNodeOccupancySection();
            DrawCaptureSection();
            DrawNodeList();
            
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        
        void DrawHeader()
        {
            GUILayout.Box("═══ WAR MAP TEST ═══", blackBoxStyle);
        }
        
        void DrawNetworkStatus()
        {
            string status = $"Server: {(NetworkServer.active ? "✓" : "✗")} | Client: {(NetworkClient.active ? "✓" : "✗")}";
            if (warMapManager != null)
                status += $" | Nodes: {warMapManager.Nodes.Count}";
            GUILayout.Label(status, blackLabelStyle);
            GUILayout.Space(5);
        }
        
        void DrawTokenSection()
        {
            if (tokenSystem == null) return;
            
            GUILayout.Label($"─── Tokens ───", blackLabelStyle);
            GUILayout.Label($"Blue: {tokenSystem.GetFactionTokens(Team.Blue)} | Red: {tokenSystem.GetFactionTokens(Team.Red)}", blackLabelStyle);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+500 Blue", blackButtonStyle))
                tokenSystem.AddTokens(Team.Blue, 500, "Test");
            if (GUILayout.Button("+500 Red", blackButtonStyle))
                tokenSystem.AddTokens(Team.Red, 500, "Test");
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }
        
        void DrawSelectors()
        {
            GUILayout.Label($"─── Selection ───", blackLabelStyle);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Faction: {testFaction}", blackLabelStyle, GUILayout.Width(100));
            if (GUILayout.Button("◄", blackButtonStyle, GUILayout.Width(30))) 
                testFaction = testFaction == Team.Blue ? Team.Red : Team.Blue;
            if (GUILayout.Button("►", blackButtonStyle, GUILayout.Width(30))) 
                testFaction = testFaction == Team.Red ? Team.Blue : Team.Red;
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Node: {testNodeID}", blackLabelStyle, GUILayout.Width(100));
            if (GUILayout.Button("◄", blackButtonStyle, GUILayout.Width(30))) 
                testNodeID = Mathf.Max(0, testNodeID - 1);
            if (GUILayout.Button("►", blackButtonStyle, GUILayout.Width(30))) 
                testNodeID = Mathf.Min(4, testNodeID + 1);
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Squad: {testSquadIndex}", blackLabelStyle, GUILayout.Width(100));
            if (GUILayout.Button("◄", blackButtonStyle, GUILayout.Width(30))) 
                testSquadIndex = Mathf.Max(0, testSquadIndex - 1);
            if (GUILayout.Button("►", blackButtonStyle, GUILayout.Width(30))) 
                testSquadIndex = Mathf.Min(2, testSquadIndex + 1);
            GUILayout.EndHorizontal();
            
            // Quick node control
            if (GUILayout.Button($"Give Node {testNodeID} to {testFaction}", blackButtonStyle))
            {
                var node = warMapManager?.GetNodeByID(testNodeID);
                if (node != null)
                {
                    node.SetControl(testFaction, 100f);
                    Debug.Log($"[Test] Node {testNodeID} → {testFaction}");
                }
            }
            GUILayout.Space(5);
        }
        
        void DrawSquadSection()
        {
            showSquadDetails = GUILayout.Toggle(showSquadDetails, "─── Squads ───", blackToggleStyle);
            if (!showSquadDetails) return;
            
            if (!testSquadManagers.TryGetValue(testFaction, out var manager) || manager == null)
            {
                GUILayout.Label($"No squad manager for {testFaction}", blackLabelStyle);
                return;
            }
            
            GUILayout.Label($"{testFaction}: {manager.SquadCount} squads, {manager.TotalManpower}/{manager.TotalManpowerCapacity} MP", blackLabelStyle);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Resupply +8", blackButtonStyle))
            {
                // Use server-side method (bypasses Command authority)
                manager.ServerResupplySquad(testSquadIndex, 8);
            }
            if (GUILayout.Button("Resupply All", blackButtonStyle))
            {
                for (int i = 0; i < manager.SquadCount; i++)
                    manager.ServerResupplySquad(i, 8);
            }
            GUILayout.EndHorizontal();
            
            if (GUILayout.Button($"Move Squad {testSquadIndex} → Node {testNodeID}", blackButtonStyle))
            {
                // Use server-side method (bypasses Command authority)
                manager.ServerMoveSquad(testSquadIndex, testNodeID);
            }
            
            // List squads
            foreach (var squad in manager.Squads)
            {
                string marker = squad.SquadIndex == testSquadIndex ? "►" : " ";
                string location = squad.IsMoving 
                    ? $"→{squad.DestinationNodeId} ({squad.MovementProgress:P0})"
                    : $"@{squad.CurrentNodeId}";
                GUILayout.Label($"{marker}[{squad.SquadIndex}] {squad.Manpower}/{squad.MaxManpower} {location}", blackLabelStyle);
            }
            GUILayout.Space(5);
        }
        
        void DrawNodeOccupancySection()
        {
            showNodeOccupancy = GUILayout.Toggle(showNodeOccupancy, "─── Node Details ───", blackToggleStyle);
            if (!showNodeOccupancy || nodeOccupancy == null) return;
            
            var node = warMapManager?.GetNodeByID(testNodeID);
            if (node == null) return;
            
            // Node header
            string ownerIcon = node.ControllingFaction switch
            {
                Team.Blue => "🔵",
                Team.Red => "🔴",
                _ => "⚪"
            };
            GUILayout.Label($"{ownerIcon} Node {testNodeID}: {node.NodeName}", blackLabelStyle);
            GUILayout.Label($"   Owner: {node.ControllingFaction} | Control: {node.ControlPercentage:F0}%", blackLabelStyle);
            
            // Manpower summary
            int blueMP = nodeOccupancy.GetFactionManpowerAtNode(testNodeID, Team.Blue);
            int redMP = nodeOccupancy.GetFactionManpowerAtNode(testNodeID, Team.Red);
            GUILayout.Label($"   🔵 Blue: {blueMP} spawn tickets | 🔴 Red: {redMP} spawn tickets", blackLabelStyle);
            
            // List individual squads
            var squads = nodeOccupancy.GetSquadsAtNode(testNodeID);
            if (squads.Count > 0)
            {
                GUILayout.Label("   Squads present:", blackLabelStyle);
                foreach (var sq in squads)
                {
                    string factionIcon = sq.Faction == Team.Blue ? "🔵" : "🔴";
                    GUILayout.Label($"      {factionIcon} {sq.OwnerDisplayName}[{sq.SquadId.Split('_').Last()}]: {sq.Manpower} MP", blackLabelStyle);
                }
            }
            
            // Incoming squads
            var incoming = nodeOccupancy.GetSquadsEnRouteToNode(testNodeID);
            if (incoming.Count > 0)
            {
                GUILayout.Label("   Incoming:", blackLabelStyle);
                foreach (var sq in incoming)
                {
                    string factionIcon = sq.Faction == Team.Blue ? "🔵" : "🔴";
                    GUILayout.Label($"      {factionIcon} {sq.OwnerDisplayName}: {sq.Manpower} MP (ETA: {sq.ETA:F1}s)", blackLabelStyle);
                }
            }
            
            GUILayout.Space(5);
            
            // Spawn ticket test button
            if (GUILayout.Button($"Request Spawn Ticket ({testFaction})", blackButtonStyle))
            {
                if (nodeOccupancy.RequestSpawnTicket(testNodeID, testFaction, 999, out string squadId, out uint ownerId))
                    Debug.Log($"[Test] Spawn ticket from {squadId}");
                else
                    Debug.Log($"[Test] No spawn tickets for {testFaction} at node {testNodeID}");
            }
            GUILayout.Space(5);
        }
        
        void DrawCaptureSection()
        {
            showCaptureTimers = GUILayout.Toggle(showCaptureTimers, "─── Captures ───", blackToggleStyle);
            if (!showCaptureTimers || captureController == null) return;
            
            var captures = captureController.GetAllCaptureAttempts();
            if (captures.Count == 0)
            {
                GUILayout.Label("No active captures", blackLabelStyle);
            }
            else
            {
                foreach (var kvp in captures)
                {
                    var cap = kvp.Value;
                    if (cap.State == CaptureState.Capturing)
                    {
                        float remaining = captureController.GetRemainingCaptureTime(kvp.Key);
                        GUILayout.Label($"Node {kvp.Key}: {cap.AttackingFaction} ({remaining:F1}s)", blackLabelStyle);
                        
                        // Simple progress bar
                        Rect rect = GUILayoutUtility.GetRect(guiWidth - 40, 8);
                        GUI.Box(rect, "");
                        rect.width *= cap.CaptureProgress;
                        GUI.DrawTexture(rect, Texture2D.whiteTexture);
                    }
                    else
                    {
                        GUILayout.Label($"Node {kvp.Key}: {cap.State}", blackLabelStyle);
                    }
                }
            }
            GUILayout.Space(5);
        }
        
        void DrawNodeList()
        {
            GUILayout.Label("─── War Map Overview ───", blackLabelStyle);
            
            var allNodes = FindObjectsByType<WarMapNode>(FindObjectsSortMode.None);
            System.Array.Sort(allNodes, (a, b) => a.NodeID.CompareTo(b.NodeID));
            
            foreach (var node in allNodes)
            {
                string marker = node.NodeID == testNodeID ? "►" : " ";
                string ownerIcon = node.ControllingFaction switch
                {
                    Team.Blue => "🔵",
                    Team.Red => "🔴",
                    Team.Green => "🟢",
                    _ => "⚪"
                };
                
                string flags = "";
                if (node.IsContested) flags += " ⚔";
                if (node.IsBattleActive) flags += " 💥";
                
                // Get manpower at node
                string mpInfo = "";
                if (nodeOccupancy != null)
                {
                    int b = nodeOccupancy.GetFactionManpowerAtNode(node.NodeID, Team.Blue);
                    int r = nodeOccupancy.GetFactionManpowerAtNode(node.NodeID, Team.Red);
                    
                    if (b > 0) mpInfo += $" 🔵{b}";
                    if (r > 0) mpInfo += $" 🔴{r}";
                }
                
                // Get capture timer if active
                string captureInfo = "";
                if (captureController != null)
                {
                    var capture = captureController.GetCaptureAttempt(node.NodeID);
                    if (capture != null)
                    {
                        if (capture.State == CaptureState.Capturing)
                        {
                            float remaining = captureController.GetRemainingCaptureTime(node.NodeID);
                            captureInfo = $" ⏱{remaining:F0}s";
                        }
                        else if (capture.State == CaptureState.Contested)
                        {
                            captureInfo = " ⚔CONTESTED";
                        }
                    }
                }
                
                GUILayout.Label($"{marker}{ownerIcon} [{node.NodeID}] {node.NodeName}{flags}{mpInfo}{captureInfo}", blackLabelStyle);
            }
            
            GUILayout.Space(10);
            
            // Show moving squads section
            DrawMovingSquadsSection();
        }
        
        void DrawMovingSquadsSection()
        {
            GUILayout.Label("─── Moving Squads ───", blackLabelStyle);
            
            bool anyMoving = false;
            
            foreach (var kvp in testSquadManagers)
            {
                var manager = kvp.Value;
                if (manager == null) continue;
                
                foreach (var squad in manager.Squads)
                {
                    if (squad.IsMoving)
                    {
                        anyMoving = true;
                        string factionIcon = squad.Faction == Team.Blue ? "🔵" : "🔴";
                        float progress = squad.MovementProgress * 100f;
                        float eta = squad.TimeToArrival;
                        
                        GUILayout.Label($"{factionIcon} {squad.OwnerDisplayName}[{squad.SquadIndex}]: " +
                                       $"{squad.CurrentNodeId}→{squad.DestinationNodeId} ({progress:F0}% ETA:{eta:F1}s)", 
                                       blackLabelStyle);
                    }
                }
            }
            
            if (!anyMoving)
            {
                GUILayout.Label("  No squads in transit", blackLabelStyle);
            }
        }
        
        #endregion
    }
}
