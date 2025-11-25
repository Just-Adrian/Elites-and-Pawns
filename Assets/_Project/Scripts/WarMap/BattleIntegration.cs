using System.Collections.Generic;
using UnityEngine;
using Mirror;
using ElitesAndPawns.Core;
using ElitesAndPawns.GameModes;
using ElitesAndPawns.Player;

namespace ElitesAndPawns.WarMap
{
    /// <summary>
    /// Integrates FPS battle results with the War Map system.
    /// Tracks battle progress and reports results back to the strategic layer.
    /// </summary>
    public class BattleIntegration : NetworkBehaviour
    {
        #region Singleton
        
        private static BattleIntegration _instance;
        public static BattleIntegration Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<BattleIntegration>();
                }
                return _instance;
            }
        }
        
        #endregion
        
        #region Fields
        
        [Header("Battle Configuration")]
        [SerializeField] private bool isBattleScene = false;
        [SerializeField] private int battleNodeID = -1; // Set when loading from war map
        [SerializeField] private Team attackingFaction = Team.None;
        [SerializeField] private Team defendingFaction = Team.None;
        
        [Header("Battle Progress")]
        [SerializeField] private float battleStartTime;
        [SerializeField] private int targetScore = 100; // Score needed to win
        [SerializeField] private float maxBattleDuration = 900f; // 15 minutes max
        
        [Header("Player Tracking")]
        private Dictionary<string, PlayerBattleStats> playerStats = new Dictionary<string, PlayerBattleStats>();
        private Dictionary<Team, int> factionPlayerCount = new Dictionary<Team, int>();
        
        [Header("Token Rewards")]
        private Dictionary<string, int> pendingTokenRewards = new Dictionary<string, int>();
        
        // Network synced battle state
        [SyncVar]
        private bool battleActive = false;
        
        [SyncVar]
        private Team winningFaction = Team.None;
        
        #endregion
        
        #region Properties
        
        public bool IsBattleActive => battleActive;
        public Team WinningFaction => winningFaction;
        public float BattleDuration => Time.time - battleStartTime;
        public int BattleNodeID => battleNodeID;
        
        #endregion
        
        #region Events
        
        public static event System.Action<Team> OnBattleStarted;
        public static event System.Action<BattleResult> OnBattleEnded;
        #pragma warning disable 0067 // Event never used - planned for player scoring system
        public static event System.Action<string, int> OnPlayerScored;
        #pragma warning restore 0067
        
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
            
            // Check if this is a battle scene
            CheckIfBattleScene();
        }
        
        void Start()
        {
            if (isBattleScene && isServer)
            {
                InitializeBattle();
            }
            
            SubscribeToGameEvents();
        }
        
        void OnDestroy()
        {
            UnsubscribeFromGameEvents();
        }
        
        void Update()
        {
            if (!isServer || !battleActive)
                return;
                
            // Check for battle timeout
            if (BattleDuration >= maxBattleDuration)
            {
                EndBattleByTimeout();
            }
        }
        
        #endregion
        
        #region Initialization
        
        private void CheckIfBattleScene()
        {
            // Determine if we're in a battle scene
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            isBattleScene = (sceneName == "NetworkTest" || sceneName.Contains("Battle"));
            
            if (isBattleScene)
            {
                // Load battle parameters (would come from scene loading parameters)
                LoadBattleParameters();
            }
        }
        
        private void LoadBattleParameters()
        {
            // In a full implementation, these would be passed from the War Map
            // when loading the battle scene. For now, we'll use PlayerPrefs or
            // static data transfer
            
            battleNodeID = PlayerPrefs.GetInt("BattleNodeID", 0);
            attackingFaction = (Team)PlayerPrefs.GetInt("AttackingFaction", 1);
            defendingFaction = (Team)PlayerPrefs.GetInt("DefendingFaction", 2);
            
            Debug.Log($"[BattleIntegration] Loaded battle parameters - Node: {battleNodeID}, " +
                     $"Attackers: {attackingFaction}, Defenders: {defendingFaction}");
        }
        
        [Server]
        private void InitializeBattle()
        {
            battleActive = true;
            battleStartTime = Time.time;
            winningFaction = Team.None;
            
            // Initialize faction player counts
            factionPlayerCount[Team.Blue] = 0;
            factionPlayerCount[Team.Red] = 0;
            factionPlayerCount[Team.Green] = 0;
            
            OnBattleStarted?.Invoke(attackingFaction);
            
            Debug.Log($"[BattleIntegration] Battle initialized at node {battleNodeID}");
        }
        
        #endregion
        
        #region Event Subscriptions
        
        private void SubscribeToGameEvents()
        {
            if (!isBattleScene)
                return;
            
            // Subscribe to game mode events
            if (ScoreNetworkSync.Instance != null)
            {
                // We'll track score changes
            }
            
            // Subscribe to player events
            // Note: PlayerHealth uses instance events, not static
            // We'll need to hook these up when players spawn
            
            // Subscribe to control point events
            ControlPoint.OnPointCaptured += (faction) => OnPointCaptured((Team)(int)faction, 10); // Convert FactionType to Team
        }
        
        private void UnsubscribeFromGameEvents()
        {
            // Player events are instance-based and handled per player
            // Control point events would need proper unsubscribe with same lambda
            // For simplicity, ControlPoint static event will be cleaned up on scene change
        }
        
        #endregion
        
        #region Player Management
        
        /// <summary>
        /// Register a player joining the battle
        /// </summary>
        [Server]
        public void RegisterPlayer(string playerId, Team faction, GameObject playerObject = null)
        {
            if (!playerStats.ContainsKey(playerId))
            {
                playerStats[playerId] = new PlayerBattleStats
                {
                    PlayerId = playerId,
                    Faction = faction,
                    JoinTime = Time.time
                };
                
                factionPlayerCount[faction]++;
                
                // Hook up player events if object provided
                if (playerObject != null)
                {
                    var playerHealth = playerObject.GetComponent<PlayerHealth>();
                    if (playerHealth != null)
                    {
                        // Subscribe to this player's health events
                        playerHealth.OnDeath += (killer) => 
                        {
                            string killerId = killer?.netId.ToString() ?? "";
                            // Convert FactionType to Team (they have same values)
                            Team killerTeam = (Team)(int)(killer?.Faction ?? ElitesAndPawns.Core.FactionType.None);
                            OnPlayerKilled(playerId, killerId, killerTeam);
                        };
                    }
                }
                
                Debug.Log($"[BattleIntegration] Player {playerId} joined battle for {faction}");
            }
        }
        
        /// <summary>
        /// Unregister a player leaving the battle
        /// </summary>
        [Server]
        public void UnregisterPlayer(string playerId)
        {
            if (playerStats.ContainsKey(playerId))
            {
                var stats = playerStats[playerId];
                factionPlayerCount[stats.Faction]--;
                
                // Calculate participation reward
                float participationTime = Time.time - stats.JoinTime;
                if (participationTime > 60f) // At least 1 minute participation
                {
                    AwardTokens(playerId, stats.Faction, TokenSystem.TokenRewardType.Participation);
                }
                
                Debug.Log($"[BattleIntegration] Player {playerId} left battle");
            }
        }
        
        #endregion
        
        #region Battle Events
        
        private void OnPlayerKilled(string victimId, string killerId, Team killerTeam)
        {
            if (!isServer || !battleActive)
                return;
            
            // Update killer stats
            if (!string.IsNullOrEmpty(killerId) && playerStats.ContainsKey(killerId))
            {
                playerStats[killerId].Kills++;
                playerStats[killerId].Score += 10;
                
                // Award kill tokens
                AwardTokens(killerId, killerTeam, TokenSystem.TokenRewardType.Kill);
            }
            
            // Update victim stats
            if (playerStats.ContainsKey(victimId))
            {
                playerStats[victimId].Deaths++;
            }
        }
        
        private void OnPlayerRespawned(string playerId)
        {
            // Could track respawn stats if needed
        }
        
        private void OnPointCaptured(Team capturingTeam, int pointsAwarded)
        {
            if (!isServer || !battleActive)
                return;
            
            // Award capture tokens to all players of the capturing team in the zone
            foreach (var stats in playerStats.Values)
            {
                if (stats.Faction == capturingTeam)
                {
                    // In a full implementation, check if player is actually in the capture zone
                    stats.CapturePoints++;
                    stats.Score += 5;
                    
                    AwardTokens(stats.PlayerId, capturingTeam, TokenSystem.TokenRewardType.Capture);
                }
            }
            
            // Check for battle end conditions
            CheckBattleEndConditions();
        }
        
        #endregion
        
        #region Battle End Conditions
        
        [Server]
        private void CheckBattleEndConditions()
        {
            if (!battleActive)
                return;
            
            // Get current scores
            int blueScore = 0;
            int redScore = 0;
            
            if (ScoreNetworkSync.Instance != null)
            {
                blueScore = ScoreNetworkSync.Instance.BlueScore;
                redScore = ScoreNetworkSync.Instance.RedScore;
            }
            
            // Check if any team has reached the target score
            if (blueScore >= targetScore)
            {
                EndBattle(Team.Blue);
            }
            else if (redScore >= targetScore)
            {
                EndBattle(Team.Red);
            }
        }
        
        [Server]
        private void EndBattleByTimeout()
        {
            // Determine winner based on current score
            int blueScore = ScoreNetworkSync.Instance?.BlueScore ?? 0;
            int redScore = ScoreNetworkSync.Instance?.RedScore ?? 0;
            
            Team winner = Team.None;
            if (blueScore > redScore)
                winner = Team.Blue;
            else if (redScore > blueScore)
                winner = Team.Red;
            else
                winner = defendingFaction; // Defender wins ties
            
            EndBattle(winner);
        }
        
        [Server]
        private void EndBattle(Team winner)
        {
            if (!battleActive)
                return;
            
            battleActive = false;
            winningFaction = winner;
            
            // Calculate battle results
            BattleResult result = CalculateBattleResult(winner);
            
            // Process all pending token rewards
            ProcessPendingTokenRewards();
            
            // Send results back to War Map
            SendResultsToWarMap(result);
            
            OnBattleEnded?.Invoke(result);
            
            Debug.Log($"[BattleIntegration] Battle ended! Winner: {winner}");
            
            // Return to war map after delay
            StartCoroutine(ReturnToWarMap());
        }
        
        #endregion
        
        #region Result Calculation
        
        [Server]
        private BattleResult CalculateBattleResult(Team winner)
        {
            var result = new BattleResult
            {
                WinnerFaction = winner,
                LoserFaction = (winner == attackingFaction) ? defendingFaction : attackingFaction,
                BattleDuration = BattleDuration,
                PlayersParticipated = playerStats.Count
            };
            
            // Calculate control change based on battle performance
            int winnerScore = 0;
            int loserScore = 0;
            
            if (ScoreNetworkSync.Instance != null)
            {
                if (winner == Team.Blue)
                {
                    winnerScore = ScoreNetworkSync.Instance.BlueScore;
                    loserScore = ScoreNetworkSync.Instance.RedScore;
                }
                else
                {
                    winnerScore = ScoreNetworkSync.Instance.RedScore;
                    loserScore = ScoreNetworkSync.Instance.BlueScore;
                }
            }
            
            // Control change based on score difference
            float scoreDiff = winnerScore - loserScore;
            result.ControlChange = Mathf.Clamp(scoreDiff * 0.5f, 10f, 50f);
            
            // Token rewards/losses
            result.TokensWon = Mathf.RoundToInt(winnerScore * 2);
            result.TokensLost = Mathf.RoundToInt(loserScore);
            
            // Add player scores
            foreach (var stats in playerStats.Values)
            {
                result.PlayerScores[stats.PlayerId] = stats.Score;
            }
            
            return result;
        }
        
        #endregion
        
        #region Token Management
        
        [Server]
        private void AwardTokens(string playerId, Team faction, TokenSystem.TokenRewardType rewardType)
        {
            if (TokenSystem.Instance == null)
            {
                // Store for later if TokenSystem not available
                if (!pendingTokenRewards.ContainsKey(playerId))
                    pendingTokenRewards[playerId] = 0;
                    
                pendingTokenRewards[playerId] += GetTokenRewardAmount(rewardType);
                return;
            }
            
            // Award immediately if possible
            TokenSystem.Instance.AwardPlayerTokens(faction, playerId, rewardType);
        }
        
        [Server]
        private void ProcessPendingTokenRewards()
        {
            if (TokenSystem.Instance == null)
                return;
            
            foreach (var kvp in pendingTokenRewards)
            {
                if (playerStats.ContainsKey(kvp.Key))
                {
                    var stats = playerStats[kvp.Key];
                    TokenSystem.Instance.AddTokens(stats.Faction, kvp.Value, 
                        $"Battle rewards for player {kvp.Key}");
                }
            }
            
            pendingTokenRewards.Clear();
        }
        
        private int GetTokenRewardAmount(TokenSystem.TokenRewardType rewardType)
        {
            switch (rewardType)
            {
                case TokenSystem.TokenRewardType.Kill:
                    return 10;
                case TokenSystem.TokenRewardType.Capture:
                    return 25;
                case TokenSystem.TokenRewardType.Participation:
                    return 20;
                default:
                    return 5;
            }
        }
        
        #endregion
        
        #region War Map Communication
        
        [Server]
        private void SendResultsToWarMap(BattleResult result)
        {
            // Save results to be retrieved by War Map
            SaveBattleResults(result);
            
            // If WarMapManager is in the scene (same server), update directly
            if (WarMapManager.Instance != null)
            {
                WarMapManager.Instance.EndBattle(battleNodeID, result);
            }
            else
            {
                // In a distributed server architecture, this would send
                // results to the war map server via network message
                Debug.Log("[BattleIntegration] Saving battle results for war map server");
            }
        }
        
        private void SaveBattleResults(BattleResult result)
        {
            // Save to PlayerPrefs for simple persistence
            // In production, this would use a proper database
            
            PlayerPrefs.SetInt("LastBattleNode", battleNodeID);
            PlayerPrefs.SetInt("LastBattleWinner", (int)result.WinnerFaction);
            PlayerPrefs.SetFloat("LastBattleControlChange", result.ControlChange);
            PlayerPrefs.SetInt("LastBattleTokensWon", result.TokensWon);
            PlayerPrefs.Save();
        }
        
        private System.Collections.IEnumerator ReturnToWarMap()
        {
            yield return new WaitForSeconds(5f); // Show results for 5 seconds
            
            // Return to war map scene
            if (NetworkManager.singleton != null)
            {
                // This would need proper scene management in production
                Debug.Log("[BattleIntegration] Returning to War Map...");
                // NetworkManager.singleton.ServerChangeScene("WarMap");
            }
        }
        
        #endregion
        
        #region Client RPCs
        
        [ClientRpc]
        public void RpcShowBattleResults(Team winner, int tokensEarned)
        {
            Debug.Log($"[BattleIntegration-Client] Battle ended! Winner: {winner}");
            Debug.Log($"[BattleIntegration-Client] You earned {tokensEarned} tokens");
            
            // Show UI notification
            // In production, this would display a proper results screen
        }
        
        #endregion
        
        #region Data Classes
        
        /// <summary>
        /// Tracks individual player statistics during a battle
        /// </summary>
        [System.Serializable]
        private class PlayerBattleStats
        {
            public string PlayerId;
            public Team Faction;
            public int Kills;
            public int Deaths;
            public int CapturePoints;
            public int Score;
            public float JoinTime;
            public float PlayTime;
        }
        
        #endregion
    }
}