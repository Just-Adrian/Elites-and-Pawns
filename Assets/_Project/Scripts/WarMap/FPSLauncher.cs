using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using ElitesAndPawns.Core;
using Debug = UnityEngine.Debug;

namespace ElitesAndPawns.WarMap
{
    /// <summary>
    /// Manages FPS battle instances. Tracks active battles and directs players
    /// to either start a new server or connect as client.
    /// 
    /// Architecture:
    /// - First player to join a battle: Starts FPS as HOST
    /// - Subsequent players: Connect as CLIENT to existing battle
    /// 
    /// Future (dedicated server): Server spawns FPS servers, all players connect as clients.
    /// </summary>
    public class FPSLauncher : MonoBehaviour
    {
        #region Singleton
        
        private static FPSLauncher _instance;
        public static FPSLauncher Instance => _instance;
        
        #endregion
        
        #region Configuration
        
        [Header("FPS Build Location")]
        [Tooltip("Path to FPS executable relative to this build's folder")]
        [SerializeField] private string fpsExecutablePath = "../FPS/ElitesFPS.exe";
        
        [Tooltip("For editor testing: absolute path to FPS build")]
        [SerializeField] private string editorFpsPath = "";
        
        [Header("Network Settings")]
        [Tooltip("Base port for FPS battles (each battle uses basePort + nodeId)")]
        [SerializeField] private ushort basePort = 7780;
        
        [Tooltip("Server address for clients (localhost for same machine, IP for network)")]
        [SerializeField] private string serverAddress = "localhost";
        
        #endregion
        
        #region Battle Tracking
        
        /// <summary>
        /// Tracks active battles. Key = nodeId, Value = port
        /// </summary>
        private Dictionary<int, ActiveBattle> activeBattles = new Dictionary<int, ActiveBattle>();
        
        private class ActiveBattle
        {
            public int nodeId;
            public ushort port;
            public string hostAddress;
            public DateTime startTime;
            public int playerCount;
        }
        
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
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Launch FPS for a player joining a battle.
        /// First player starts as host, subsequent players connect as clients.
        /// </summary>
        public void LaunchFPS(int nodeId, Team faction, string playerName = "Soldier")
        {
            string exePath = GetFPSExecutablePath();
            
            if (!ValidateExecutable(exePath))
                return;
            
            // Calculate port for this battle
            ushort battlePort = (ushort)(basePort + nodeId);
            
            // Check if battle already has a host
            bool needsHost = !activeBattles.ContainsKey(nodeId);
            
            // Build arguments
            string args;
            if (needsHost)
            {
                // First player - start as host
                args = BuildHostArgs(nodeId, faction, playerName, battlePort);
                
                // Track this battle
                activeBattles[nodeId] = new ActiveBattle
                {
                    nodeId = nodeId,
                    port = battlePort,
                    hostAddress = serverAddress,
                    startTime = DateTime.Now,
                    playerCount = 1
                };
                
                Debug.Log($"[FPSLauncher] Starting NEW battle at node {nodeId} on port {battlePort}");
            }
            else
            {
                // Subsequent player - connect as client
                var battle = activeBattles[nodeId];
                args = BuildClientArgs(nodeId, faction, playerName, battle.hostAddress, battle.port);
                battle.playerCount++;
                
                Debug.Log($"[FPSLauncher] Joining EXISTING battle at node {nodeId} ({battle.hostAddress}:{battle.port})");
            }
            
            LaunchProcess(exePath, args, nodeId, faction);
        }
        
        /// <summary>
        /// Force launch as host (for testing or server-side spawning).
        /// </summary>
        public void LaunchFPSAsHost(int nodeId, Team faction, string playerName, ushort port)
        {
            string exePath = GetFPSExecutablePath();
            if (!ValidateExecutable(exePath)) return;
            
            string args = BuildHostArgs(nodeId, faction, playerName, port);
            
            activeBattles[nodeId] = new ActiveBattle
            {
                nodeId = nodeId,
                port = port,
                hostAddress = serverAddress,
                startTime = DateTime.Now,
                playerCount = 1
            };
            
            LaunchProcess(exePath, args, nodeId, faction);
        }
        
        /// <summary>
        /// Force launch as client (for players joining existing battles).
        /// </summary>
        public void LaunchFPSAsClient(int nodeId, Team faction, string playerName, string server, ushort port)
        {
            string exePath = GetFPSExecutablePath();
            if (!ValidateExecutable(exePath)) return;
            
            string args = BuildClientArgs(nodeId, faction, playerName, server, port);
            LaunchProcess(exePath, args, nodeId, faction);
        }
        
        /// <summary>
        /// Called when a battle ends - removes from tracking.
        /// </summary>
        public void OnBattleEnded(int nodeId)
        {
            if (activeBattles.ContainsKey(nodeId))
            {
                activeBattles.Remove(nodeId);
                Debug.Log($"[FPSLauncher] Battle at node {nodeId} ended");
            }
        }
        
        /// <summary>
        /// Check if a battle is already running at a node.
        /// </summary>
        public bool IsBattleActive(int nodeId) => activeBattles.ContainsKey(nodeId);
        
        /// <summary>
        /// Get info about an active battle.
        /// </summary>
        public (string address, ushort port)? GetBattleInfo(int nodeId)
        {
            if (activeBattles.TryGetValue(nodeId, out var battle))
            {
                return (battle.hostAddress, battle.port);
            }
            return null;
        }
        
        /// <summary>
        /// Set the server address for network play (call before launching battles).
        /// </summary>
        public void SetServerAddress(string address)
        {
            serverAddress = address;
            Debug.Log($"[FPSLauncher] Server address set to: {address}");
        }
        
        #endregion
        
        #region Private Methods
        
        private string BuildHostArgs(int nodeId, Team faction, string playerName, ushort port)
        {
            return $"-host -node {nodeId} -faction {faction} -name \"{playerName}\" -port {port}";
        }
        
        private string BuildClientArgs(int nodeId, Team faction, string playerName, string server, ushort port)
        {
            return $"-client -server {server} -port {port} -node {nodeId} -faction {faction} -name \"{playerName}\"";
        }
        
        private bool ValidateExecutable(string exePath)
        {
            if (string.IsNullOrEmpty(exePath))
            {
                Debug.LogError("[FPSLauncher] FPS executable path not configured!");
                return false;
            }
            
            if (!File.Exists(exePath))
            {
                Debug.LogError($"[FPSLauncher] FPS executable not found: {exePath}");
                
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.DisplayDialog(
                    "FPS Build Not Found",
                    $"Could not find FPS executable at:\n{exePath}\n\n" +
                    "Build NetworkTest scene to 'Builds/FPS/' folder.",
                    "OK"
                );
                #endif
                
                return false;
            }
            
            return true;
        }
        
        private void LaunchProcess(string exePath, string args, int nodeId, Team faction)
        {
            Debug.Log($"[FPSLauncher] Launching: {exePath}");
            Debug.Log($"[FPSLauncher] Arguments: {args}");
            
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exePath)
                };
                
                Process.Start(startInfo);
                Debug.Log($"[FPSLauncher] Launched FPS for node {nodeId} as {faction}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[FPSLauncher] Failed to launch: {e.Message}");
            }
        }
        
        private string GetFPSExecutablePath()
        {
            #if UNITY_EDITOR
            if (!string.IsNullOrEmpty(editorFpsPath) && File.Exists(editorFpsPath))
                return editorFpsPath;
            
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string relativePath = Path.Combine(projectPath, "Builds", "FPS", "ElitesFPS.exe");
            if (File.Exists(relativePath))
                return relativePath;
            
            return editorFpsPath;
            #else
            string rtsFolder = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(rtsFolder, fpsExecutablePath);
            #endif
        }
        
        #endregion
    }
}
