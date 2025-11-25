using UnityEngine;
using Mirror;

namespace ElitesAndPawns.GameModes
{
    /// <summary>
    /// Dedicated NetworkBehaviour for synchronizing team scores across the network.
    /// This component must be on an active GameObject with NetworkIdentity.
    /// </summary>
    public class ScoreNetworkSync : NetworkBehaviour
    {
        [Header("Synchronized Scores")]
        [SyncVar(hook = nameof(OnBlueScoreChanged))]
        private int blueScore = 0;
        
        [SyncVar(hook = nameof(OnRedScoreChanged))]
        private int redScore = 0;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = true;
        
        // Singleton
        private static ScoreNetworkSync _instance;
        public static ScoreNetworkSync Instance => _instance;
        
        // Events that UI can listen to
        public delegate void ScoreUpdated(int blueScore, int redScore);
        public static event ScoreUpdated OnScoreUpdated;
        
        // Properties
        public int BlueScore => blueScore;
        public int RedScore => redScore;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            
            if (debugMode)
            {
                Debug.Log("[ScoreNetworkSync] Initialized");
            }
        }
        
        private void Start()
        {
            if (debugMode)
            {
                Debug.Log($"[ScoreNetworkSync] NetworkIdentity - Server: {isServer}, Client: {isClient}");
            }
        }
        
        /// <summary>
        /// Update both scores (Server only)
        /// SyncVars will automatically sync to all clients
        /// </summary>
        [Server]
        public void SetScores(int blue, int red)
        {
            blueScore = blue;
            redScore = red;
            
            if (debugMode)
            {
                Debug.Log($"[ScoreNetworkSync] Server set scores - Blue: {blue}, Red: {red}");
            }
        }
        
        /// <summary>
        /// Add to a specific team's score (Server only)
        /// </summary>
        [Server]
        public void AddScore(Core.FactionType team, int points)
        {
            switch (team)
            {
                case Core.FactionType.Blue:
                    blueScore += points;
                    break;
                case Core.FactionType.Red:
                    redScore += points;
                    break;
            }
            
            if (debugMode)
            {
                Debug.Log($"[ScoreNetworkSync] Server added {points} to {team} - Blue: {blueScore}, Red: {redScore}");
            }
        }
        
        /// <summary>
        /// Reset scores (Server only)
        /// </summary>
        [Server]
        public void ResetScores()
        {
            blueScore = 0;
            redScore = 0;
            
            if (debugMode)
            {
                Debug.Log("[ScoreNetworkSync] Server reset scores");
            }
        }
        
        /// <summary>
        /// SyncVar hook for blue score - fires on ALL clients when value changes
        /// </summary>
        private void OnBlueScoreChanged(int oldScore, int newScore)
        {
            if (debugMode)
            {
                Debug.Log($"[ScoreNetworkSync] Blue score changed: {oldScore} → {newScore}");
            }
            
            // Fire event that UI listens to
            OnScoreUpdated?.Invoke(blueScore, redScore);
        }
        
        /// <summary>
        /// SyncVar hook for red score - fires on ALL clients when value changes
        /// </summary>
        private void OnRedScoreChanged(int oldScore, int newScore)
        {
            if (debugMode)
            {
                Debug.Log($"[ScoreNetworkSync] Red score changed: {oldScore} → {newScore}");
            }
            
            // Fire event that UI listens to
            OnScoreUpdated?.Invoke(blueScore, redScore);
        }
    }
}
