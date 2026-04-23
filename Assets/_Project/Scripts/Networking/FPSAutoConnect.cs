using System;
using System.Collections;
using UnityEngine;
using Mirror;
using ElitesAndPawns.Core;
using ElitesAndPawns.WarMap;

namespace ElitesAndPawns.Networking
{
    /// <summary>
    /// Handles FPS game startup when launched from the central server or as a client.
    /// 
    /// THREE modes of operation:
    /// 
    /// 1. DEDICATED SERVER MODE (launched by central RTS server):
    ///    - Runs headless (-batchmode -nographics -dedicated)
    ///    - Initializes BattleManager with parameters from command line
    ///    - Waits for clients to connect
    ///    
    /// 2. CLIENT MODE (launched by ClientBattleRedirector):
    ///    - Connects to the specified server address/port
    ///    - Passes faction info to server
    ///    
    /// 3. STANDALONE/TESTING MODE (direct launch):
    ///    - Starts as host for local testing
    ///    - No battle integration
    /// 
    /// Command line args:
    ///   Dedicated Server:
    ///     -batchmode -nographics -dedicated
    ///     -port [port]
    ///     -node [nodeId]
    ///     -attacker [faction]
    ///     -defender [faction]
    ///     -attackerTickets [count]
    ///     -defenderTickets [count]
    ///     -battleId [id]
    ///     -nodeName "[name]"
    ///     -timeLimit [seconds]
    ///   
    ///   Client:
    ///     -client
    ///     -server [address]
    ///     -port [port]
    ///     -node [nodeId]
    ///     -faction [faction]
    ///     -name "[playerName]"
    /// </summary>
    public class FPSAutoConnect : MonoBehaviour
    {
        #region Configuration
        
        [Header("Network Settings")]
        [Tooltip("Default port for FPS battles")]
        public ushort defaultPort = 7778;
        
        [Header("Startup Settings")]
        [Tooltip("Delay before starting (seconds)")]
        public float startDelay = 0.5f;
        
        [Tooltip("Maximum connection attempts for clients")]
        public int maxAttempts = 5;
        
        [Header("Debug")]
        public bool showDebugUI = true;
        public bool verboseLogging = true;
        
        #endregion
        
        #region Static Properties
        
        public static bool IsHeadless => Application.isBatchMode;
        public static bool IsDedicatedServer { get; private set; }
        public static FPSAutoConnect Instance { get; private set; }
        
        #endregion
        
        #region Parsed Arguments - Server
        
        private int nodeId = -1;
        private FactionType attackingFaction = FactionType.None;
        private FactionType defendingFaction = FactionType.None;
        private int attackerTickets = 100;
        private int defenderTickets = 100;
        private string battleId = "";
        private string nodeName = "";
        private float timeLimit = 900f; // 15 minutes default
        
        #endregion
        
        #region Parsed Arguments - Client
        
        private FactionType clientFaction = FactionType.None;
        private string clientPlayerName = "Soldier";
        private string serverAddress = "localhost";
        
        #endregion
        
        #region Parsed Arguments - Common
        
        private ushort port = 7778;
        #pragma warning disable CS0414 // Field may be unused in some configurations
        private bool forceHost = false;
        #pragma warning restore CS0414
        private bool forceClient = false;
        private bool forceDedicated = false;
        private bool hasLaunchArgs = false;
        
        #endregion
        
        #region State
        
        private int attempts = 0;
        private bool isStarting = false;
        private string lastError = "";
        private string currentMode = "Determining...";
        
        #endregion
        
        #region Properties
        
        public int TargetNodeId => nodeId;
        public FactionType AttackingFaction => attackingFaction;
        public FactionType DefendingFaction => defendingFaction;
        public FactionType ClientFaction => clientFaction;
        public string PlayerName => clientPlayerName;
        public bool WasLaunchedFromRTS => hasLaunchArgs;
        public bool IsHost => NetworkServer.active;
        
        #endregion
        
        #region Unity Lifecycle
        
        void Awake()
        {
            Instance = this;
            Log("Awake called");
            ParseCommandLineArgs();
        }
        
        void Start()
        {
            // Determine mode based on arguments
            if (forceDedicated || (IsHeadless && !forceClient))
            {
                // DEDICATED SERVER MODE
                IsDedicatedServer = true;
                currentMode = "Dedicated Server";
                
                // Console output for headless mode
                Console.WriteLine("========================================");
                Console.WriteLine("  ELITES FPS BATTLE SERVER");
                Console.WriteLine("========================================");
                Console.WriteLine($"  Node: {nodeId} ({nodeName})");
                Console.WriteLine($"  Port: {port}");
                Console.WriteLine($"  Battle: {attackingFaction} vs {defendingFaction}");
                Console.WriteLine($"  Tickets: {attackerTickets} vs {defenderTickets}");
                Console.WriteLine("========================================");
                
                StartCoroutine(StartAsDedicatedServer());
            }
            else if (forceClient)
            {
                // CLIENT MODE - explicitly requested
                currentMode = "Client";
                StartCoroutine(StartAsClient());
            }
            else if (hasLaunchArgs && !string.IsNullOrEmpty(serverAddress))
            {
                // CLIENT MODE - has server address from launch args
                currentMode = "Client (auto)";
                StartCoroutine(StartAsClient());
            }
            else
            {
                // STANDALONE/TESTING MODE - no args, start as host for local testing
                currentMode = "Host (Testing)";
                StartCoroutine(StartAsHost());
            }
        }
        
        #endregion
        
        #region Dedicated Server Mode
        
        IEnumerator StartAsDedicatedServer()
        {
            isStarting = true;
            yield return new WaitForSeconds(startDelay);
            
            Log($"Starting dedicated server on port {port}");
            
            NetworkManager manager = GetNetworkManager();
            if (manager == null)
            {
                LogError("No NetworkManager found!");
                yield break;
            }
            
            // Set port
            if (!SetTransportPort(port))
            {
                LogError("Failed to set transport port!");
            }
            
            // Stop any existing connections
            if (NetworkServer.active || NetworkClient.active)
            {
                manager.StopHost();
                yield return new WaitForSeconds(0.2f);
            }
            
            // Start server
            try
            {
                manager.StartServer();
                Log($"Server started on port {port}");
            }
            catch (Exception e)
            {
                lastError = $"StartServer failed: {e.Message}";
                LogError(lastError);
                yield break;
            }
            
            yield return new WaitForSeconds(0.3f);
            
            if (!NetworkServer.active)
            {
                LogError("Server failed to start!");
                yield break;
            }
            
            // Initialize BattleManager with our parameters
            InitializeBattleManager();
            
            Console.WriteLine("Server started. Waiting for connections...");
            Log("Dedicated server ready!");
            
            isStarting = false;
        }
        
        private void InitializeBattleManager()
        {
            // Create battle parameters from command line args
            var parameters = new BattleParameters
            {
                BattleId = battleId,
                NodeId = nodeId,
                NodeName = nodeName,
                AttackingFaction = attackingFaction,
                DefendingFaction = defendingFaction,
                AttackerSpawnTickets = attackerTickets,
                DefenderSpawnTickets = defenderTickets,
                TimeLimit = timeLimit
            };
            
            // Find or wait for BattleManager
            StartCoroutine(WaitAndInitializeBattleManager(parameters));
        }
        
        IEnumerator WaitAndInitializeBattleManager(BattleParameters parameters)
        {
            float timeout = 5f;
            float elapsed = 0f;
            
            while (BattleManager.Instance == null && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            
            if (BattleManager.Instance != null)
            {
                BattleManager.Instance.InitializeBattle(parameters);
                Log("BattleManager initialized with battle parameters");
                
                // Auto-start battle after short delay (or wait for lobby countdown)
                yield return new WaitForSeconds(2f);
                
                // If we have players, start the battle
                if (NetworkServer.connections.Count > 0)
                {
                    BattleManager.Instance.StartBattle();
                }
            }
            else
            {
                LogWarning("BattleManager not found - battle integration disabled");
            }
        }
        
        #endregion
        
        #region Client Mode
        
        IEnumerator StartAsClient()
        {
            isStarting = true;
            yield return new WaitForSeconds(startDelay);
            
            Log($"Connecting as client to {serverAddress}:{port}");
            
            while (attempts < maxAttempts && !NetworkClient.isConnected)
            {
                attempts++;
                Log($"=== Connection attempt {attempts}/{maxAttempts} ===");
                
                NetworkManager manager = GetNetworkManager();
                if (manager == null)
                {
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }
                
                // Set address and port
                manager.networkAddress = serverAddress;
                SetTransportPort(port);
                
                // Stop existing connections
                if (NetworkClient.active)
                {
                    manager.StopClient();
                    yield return new WaitForSeconds(0.2f);
                }
                
                // Connect
                try
                {
                    manager.StartClient();
                    Log($"Connecting to {serverAddress}:{port}...");
                }
                catch (Exception e)
                {
                    lastError = $"StartClient failed: {e.Message}";
                    LogError(lastError);
                }
                
                // Wait for connection
                float timeout = 3f;
                float elapsed = 0f;
                while (!NetworkClient.isConnected && elapsed < timeout)
                {
                    yield return new WaitForSeconds(0.1f);
                    elapsed += 0.1f;
                }
                
                if (NetworkClient.isConnected)
                {
                    Log("Connected to server!");
                    lastError = "";
                    
                    // Set our faction info on our player
                    SetupLocalPlayer();
                    break;
                }
                else
                {
                    lastError = $"Connection failed (attempt {attempts})";
                    LogError(lastError);
                }
                
                yield return new WaitForSeconds(0.5f);
            }
            
            isStarting = false;
            
            if (!NetworkClient.isConnected)
            {
                LogError($"Failed to connect after {maxAttempts} attempts!");
            }
        }
        
        private void SetupLocalPlayer()
        {
            StartCoroutine(WaitAndSetupPlayer());
        }
        
        IEnumerator WaitAndSetupPlayer()
        {
            // Wait for local player to spawn
            float timeout = 5f;
            float elapsed = 0f;
            
            while (NetworkClient.localPlayer == null && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            
            if (NetworkClient.localPlayer != null)
            {
                var networkPlayer = NetworkClient.localPlayer.GetComponent<NetworkPlayer>();
                if (networkPlayer != null)
                {
                    // Request faction assignment based on our launch args
                    networkPlayer.CmdSetPlayerName(clientPlayerName);
                    
                    // The server will assign faction based on battle parameters
                    // But we can request a specific faction if needed
                    if (clientFaction != FactionType.None)
                    {
                        FactionType factionType = clientFaction == FactionType.Blue ? FactionType.Blue :
                                                  clientFaction == FactionType.Red ? FactionType.Red : FactionType.None;
                        networkPlayer.CmdRequestFaction(factionType);
                    }
                    
                    Log($"Player setup complete: {clientPlayerName} ({clientFaction})");
                }
            }
            else
            {
                LogWarning("Local player not spawned!");
            }
        }
        
        #endregion
        
        #region Host Mode (Testing)
        
        IEnumerator StartAsHost()
        {
            isStarting = true;
            yield return new WaitForSeconds(startDelay);
            
            Log("Starting as host (testing mode)");
            
            NetworkManager manager = GetNetworkManager();
            if (manager == null)
            {
                LogError("No NetworkManager found!");
                yield break;
            }
            
            SetTransportPort(port);
            
            if (NetworkServer.active || NetworkClient.active)
            {
                manager.StopHost();
                yield return new WaitForSeconds(0.2f);
            }
            
            try
            {
                manager.StartHost();
                Log("Host started");
            }
            catch (Exception e)
            {
                lastError = $"StartHost failed: {e.Message}";
                LogError(lastError);
            }
            
            isStarting = false;
        }
        
        #endregion
        
        #region Helpers
        
        NetworkManager GetNetworkManager()
        {
            NetworkManager manager = NetworkManager.singleton;
            if (manager == null)
            {
                manager = FindAnyObjectByType<NetworkManager>();
            }
            
            if (manager == null)
            {
                lastError = "No NetworkManager found!";
                LogError(lastError);
            }
            
            return manager;
        }
        
        bool SetTransportPort(ushort targetPort)
        {
            var transport = Transport.active;
            if (transport == null)
            {
                LogError("No Transport.active!");
                return false;
            }
            
            var portField = transport.GetType().GetField("port") ?? 
                           transport.GetType().GetField("Port");
            if (portField != null)
            {
                portField.SetValue(transport, targetPort);
                Log($"Transport port set to {targetPort}");
                return true;
            }
            
            var portProp = transport.GetType().GetProperty("Port");
            if (portProp != null && portProp.CanWrite)
            {
                portProp.SetValue(transport, targetPort);
                return true;
            }
            
            LogWarning($"Could not set port on {transport.GetType().Name}");
            return false;
        }
        
        #endregion
        
        #region Command Line Parsing
        
        private void ParseCommandLineArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            port = defaultPort;
            
            Log($"Args: {string.Join(" ", args)}");
            
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLower();
                bool hasNext = i + 1 < args.Length;
                
                switch (arg)
                {
                    // Common args
                    case "-port":
                        if (hasNext && ushort.TryParse(args[++i], out ushort p))
                        {
                            port = p;
                            hasLaunchArgs = true;
                        }
                        break;
                        
                    case "-node":
                        if (hasNext && int.TryParse(args[++i], out int n))
                        {
                            nodeId = n;
                            hasLaunchArgs = true;
                        }
                        break;
                    
                    // Server args
                    case "-dedicated":
                        forceDedicated = true;
                        hasLaunchArgs = true;
                        break;
                        
                    case "-attacker":
                        if (hasNext && Enum.TryParse<FactionType>(args[++i], true, out FactionType att))
                        {
                            attackingFaction = att;
                            hasLaunchArgs = true;
                        }
                        break;
                        
                    case "-defender":
                        if (hasNext && Enum.TryParse<FactionType>(args[++i], true, out FactionType def))
                        {
                            defendingFaction = def;
                            hasLaunchArgs = true;
                        }
                        break;
                        
                    case "-attackertickets":
                        if (hasNext && int.TryParse(args[++i], out int at))
                        {
                            attackerTickets = at;
                            hasLaunchArgs = true;
                        }
                        break;
                        
                    case "-defendertickets":
                        if (hasNext && int.TryParse(args[++i], out int dt))
                        {
                            defenderTickets = dt;
                            hasLaunchArgs = true;
                        }
                        break;
                        
                    case "-battleid":
                        if (hasNext)
                        {
                            battleId = args[++i].Trim('"');
                            hasLaunchArgs = true;
                        }
                        break;
                        
                    case "-nodename":
                        if (hasNext)
                        {
                            nodeName = args[++i].Trim('"');
                            hasLaunchArgs = true;
                        }
                        break;
                        
                    case "-timelimit":
                        if (hasNext && float.TryParse(args[++i], out float tl))
                        {
                            timeLimit = tl;
                            hasLaunchArgs = true;
                        }
                        break;
                    
                    // Client args
                    case "-client":
                        forceClient = true;
                        hasLaunchArgs = true;
                        break;
                        
                    case "-server":
                        if (hasNext)
                        {
                            serverAddress = args[++i];
                            hasLaunchArgs = true;
                        }
                        break;
                        
                    case "-faction":
                        if (hasNext && Enum.TryParse<FactionType>(args[++i], true, out FactionType f))
                        {
                            clientFaction = f;
                            hasLaunchArgs = true;
                        }
                        break;
                        
                    case "-name":
                        if (hasNext)
                        {
                            clientPlayerName = args[++i].Trim('"');
                            hasLaunchArgs = true;
                        }
                        break;
                    
                    // Legacy/testing
                    case "-host":
                        forceHost = true;
                        hasLaunchArgs = true;
                        break;
                }
            }
            
            Log($"Parsed - Mode: {(forceDedicated ? "Dedicated" : forceClient ? "Client" : "Host")}");
            Log($"  Port: {port}, Node: {nodeId}");
            Log($"  Server: {attackingFaction} vs {defendingFaction} ({attackerTickets}/{defenderTickets})");
            Log($"  Client: {clientFaction} as '{clientPlayerName}' -> {serverAddress}");
        }
        
        #endregion
        
        #region Debug UI
        
        void OnGUI()
        {
            if (!showDebugUI || IsHeadless) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 380, 420));
            GUILayout.BeginVertical(GUI.skin.box);
            
            GUILayout.Label("FPS Battle", GUI.skin.box);
            
            // Mode info
            GUILayout.Label($"Mode: {currentMode}");
            GUILayout.Label($"Port: {port}");
            
            if (hasLaunchArgs)
            {
                if (IsDedicatedServer)
                {
                    GUILayout.Label($"Node: {nodeId} ({nodeName})");
                    GUILayout.Label($"Battle: {attackingFaction} vs {defendingFaction}");
                }
                else
                {
                    GUILayout.Label($"Player: {clientPlayerName} ({clientFaction})");
                    GUILayout.Label($"Server: {serverAddress}");
                }
            }
            else
            {
                GUILayout.Label("Standalone Mode (testing)");
            }
            
            GUILayout.Space(5);
            
            // Connection status
            if (NetworkServer.active)
            {
                GUI.color = Color.green;
                GUILayout.Label($"✓ SERVER RUNNING ({NetworkServer.connections.Count} clients)");
                GUI.color = Color.white;
            }
            
            if (NetworkClient.isConnected)
            {
                GUI.color = Color.green;
                GUILayout.Label(NetworkServer.active ? "✓ Host Connected" : "✓ Connected to Server");
                GUI.color = Color.white;
                
                if (NetworkClient.localPlayer != null)
                {
                    var pos = NetworkClient.localPlayer.transform.position;
                    GUILayout.Label($"✓ Playing at ({pos.x:F0}, {pos.y:F0}, {pos.z:F0})");
                }
            }
            else if (isStarting)
            {
                GUI.color = Color.yellow;
                GUILayout.Label($"⏳ {currentMode}... ({attempts}/{maxAttempts})");
                GUI.color = Color.white;
            }
            
            // Error
            if (!string.IsNullOrEmpty(lastError))
            {
                GUI.color = Color.red;
                GUILayout.Label($"Error: {lastError}");
                GUI.color = Color.white;
            }
            
            GUILayout.Space(10);
            GUILayout.Label("─── Controls ───");
            GUILayout.Label("WASD: Move | Mouse: Look | LMB: Shoot");
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        #endregion
        
        #region Logging
        
        private void Log(string msg)
        {
            if (verboseLogging) Debug.Log($"[FPSAutoConnect] {msg}");
        }
        
        private void LogWarning(string msg)
        {
            Debug.LogWarning($"[FPSAutoConnect] {msg}");
        }
        
        private void LogError(string msg)
        {
            Debug.LogError($"[FPSAutoConnect] {msg}");
        }
        
        #endregion
    }
}
