using System.Collections;
using UnityEngine;
using Mirror;
using ElitesAndPawns.Core;

namespace ElitesAndPawns.WarMap
{
    public class WarMapTestHarness : MonoBehaviour
    {
        [Header("Test Configuration")]
        [SerializeField] private bool autoInitialize = true;
        [SerializeField] private bool showDebugGUI = true;
        [SerializeField] private int guiWidth = 300;
        
        [Header("Quick Actions")]
        [SerializeField] private Team testFaction = Team.Blue;
        [SerializeField] private int testNodeID = 0;
        
        private WarMapManager warMapManager;
        private TokenSystem tokenSystem;
        private bool isInitialized = false;
        private NetworkManager networkManager;
        
        void Start()
        {
            if (autoInitialize)
            {
                StartCoroutine(InitializeWarMapSystem());
            }
        }
        
        IEnumerator InitializeWarMapSystem()
        {
            Debug.Log("[WarMapTest] Initializing Real-Time War Map test system...");
            
            // Start network if not already active
            if (!NetworkServer.active && !NetworkClient.active)
            {
                Debug.Log("[WarMapTest] Starting network server...");
                EnsureNetworkManager();
                
                if (networkManager != null)
                {
                    networkManager.StartHost();
                    yield return new WaitForSeconds(0.5f);
                    Debug.Log("[WarMapTest] Network server started successfully!");
                }
            }
            
            // Create WarMapManager if not exists
            if (WarMapManager.Instance == null)
            {
                GameObject warMapGO = new GameObject("WarMapManager");
                warMapManager = warMapGO.AddComponent<WarMapManager>();
                var netId = warMapGO.AddComponent<NetworkIdentity>();
                
                if (NetworkServer.active)
                {
                    NetworkServer.Spawn(warMapGO);
                }
            }
            else
            {
                warMapManager = WarMapManager.Instance;
            }
            
            // Create TokenSystem if not exists  
            if (TokenSystem.Instance == null)
            {
                GameObject tokenGO = new GameObject("TokenSystem");
                tokenSystem = tokenGO.AddComponent<TokenSystem>();
                var netId = tokenGO.AddComponent<NetworkIdentity>();
                
                if (NetworkServer.active)
                {
                    NetworkServer.Spawn(tokenGO);
                }
            }
            else
            {
                tokenSystem = TokenSystem.Instance;
            }
            
            // Wait for everything to initialize
            yield return null;
            yield return null;
            
            // Create nodes AFTER managers are ready
            CreateSimpleNodeVisuals();
            
            // CRITICAL: Register the nodes we just created
            yield return null; // One more frame to be sure nodes are spawned
            
            if (warMapManager != null && NetworkServer.active)
            {
                Debug.Log("[WarMapTest] ⚠️ REGISTERING NODES WITH MANAGER");
                warMapManager.RegisterExistingNodes();
                Debug.Log($"[WarMapTest] ✓ Manager now has {warMapManager.Nodes.Count} nodes");
            }
            else
            {
                Debug.LogError("[WarMapTest] ✗ Cannot register - manager null or not server!");
            }
            
            isInitialized = true;
            Debug.Log("[WarMapTest] Real-Time War Map test system ready!");
        }
        
        void EnsureNetworkManager()
        {
            networkManager = FindAnyObjectByType<NetworkManager>();
            
            if (networkManager == null)
            {
                Debug.LogError("[WarMapTest] No NetworkManager found in scene!");
            }
            else
            {
                Debug.Log("[WarMapTest] NetworkManager found!");
            }
        }
        
        void CreateSimpleNodeVisuals()
        {
            if (!NetworkServer.active)
                return;
                
            WarMapNode[] existingNodes = FindObjectsByType<WarMapNode>(FindObjectsSortMode.None);
            
            if (existingNodes.Length == 0)
            {
                Debug.Log("[WarMapTest] Creating test node objects...");
                
                Vector3[] positions = new Vector3[]
                {
                    new Vector3(-5, 0, 0),
                    new Vector3(0, 0, 5),
                    new Vector3(0, 0, 0),
                    new Vector3(0, 0, -5),
                    new Vector3(5, 0, 0)
                };
                
                string[] names = new string[]
                {
                    "Blue Stronghold",
                    "Northern Outpost",
                    "Resource Hub",
                    "Southern Fort",
                    "Red Fortress"
                };
                
                WarMapNode.NodeType[] types = new WarMapNode.NodeType[]
                {
                    WarMapNode.NodeType.Capital,
                    WarMapNode.NodeType.Strategic,
                    WarMapNode.NodeType.Resource,
                    WarMapNode.NodeType.Strategic,
                    WarMapNode.NodeType.Capital
                };
                
                for (int i = 0; i < 5; i++)
                {
                    GameObject nodeGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    nodeGO.name = $"TestNode_{names[i]}";
                    nodeGO.transform.position = positions[i];
                    
                    WarMapNode node = nodeGO.AddComponent<WarMapNode>();
                    nodeGO.AddComponent<NetworkIdentity>();
                    
                    System.Collections.Generic.List<int> connections = new System.Collections.Generic.List<int>();
                    
                    switch (i)
                    {
                        case 0: connections.AddRange(new int[] { 1, 2 }); break;
                        case 1: connections.AddRange(new int[] { 0, 2, 3 }); break;
                        case 2: connections.AddRange(new int[] { 0, 1, 3, 4 }); break;
                        case 3: connections.AddRange(new int[] { 1, 2, 4 }); break;
                        case 4: connections.AddRange(new int[] { 2, 3 }); break;
                    }
                    
                    node.Initialize(i, names[i], types[i], connections);
                    
                    if (i == 0)
                        node.SetControl(Team.Blue, 100f);
                    else if (i == 4)
                        node.SetControl(Team.Red, 100f);
                    else
                        node.SetControl(Team.None, 0f);
                    
                    var renderer = nodeGO.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        UpdateNodeColor(node, renderer);
                    }
                    
                    NetworkServer.Spawn(nodeGO);
                }
                
                Debug.Log("[WarMapTest] Created 5 test nodes");
            }
        }
        
        void UpdateNodeColor(WarMapNode node, Renderer renderer)
        {
            Color color = Color.gray;
            
            switch (node.ControllingFaction)
            {
                case Team.Blue: color = Color.blue; break;
                case Team.Red: color = Color.red; break;
                case Team.Green: color = Color.green; break;
            }
            
            if (node.IsContested) color = Color.yellow;
            if (node.IsBattleActive) color = Color.magenta;
                
            renderer.material.color = color;
        }
        
        void OnGUI()
        {
            if (!showDebugGUI || !isInitialized)
                return;
            
            GUILayout.BeginArea(new Rect(10, 10, guiWidth, Screen.height - 20));
            
            GUILayout.Label("=== REAL-TIME WAR MAP TEST ===");
            GUILayout.Space(10);
            
            // Network Status
            GUILayout.Label("--- NETWORK STATUS ---");
            GUILayout.Label($"Server: {(NetworkServer.active ? "ACTIVE" : "INACTIVE")}");
            GUILayout.Label($"Client: {(NetworkClient.active ? "ACTIVE" : "INACTIVE")}");
            GUILayout.Space(10);
            
            // Token Display
            if (tokenSystem != null)
            {
                GUILayout.Label("--- TOKENS (REAL-TIME) ---");
                GUILayout.Label($"Blue: {tokenSystem.GetFactionTokens(Team.Blue)}");
                GUILayout.Label($"Red: {tokenSystem.GetFactionTokens(Team.Red)}");
                GUILayout.Label($"Green: {tokenSystem.GetFactionTokens(Team.Green)}");
                GUILayout.Space(10);
            }
            
            // War State
            if (warMapManager != null)
            {
                GUILayout.Label("--- WAR STATE ---");
                GUILayout.Label($"State: {warMapManager.CurrentState}");
                GUILayout.Label($"Active Battles: {warMapManager.ActiveBattleCount}");
                GUILayout.Label($"Registered Nodes: {warMapManager.Nodes.Count}");
                GUILayout.Space(10);
            }
            
            // Quick Actions
            GUILayout.Label("--- QUICK ACTIONS ---");
            
            // Faction Selection
            GUILayout.BeginHorizontal();
            GUILayout.Label("Test Faction:");
            if (GUILayout.Button(testFaction.ToString()))
            {
                switch (testFaction)
                {
                    case Team.Blue: testFaction = Team.Red; break;
                    case Team.Red: testFaction = Team.Green; break;
                    default: testFaction = Team.Blue; break;
                }
            }
            GUILayout.EndHorizontal();
            
            // Node Selection
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Node ID: {testNodeID}");
            if (GUILayout.Button("-")) testNodeID = Mathf.Max(0, testNodeID - 1);
            if (GUILayout.Button("+")) testNodeID = Mathf.Min(4, testNodeID + 1);
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // Token Actions
            if (GUILayout.Button($"Add 500 Tokens to {testFaction}"))
            {
                if (NetworkServer.active && tokenSystem != null)
                {
                    tokenSystem.AddTokens(testFaction, 500, "Test Grant");
                    Debug.Log($"Added 500 tokens to {testFaction}");
                }
            }
            
            GUILayout.Space(10);
            
            // Battle Actions
            if (GUILayout.Button($"Attack Node {testNodeID} as {testFaction}"))
            {
                if (warMapManager != null)
                {
                    warMapManager.CmdRequestBattle(testNodeID, testFaction);
                    Debug.Log($"{testFaction} attacking node {testNodeID}");
                }
            }
            
            if (GUILayout.Button("Simulate Battle Victory (Blue)"))
            {
                if (NetworkServer.active && warMapManager != null)
                {
                    var result = new BattleResult
                    {
                        WinnerFaction = Team.Blue,
                        LoserFaction = Team.Red,
                        ControlChange = 100f,
                        TokensWon = 200,
                        TokensLost = 100,
                        PlayersParticipated = 4,
                        BattleDuration = 600f
                    };
                    
                    warMapManager.EndBattle(testNodeID, result);
                    Debug.Log($"Battle at node {testNodeID} ended - Blue victory!");
                }
            }
            
            if (GUILayout.Button("Simulate Battle Victory (Red)"))
            {
                if (NetworkServer.active && warMapManager != null)
                {
                    var result = new BattleResult
                    {
                        WinnerFaction = Team.Red,
                        LoserFaction = Team.Blue,
                        ControlChange = 100f,
                        TokensWon = 200,
                        TokensLost = 100,
                        PlayersParticipated = 4,
                        BattleDuration = 600f
                    };
                    
                    warMapManager.EndBattle(testNodeID, result);
                    Debug.Log($"Battle at node {testNodeID} ended - Red victory!");
                }
            }
            
            GUILayout.Space(10);
            
            // Node Control
            if (GUILayout.Button($"Give Node {testNodeID} to {testFaction}"))
            {
                if (NetworkServer.active)
                {
                    var node = warMapManager?.GetNodeByID(testNodeID);
                    if (node != null)
                    {
                        node.SetControl(testFaction, 100f);
                        UpdateNodeVisual(node);
                        Debug.Log($"Node {testNodeID} given to {testFaction}");
                    }
                }
            }
            
            if (GUILayout.Button($"Contest Node {testNodeID}"))
            {
                if (NetworkServer.active)
                {
                    var node = warMapManager?.GetNodeByID(testNodeID);
                    if (node != null)
                    {
                        node.SetContested(true, testFaction);
                        UpdateNodeVisual(node);
                        Debug.Log($"Node {testNodeID} contested by {testFaction}");
                    }
                }
            }
            
            GUILayout.Space(10);
            
            // War Control
            if (GUILayout.Button("Start War"))
            {
                if (NetworkServer.active && warMapManager != null)
                {
                    warMapManager.StartWar();
                    Debug.Log("War started!");
                }
            }
            
            if (GUILayout.Button($"Force {testFaction} Victory"))
            {
                if (NetworkServer.active)
                {
                    var nodes = FindObjectsByType<WarMapNode>(FindObjectsSortMode.None);
                    int count = 0;
                    foreach (var node in nodes)
                    {
                        if (count < 4)
                        {
                            node.SetControl(testFaction, 100f);
                            UpdateNodeVisual(node);
                            count++;
                        }
                    }
                    Debug.Log($"Forced {testFaction} victory");
                }
            }
            
            GUILayout.Space(10);
            
            // Node Information
            GUILayout.Label("--- NODE STATUS ---");
            var allNodes = FindObjectsByType<WarMapNode>(FindObjectsSortMode.None);
            foreach (var node in allNodes)
            {
                string status = $"{node.NodeID}: {node.NodeName}";
                status += $"\n  {node.ControllingFaction} ({node.ControlPercentage:F0}%)";
                if (node.IsContested) status += " [CONTESTED]";
                if (node.IsBattleActive) status += " [BATTLE]";
                    
                GUILayout.Label(status);
                GUILayout.Space(5);
            }
            
            GUILayout.EndArea();
        }
        
        void UpdateNodeVisual(WarMapNode node)
        {
            var renderer = node.GetComponent<Renderer>();
            if (renderer != null)
            {
                UpdateNodeColor(node, renderer);
            }
        }
    }
}
