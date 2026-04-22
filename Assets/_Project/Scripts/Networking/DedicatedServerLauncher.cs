using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Mirror;
using ElitesAndPawns.Core;
using ElitesAndPawns.WarMap;
using Debug = UnityEngine.Debug;

namespace ElitesAndPawns.Networking
{
    // This component only compiles for Server builds and Editor
    // In client builds, it becomes an empty stub
#if UNITY_SERVER || UNITY_EDITOR
    /// <summary>
    /// SERVER-SIDE ONLY: Manages spawning and tracking of FPS battle server processes.
    /// 
    /// This runs on the central RTS server and:
    /// 1. Spawns dedicated FPS server processes when battles start
    /// 2. Tracks active battle servers (port, node, status)
    /// 3. Provides connection info to clients so they can join battles
    /// 4. Cleans up FPS servers when battles end
    /// 
    /// Architecture:
    ///   Central Server (RTS)  ──spawns──>  FPS Server Process (per battle)
    ///          │                                    │
    ///          │                                    │
    ///          └───────> Client connects to FPS ────┘
    /// 
    /// Note: This is a MonoBehaviour, not NetworkBehaviour, because it only
    /// runs on the server and doesn't need to sync state to clients.
    /// Client notifications go through WarMapManager's RPCs instead.
    /// </summary>
    public class DedicatedServerLauncher : MonoBehaviour
    {
        #region Singleton
        
        private static DedicatedServerLauncher _instance;
        public static DedicatedServerLauncher Instance => _instance;
        
        #endregion
        
        #region Configuration
        
        [Header("FPS Server Configuration")]
        [Tooltip("Path to FPS server executable relative to RTS server")]
        #pragma warning disable CS0414 // Field assigned but never used - used in builds
        [SerializeField] private string fpsServerRelativePath = "../FPS/ElitesFPS.exe";
        #pragma warning restore CS0414
        
        [Tooltip("Absolute path for editor testing")]
        [SerializeField] private string editorFpsServerPath = "";
        
        [Header("Network Configuration")]
        [Tooltip("Base port for FPS battle servers")]
        [SerializeField] private ushort basePort = 7780;
        
        [Tooltip("Maximum concurrent FPS battles")]
        [SerializeField] private int maxConcurrentBattles = 5;
        
        [Tooltip("IP/hostname clients should connect to (use public IP for internet play)")]
        [SerializeField] private string serverPublicAddress = "localhost";
        
        [Header("Process Management")]
        [Tooltip("Wait time for FPS server to start before notifying clients (seconds)")]
        [SerializeField] private float serverStartupDelay = 2.0f;
        
        [Tooltip("Timeout for FPS server to respond (seconds)")]
        [SerializeField] private float serverStartupTimeout = 10.0f;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = true;
        
        #endregion
        
        #region State
        
        /// <summary>
        /// Active FPS server processes, keyed by node ID
        /// </summary>
        private Dictionary<int, FPSServerInstance> activeServers = new Dictionary<int, FPSServerInstance>();
        
        /// <summary>
        /// Port allocation tracking
        /// </summary>
        private HashSet<ushort> usedPorts = new HashSet<ushort>();
        
        /// <summary>
        /// Pending battle starts (waiting for server to be ready)
        /// </summary>
        private Dictionary<int, PendingBattle> pendingBattles = new Dictionary<int, PendingBattle>();
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when an FPS server is ready for clients to connect
        /// </summary>
        public static event Action<int, string, ushort> OnBattleServerReady; // nodeId, address, port
        
        /// <summary>
        /// Fired when an FPS server shuts down
        /// </summary>
        public static event Action<int> OnBattleServerStopped; // nodeId
        
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
            // Only runs on server builds anyway due to #if UNITY_SERVER
            
            // Check pending battles for timeout
            CheckPendingBattles();
            
            // Monitor active server processes
            MonitorActiveServers();
        }
        
        void OnDestroy()
        {
            // Clean up all spawned processes on shutdown
            ShutdownAllServers();
            
            if (_instance == this)
                _instance = null;
        }
        
        void OnApplicationQuit()
        {
            ShutdownAllServers();
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Start a new FPS battle server for a contested node.
        /// Called by WarMapManager when a battle should begin.
        /// Server-only: Only call this on the server.
        /// </summary>
        public bool StartBattleServer(BattleParameters parameters)
        {
            if (parameters == null)
            {
                LogError("Cannot start battle server - parameters are null!");
                return false;
            }
            
            int nodeId = parameters.NodeId;
            
            // Check if battle already exists
            if (activeServers.ContainsKey(nodeId))
            {
                LogWarning($"Battle server already running for node {nodeId}");
                return false;
            }
            
            // Check concurrent limit
            if (activeServers.Count >= maxConcurrentBattles)
            {
                LogWarning($"Maximum concurrent battles reached ({maxConcurrentBattles})");
                return false;
            }
            
            // Allocate port
            ushort port = AllocatePort();
            if (port == 0)
            {
                LogError("No available ports for FPS server!");
                return false;
            }
            
            // Get executable path
            string exePath = GetFPSServerPath();
            if (!ValidateExecutable(exePath))
            {
                usedPorts.Remove(port);
                return false;
            }
            
            // Build command line arguments for the FPS server
            string args = BuildServerArgs(parameters, port);
            
            Log($"Starting FPS server for node {nodeId} on port {port}");
            Log($"  Executable: {exePath}");
            Log($"  Arguments: {args}");
            
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = !debugMode, // Show window in debug mode
                    WorkingDirectory = Path.GetDirectoryName(exePath),
                    RedirectStandardOutput = debugMode,
                    RedirectStandardError = debugMode
                };
                
                Process process = Process.Start(startInfo);
                
                if (process == null)
                {
                    LogError("Failed to start FPS server process!");
                    usedPorts.Remove(port);
                    return false;
                }
                
                // Track the server instance
                var serverInstance = new FPSServerInstance
                {
                    NodeId = nodeId,
                    Port = port,
                    Process = process,
                    Parameters = parameters,
                    StartTime = Time.time,
                    Status = FPSServerStatus.Starting
                };
                
                activeServers[nodeId] = serverInstance;
                
                // Add to pending (waiting for server to be ready)
                pendingBattles[nodeId] = new PendingBattle
                {
                    NodeId = nodeId,
                    Port = port,
                    StartTime = Time.time
                };
                
                // Start coroutine to check when server is ready
                StartCoroutine(WaitForServerReady(nodeId, port));
                
                Log($"FPS server process started (PID: {process.Id})");
                return true;
            }
            catch (Exception e)
            {
                LogError($"Exception starting FPS server: {e.Message}");
                usedPorts.Remove(port);
                return false;
            }
        }
        
        /// <summary>
        /// Stop a battle server when the battle ends.
        /// Server-only: Only call this on the server.
        /// </summary>
        public void StopBattleServer(int nodeId)
        {
            if (!activeServers.TryGetValue(nodeId, out FPSServerInstance server))
            {
                LogWarning($"No active server for node {nodeId}");
                return;
            }
            
            Log($"Stopping FPS server for node {nodeId}");
            
            // Release port
            usedPorts.Remove(server.Port);
            
            // Kill process
            try
            {
                if (server.Process != null && !server.Process.HasExited)
                {
                    server.Process.Kill();
                    server.Process.Dispose();
                }
            }
            catch (Exception e)
            {
                LogWarning($"Error killing FPS server process: {e.Message}");
            }
            
            activeServers.Remove(nodeId);
            pendingBattles.Remove(nodeId);
            
            // Notify via event (WarMapManager will relay to clients)
            OnBattleServerStopped?.Invoke(nodeId);
        }
        
        /// <summary>
        /// Get connection info for a battle server.
        /// Returns null if no server is running for that node.
        /// </summary>
        public (string address, ushort port)? GetBattleServerInfo(int nodeId)
        {
            if (activeServers.TryGetValue(nodeId, out FPSServerInstance server))
            {
                if (server.Status == FPSServerStatus.Ready)
                {
                    return (serverPublicAddress, server.Port);
                }
            }
            return null;
        }
        
        /// <summary>
        /// Check if a battle server is ready for connections.
        /// </summary>
        public bool IsBattleServerReady(int nodeId)
        {
            return activeServers.TryGetValue(nodeId, out FPSServerInstance server) 
                   && server.Status == FPSServerStatus.Ready;
        }
        
        /// <summary>
        /// Set the public address clients should use to connect.
        /// </summary>
        public void SetPublicAddress(string address)
        {
            serverPublicAddress = address;
            Log($"Public address set to: {address}");
        }
        
        #endregion
        
        #region Server Startup & Monitoring
        
        private System.Collections.IEnumerator WaitForServerReady(int nodeId, ushort port)
        {
            // Wait for server startup
            yield return new WaitForSeconds(serverStartupDelay);
            
            if (!activeServers.TryGetValue(nodeId, out FPSServerInstance server))
            {
                yield break; // Server was stopped
            }
            
            // Check if process is still running
            if (server.Process == null || server.Process.HasExited)
            {
                LogError($"FPS server for node {nodeId} crashed during startup!");
                CleanupFailedServer(nodeId);
                yield break;
            }
            
            // Mark as ready
            server.Status = FPSServerStatus.Ready;
            pendingBattles.Remove(nodeId);
            
            Log($"FPS server for node {nodeId} is READY on port {port}");
            
            // Notify everyone via event (WarMapManager will relay to clients)
            OnBattleServerReady?.Invoke(nodeId, serverPublicAddress, port);
        }
        
        private void CheckPendingBattles()
        {
            var timedOut = new List<int>();
            
            foreach (var kvp in pendingBattles)
            {
                if (Time.time - kvp.Value.StartTime > serverStartupTimeout)
                {
                    timedOut.Add(kvp.Key);
                }
            }
            
            foreach (int nodeId in timedOut)
            {
                LogError($"FPS server for node {nodeId} timed out during startup");
                CleanupFailedServer(nodeId);
            }
        }
        
        private void MonitorActiveServers()
        {
            var crashed = new List<int>();
            
            foreach (var kvp in activeServers)
            {
                var server = kvp.Value;
                
                if (server.Process == null || server.Process.HasExited)
                {
                    if (server.Status == FPSServerStatus.Ready)
                    {
                        LogWarning($"FPS server for node {kvp.Key} has exited unexpectedly");
                        crashed.Add(kvp.Key);
                    }
                }
            }
            
            foreach (int nodeId in crashed)
            {
                CleanupFailedServer(nodeId);
            }
        }
        
        private void CleanupFailedServer(int nodeId)
        {
            if (activeServers.TryGetValue(nodeId, out FPSServerInstance server))
            {
                usedPorts.Remove(server.Port);
                
                try
                {
                    server.Process?.Dispose();
                }
                catch { }
                
                activeServers.Remove(nodeId);
            }
            
            pendingBattles.Remove(nodeId);
            
            // Notify WarMap that battle failed to start
            if (WarMapManager.Instance != null)
            {
                WarMapManager.Instance.EndBattle(nodeId, new BattleResult 
                { 
                    WinnerFaction = Team.None,
                    ControlChange = 0
                });
            }
            
            OnBattleServerStopped?.Invoke(nodeId);
        }
        
        private void ShutdownAllServers()
        {
            Log("Shutting down all FPS servers...");
            
            foreach (var kvp in activeServers)
            {
                try
                {
                    if (kvp.Value.Process != null && !kvp.Value.Process.HasExited)
                    {
                        kvp.Value.Process.Kill();
                        kvp.Value.Process.Dispose();
                    }
                }
                catch (Exception e)
                {
                    LogWarning($"Error shutting down server for node {kvp.Key}: {e.Message}");
                }
            }
            
            activeServers.Clear();
            pendingBattles.Clear();
            usedPorts.Clear();
        }
        
        #endregion
        
        #region Port Allocation
        
        private ushort AllocatePort()
        {
            for (ushort port = basePort; port < basePort + 100; port++)
            {
                if (!usedPorts.Contains(port))
                {
                    usedPorts.Add(port);
                    return port;
                }
            }
            return 0; // No port available
        }
        
        #endregion
        
        #region Argument Building
        
        private string BuildServerArgs(BattleParameters parameters, ushort port)
        {
            // Arguments for dedicated server mode
            var args = new List<string>
            {
                "-batchmode",           // Headless server
                "-nographics",          // No graphics
                "-dedicated",           // Our custom flag for dedicated mode
                $"-port {port}",
                $"-node {parameters.NodeId}",
                $"-attacker {parameters.AttackingFaction}",
                $"-defender {parameters.DefendingFaction}",
                $"-attackerTickets {parameters.AttackerSpawnTickets}",
                $"-defenderTickets {parameters.DefenderSpawnTickets}",
                $"-battleId {parameters.BattleId}"
            };
            
            // Add node name (quoted for spaces)
            if (!string.IsNullOrEmpty(parameters.NodeName))
            {
                args.Add($"-nodeName \"{parameters.NodeName}\"");
            }
            
            // Add timeout
            if (parameters.TimeLimit > 0)
            {
                args.Add($"-timeLimit {parameters.TimeLimit}");
            }
            
            return string.Join(" ", args);
        }
        
        #endregion
        
        #region Path Helpers
        
        private string GetFPSServerPath()
        {
            #if UNITY_EDITOR
            // In editor, use configured path
            if (!string.IsNullOrEmpty(editorFpsServerPath) && File.Exists(editorFpsServerPath))
                return editorFpsServerPath;
            
            // Try Builds folder relative to project
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string relativePath = Path.Combine(projectPath, "Builds", "FPS", "ElitesFPS.exe");
            if (File.Exists(relativePath))
                return relativePath;
            
            return editorFpsServerPath;
            #else
            // In build, relative to our executable
            string ourFolder = Path.GetDirectoryName(Application.dataPath);
            return Path.GetFullPath(Path.Combine(ourFolder, fpsServerRelativePath));
            #endif
        }
        
        private bool ValidateExecutable(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                LogError("FPS server path not configured!");
                return false;
            }
            
            if (!File.Exists(path))
            {
                LogError($"FPS server executable not found: {path}");
                return false;
            }
            
            return true;
        }
        
        #endregion
        
        #region Logging
        
        private void Log(string msg)
        {
            if (debugMode) Debug.Log($"[DedicatedServerLauncher] {msg}");
        }
        
        private void LogWarning(string msg)
        {
            Debug.LogWarning($"[DedicatedServerLauncher] {msg}");
        }
        
        private void LogError(string msg)
        {
            Debug.LogError($"[DedicatedServerLauncher] {msg}");
        }
        
        #endregion
        
        #region Data Classes
        
        private class FPSServerInstance
        {
            public int NodeId;
            public ushort Port;
            public Process Process;
            public BattleParameters Parameters;
            public float StartTime;
            public FPSServerStatus Status;
        }
        
        private class PendingBattle
        {
            public int NodeId;
            public ushort Port;
            public float StartTime;
        }
        
        private enum FPSServerStatus
        {
            Starting,
            Ready,
            Stopping
        }
        
        #endregion
    }
#else
    // Stub for client builds - provides interface but does nothing
    public class DedicatedServerLauncher : MonoBehaviour
    {
        public static DedicatedServerLauncher Instance => null;
        public static event Action<int, string, ushort> OnBattleServerReady;
        public static event Action<int> OnBattleServerStopped;
        
        public bool StartBattleServer(BattleParameters parameters) => false;
        public void StopBattleServer(int nodeId) { }
        public (string address, ushort port)? GetBattleServerInfo(int nodeId) => null;
        public bool IsBattleServerReady(int nodeId) => false;
        public void SetPublicAddress(string address) { }
    }
#endif
}
