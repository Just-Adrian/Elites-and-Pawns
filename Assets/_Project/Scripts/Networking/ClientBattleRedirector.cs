using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Mirror;
using ElitesAndPawns.Core;
using ElitesAndPawns.WarMap;
using Debug = UnityEngine.Debug;

namespace ElitesAndPawns.Networking
{
    // This component only fully compiles for Client builds and Editor
    // In server builds, it becomes an empty stub (servers don't need to redirect to FPS)
#if !UNITY_SERVER || UNITY_EDITOR
    /// <summary>
    /// CLIENT-SIDE: Handles redirecting the player to FPS battles.
    /// 
    /// When the server notifies us that a battle server is ready and we're
    /// participating in that battle, this component:
    /// 1. Launches the FPS client executable
    /// 2. Passes connection info (server address, port, faction, etc.)
    /// 3. Optionally minimizes/hides the RTS client during battle
    /// 
    /// The FPS client is a separate process - this allows:
    /// - Better performance (FPS and RTS don't share resources)
    /// - Clean separation of concerns
    /// - Players can alt-tab back to RTS while waiting to respawn
    /// </summary>
    public class ClientBattleRedirector : MonoBehaviour
    {
        #region Singleton
        
        private static ClientBattleRedirector _instance;
        public static ClientBattleRedirector Instance => _instance;
        
        #endregion
        
        #region Configuration
        
        [Header("FPS Client Configuration")]
        [Tooltip("Path to FPS client executable relative to RTS client")]
        #pragma warning disable CS0414 // Field assigned but never used - used in builds
        [SerializeField] private string fpsClientRelativePath = "../FPS/ElitesFPS.exe";
        #pragma warning restore CS0414
        
        [Tooltip("Absolute path for editor testing")]
        [SerializeField] private string editorFpsClientPath = "";
        
        [Header("Behavior")]
        [Tooltip("Automatically join battles when server notifies us")]
        [SerializeField] private bool autoJoinBattles = true;
        
        [Tooltip("Minimize RTS window when FPS launches")]
        [SerializeField] private bool minimizeOnBattleStart = false;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = true;
        [SerializeField] private bool showDebugUI = true;
        
        #endregion
        
        #region State
        
        /// <summary>
        /// Which node battles we're currently eligible to join
        /// </summary>
        private int currentBattleNode = -1;
        
        /// <summary>
        /// Our faction (for passing to FPS client)
        /// </summary>
        private FactionType myFaction = FactionType.None;
        
        /// <summary>
        /// Player display name
        /// </summary>
        private string myPlayerName = "Soldier";
        
        /// <summary>
        /// Currently active FPS client process
        /// </summary>
        private Process activeFPSProcess;
        
        /// <summary>
        /// Battle server info for UI display
        /// </summary>
        private string currentBattleInfo = "No battle";
        
        /// <summary>
        /// Whether we're waiting to join a battle
        /// </summary>
        private bool isWaitingForBattle = false;
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when we successfully launch the FPS client
        /// </summary>
        public static event Action<int> OnFPSClientLaunched; // nodeId
        
        /// <summary>
        /// Fired when the FPS client process exits
        /// </summary>
        public static event Action<int> OnFPSClientExited; // nodeId
        
        #endregion
        
        #region Properties
        
        public bool IsInBattle => activeFPSProcess != null && !activeFPSProcess.HasExited;
        public int CurrentBattleNode => currentBattleNode;
        
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
        
        void Start()
        {
            // Subscribe to battle server notifications
            DedicatedServerLauncher.OnBattleServerReady += OnBattleServerReady;
            DedicatedServerLauncher.OnBattleServerStopped += OnBattleServerStopped;
        }
        
        void Update()
        {
            // Monitor FPS process
            if (activeFPSProcess != null)
            {
                if (activeFPSProcess.HasExited)
                {
                    Log($"FPS client exited (node {currentBattleNode})");
                    OnFPSClientExited?.Invoke(currentBattleNode);
                    
                    activeFPSProcess.Dispose();
                    activeFPSProcess = null;
                    currentBattleNode = -1;
                    currentBattleInfo = "Battle ended";
                }
            }
        }
        
        void OnDestroy()
        {
            DedicatedServerLauncher.OnBattleServerReady -= OnBattleServerReady;
            DedicatedServerLauncher.OnBattleServerStopped -= OnBattleServerStopped;
            
            // Clean up FPS process
            if (activeFPSProcess != null && !activeFPSProcess.HasExited)
            {
                try
                {
                    activeFPSProcess.Kill();
                    activeFPSProcess.Dispose();
                }
                catch { }
            }
            
            if (_instance == this)
                _instance = null;
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Set this client's faction and player info.
        /// Called when we connect to the RTS server and get assigned.
        /// </summary>
        public void SetPlayerInfo(FactionType faction, string playerName)
        {
            myFaction = faction;
            myPlayerName = playerName;
            Log($"Player info set: {playerName} ({faction})");
        }
        
        /// <summary>
        /// Mark that we want to join a specific battle.
        /// Called when player commits squads to a contested node.
        /// </summary>
        public void RegisterForBattle(int nodeId)
        {
            isWaitingForBattle = true;
            currentBattleNode = nodeId;
            currentBattleInfo = $"Waiting for battle at node {nodeId}...";
            Log($"Registered for battle at node {nodeId}");
        }
        
        /// <summary>
        /// Cancel waiting for a battle.
        /// </summary>
        public void UnregisterFromBattle(int nodeId)
        {
            if (currentBattleNode == nodeId)
            {
                isWaitingForBattle = false;
                currentBattleNode = -1;
                currentBattleInfo = "No battle";
                Log($"Unregistered from battle at node {nodeId}");
            }
        }
        
        /// <summary>
        /// Manually launch FPS client for a battle.
        /// </summary>
        public void JoinBattle(int nodeId, string serverAddress, ushort port)
        {
            if (IsInBattle)
            {
                LogWarning("Already in a battle!");
                return;
            }
            
            LaunchFPSClient(nodeId, serverAddress, port);
        }
        
        /// <summary>
        /// Request to join an active battle (queries server for info).
        /// </summary>
        [Client]
        public void RequestJoinBattle(int nodeId)
        {
            // Ask server for battle server info
            var networkPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
            if (networkPlayer != null)
            {
                networkPlayer.CmdRequestBattleServerInfo(nodeId);
            }
        }
        
        /// <summary>
        /// Called by WarMapManager when it receives notification that a battle server is ready.
        /// This is the public entry point for RPC-based notifications.
        /// </summary>
        public void OnBattleServerReadyNotification(int nodeId, string address, ushort port)
        {
            OnBattleServerReady(nodeId, address, port);
        }
        
        #endregion
        
        #region Event Handlers
        
        private void OnBattleServerReady(int nodeId, string address, ushort port)
        {
            Log($"Battle server ready notification: node {nodeId} at {address}:{port}");
            
            currentBattleInfo = $"Battle ready at node {nodeId} ({address}:{port})";
            
            // Auto-join if we're waiting for this battle
            if (autoJoinBattles && isWaitingForBattle && currentBattleNode == nodeId)
            {
                Log("Auto-joining battle...");
                LaunchFPSClient(nodeId, address, port);
            }
        }
        
        private void OnBattleServerStopped(int nodeId)
        {
            Log($"Battle server stopped notification: node {nodeId}");
            
            if (currentBattleNode == nodeId)
            {
                isWaitingForBattle = false;
                currentBattleInfo = $"Battle at node {nodeId} ended";
            }
        }
        
        #endregion
        
        #region FPS Client Launching
        
        private void LaunchFPSClient(int nodeId, string serverAddress, ushort port)
        {
            string exePath = GetFPSClientPath();
            
            if (!ValidateExecutable(exePath))
            {
                return;
            }
            
            // Build arguments
            string args = BuildClientArgs(nodeId, serverAddress, port);
            
            Log($"Launching FPS client: {exePath}");
            Log($"Arguments: {args}");
            
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    UseShellExecute = true, // Allow window to show
                    WorkingDirectory = Path.GetDirectoryName(exePath)
                };
                
                activeFPSProcess = Process.Start(startInfo);
                
                if (activeFPSProcess == null)
                {
                    LogError("Failed to start FPS client!");
                    return;
                }
                
                currentBattleNode = nodeId;
                isWaitingForBattle = false;
                currentBattleInfo = $"In battle at node {nodeId}";
                
                Log($"FPS client launched (PID: {activeFPSProcess.Id})");
                OnFPSClientLaunched?.Invoke(nodeId);
                
                // Minimize RTS if configured
                if (minimizeOnBattleStart)
                {
                    // Note: This requires platform-specific code
                    // For now, just log
                    Log("Would minimize RTS window here");
                }
            }
            catch (Exception e)
            {
                LogError($"Exception launching FPS client: {e.Message}");
            }
        }
        
        private string BuildClientArgs(int nodeId, string serverAddress, ushort port)
        {
            return $"-client " +
                   $"-server {serverAddress} " +
                   $"-port {port} " +
                   $"-node {nodeId} " +
                   $"-faction {myFaction} " +
                   $"-name \"{myPlayerName}\"";
        }
        
        #endregion
        
        #region Path Helpers
        
        private string GetFPSClientPath()
        {
            #if UNITY_EDITOR
            if (!string.IsNullOrEmpty(editorFpsClientPath) && File.Exists(editorFpsClientPath))
                return editorFpsClientPath;
            
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string relativePath = Path.Combine(projectPath, "Builds", "FPS", "ElitesFPS.exe");
            if (File.Exists(relativePath))
                return relativePath;
            
            return editorFpsClientPath;
            #else
            string ourFolder = Path.GetDirectoryName(Application.dataPath);
            return Path.GetFullPath(Path.Combine(ourFolder, fpsClientRelativePath));
            #endif
        }
        
        private bool ValidateExecutable(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                LogError("FPS client path not configured!");
                
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.DisplayDialog(
                    "FPS Build Not Found",
                    "Please configure the FPS client path in ClientBattleRedirector,\n" +
                    "or build the FPS scene to Builds/FPS/",
                    "OK"
                );
                #endif
                
                return false;
            }
            
            if (!File.Exists(path))
            {
                LogError($"FPS client not found: {path}");
                return false;
            }
            
            return true;
        }
        
        #endregion
        
        #region Debug UI
        
        void OnGUI()
        {
            if (!showDebugUI) return;
            
            // Position in bottom-left to avoid overlapping squad menu (top-right)
            GUILayout.BeginArea(new Rect(10, Screen.height - 210, 250, 200));
            GUILayout.BeginVertical(GUI.skin.box);
            
            GUILayout.Label("Battle Redirector", GUI.skin.box);
            
            GUILayout.Label($"Faction: {myFaction}");
            GUILayout.Label($"Status: {currentBattleInfo}");
            
            if (IsInBattle)
            {
                GUI.color = Color.green;
                GUILayout.Label("✓ In FPS Battle");
                GUI.color = Color.white;
            }
            else if (isWaitingForBattle)
            {
                GUI.color = Color.yellow;
                GUILayout.Label("⏳ Waiting for battle...");
                GUI.color = Color.white;
            }
            
            GUILayout.Space(5);
            
            // Manual join button for testing
            if (!IsInBattle)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Test Join (7780)"))
                {
                    JoinBattle(0, "localhost", 7780);
                }
                if (GUILayout.Button("Test Join (7781)"))
                {
                    JoinBattle(1, "localhost", 7781);
                }
                GUILayout.EndHorizontal();
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        #endregion
        
        #region Logging
        
        private void Log(string msg)
        {
            if (debugMode) Debug.Log($"[ClientBattleRedirector] {msg}");
        }
        
        private void LogWarning(string msg)
        {
            Debug.LogWarning($"[ClientBattleRedirector] {msg}");
        }
        
        private void LogError(string msg)
        {
            Debug.LogError($"[ClientBattleRedirector] {msg}");
        }
        
        #endregion
    }
#else
    // Stub for server builds - servers don't need battle redirection
    public class ClientBattleRedirector : MonoBehaviour
    {
        public static ClientBattleRedirector Instance => null;
        public static event Action<int> OnFPSClientLaunched;
        public static event Action<int> OnFPSClientExited;
        
        public bool IsInBattle => false;
        public int CurrentBattleNode => -1;
        
        public void SetPlayerInfo(FactionType faction, string playerName) { }
        public void RegisterForBattle(int nodeId) { }
        public void UnregisterFromBattle(int nodeId) { }
        public void JoinBattle(int nodeId, string serverAddress, ushort port) { }
        public void RequestJoinBattle(int nodeId) { }
        public void OnBattleServerReadyNotification(int nodeId, string address, ushort port) { }
    }
#endif
}
