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
    /// 
    /// In the new squad-based system, this class works with NodeOccupancy to:
    /// - Track which squads are involved in battles
    /// - Consume spawn tickets from squads when players respawn
    /// - Report battle outcomes to affect node control
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
        [SerializeField] private int battleNodeID = -1;
        [SerializeField] private FactionType attackingFaction = FactionType.None;
        [SerializeField] private FactionType defendingFaction = FactionType.None;
        
        [Header("Battle Progress")]
        [SerializeField] private float battleStartTime;
        [SerializeField] private int targetScore = 100;
        [SerializeField] private float maxBattleDuration = 900f;
        
        [Header("Player Tracking")]
        private Dictionary<string, PlayerBattleStats> playerStats = new Dictionary<string, PlayerBattleStats>();
        private Dictionary<FactionType, int> factionPlayerCount = new Dictionary<FactionType, int>();
        
        // Network synced battle state
        [SyncVar]
        private bool battleActive = false;
        
        [SyncVar]
        private FactionType winningFaction = FactionType.None;
        
        #endregion
        
        #region Properties
        
        public bool IsBattleActive => battleActive;
        public FactionType WinningFaction => winningFaction;
        public float BattleDuration => Time.time - battleStartTime;
        public int BattleNodeID => battleNodeID;
        public FactionType AttackingFaction => attackingFaction;
        public FactionType DefendingFaction => defendingFaction;
        
        #endregion
        
        #region Events
        
        public static event System.Action<FactionType> OnBattleStarted;
        public static event System.Action<BattleResult> OnBattleEnded;
        public static event System.Action<string, int> OnPlayerScored;
        
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
                
            if (BattleDuration >= maxBattleDuration)
            {
                EndBattleByTimeout();
            }
        }
        
        #endregion
        
        #region Initialization
        
        private void CheckIfBattleScene()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            isBattleScene = (sceneName == "NetworkTest" || sceneName.Contains("Battle"));
            
            if (isBattleScene)
            {
                LoadBattleParameters();
            }
        }
        
        private void LoadBattleParameters()
        {
            battleNodeID = PlayerPrefs.GetInt("BattleNodeID", 0);
            attackingFaction = (FactionType)PlayerPrefs.GetInt("AttackingFaction", 1);
            defendingFaction = (FactionType)PlayerPrefs.GetInt("DefendingFaction", 2);
            
            Debug.Log($"[BattleIntegration] Loaded battle parameters - Node: {battleNodeID}, " +
                     $"Attackers: {attackingFaction}, Defenders: {defendingFaction}");
        }
        
        [Server]
        private void InitializeBattle()
        {
            battleActive = true;
            battleStartTime = Time.time;
            winningFaction = FactionType.None;
            
            factionPlayerCount[FactionType.Blue] = 0;
            factionPlayerCount[FactionType.Red] = 0;
            factionPlayerCount[FactionType.Green] = 0;
            
            OnBattleStarted?.Invoke(attackingFaction);
            
            Debug.Log($"[BattleIntegration] Battle initialized at node {battleNodeID}");
        }
        
        #endregion
        
        #region Event Subscriptions
        
        private void SubscribeToGameEvents()
        {
            if (!isBattleScene)
                return;
            
            ControlPoint.OnPointCaptured += (faction) => OnPointCaptured((FactionType)(int)faction, 10);
        }
        
        private void UnsubscribeFromGameEvents()
        {
            // Events cleaned up on scene change
        }
        
        #endregion
        
        #region Spawn Ticket Integration
        
        /// <summary>
        /// Request a spawn ticket for a player. Integrates with NodeOccupancy.
        /// </summary>
        /// <param name="playerNetId">Network ID of the spawning player</param>
        /// <param name="faction">Faction of the player</param>
        /// <param name="squadId">Output: ID of the squad that provided the ticket</param>
        /// <param name="squadOwnerNetId">Output: Net ID of the squad owner</param>
        /// <returns>True if spawn is allowed</returns>
        [Server]
        public bool RequestSpawn(uint playerNetId, FactionType faction, out string squadId, out uint squadOwnerNetId)
        {
            squadId = "";
            squadOwnerNetId = 0;
            
            if (!battleActive)
            {
                Debug.LogWarning("[BattleIntegration] Cannot spawn - battle not active");
                return false;
            }
            
            // Use NodeOccupancy to get a spawn ticket from available squads
            if (NodeOccupancy.Instance != null)
            {
                return NodeOccupancy.Instance.RequestSpawnTicket(
                    battleNodeID, 
                    faction, 
                    playerNetId,
                    out squadId,
                    out squadOwnerNetId
                );
            }
            
            // Fallback if NodeOccupancy not available (for testing without full war map)
            Debug.LogWarning("[BattleIntegration] NodeOccupancy not available, allowing spawn for testing");
            return true;
        }
        
        /// <summary>
        /// Check if a faction has any spawn tickets remaining at this node.
        /// </summary>
        [Server]
        public bool HasSpawnTickets(FactionType faction)
        {
            if (NodeOccupancy.Instance != null)
            {
                return NodeOccupancy.Instance.GetFactionManpowerAtNode(battleNodeID, faction) > 0;
            }
            return true; // Fallback for testing
        }
        
        /// <summary>
        /// Get remaining spawn tickets for a faction.
        /// </summary>
        public int GetRemainingSpawnTickets(FactionType faction)
        {
            if (NodeOccupancy.Instance != null)
            {
                return NodeOccupancy.Instance.GetFactionManpowerAtNode(battleNodeID, faction);
            }
            return 999; // Fallback for testing
        }
        
        #endregion
        
        #region Player Management
        
        /// <summary>
        /// Register a player joining the battle.
        /// </summary>
        [Server]
        public void RegisterPlayer(string playerId, FactionType faction, GameObject playerObject = null)
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
                
                if (playerObject != null)
                {
                    var playerHealth = playerObject.GetComponent<PlayerHealth>();
                    if (playerHealth != null)
                    {
                        playerHealth.OnDeath += (killer) => 
                        {
                            string killerId = killer?.netId.ToString() ?? "";
                            FactionType killerTeam = (FactionType)(int)(killer?.Faction ?? ElitesAndPawns.Core.FactionType.None);
                            OnPlayerKilled(playerId, killerId, killerTeam);
                        };
                    }
                }
                
                Debug.Log($"[BattleIntegration] Player {playerId} joined battle for {faction}");
            }
        }
        
        /// <summary>
        /// Unregister a player leaving the battle.
        /// </summary>
        [Server]
        public void UnregisterPlayer(string playerId)
        {
            if (playerStats.ContainsKey(playerId))
            {
                var stats = playerStats[playerId];
                factionPlayerCount[stats.Faction]--;
                
                Debug.Log($"[BattleIntegration] Player {playerId} left battle");
            }
        }
        
        #endregion
        
        #region Battle Events
        
        private void OnPlayerKilled(string victimId, string killerId, FactionType killerTeam)
        {
            if (!isServer || !battleActive)
                return;
            
            if (!string.IsNullOrEmpty(killerId) && playerStats.ContainsKey(killerId))
            {
                playerStats[killerId].Kills++;
                playerStats[killerId].Score += 10;
                
                OnPlayerScored?.Invoke(killerId, 10);
            }
            
            if (playerStats.ContainsKey(victimId))
            {
                playerStats[victimId].Deaths++;
            }
            
            // Check if losing faction is out of spawn tickets
            FactionType victimFaction = playerStats.ContainsKey(victimId) ? playerStats[victimId].Faction : FactionType.None;
            if (victimFaction != FactionType.None && !HasSpawnTickets(victimFaction))
            {
                // Faction eliminated - they lose
                FactionType winner = (victimFaction == attackingFaction) ? defendingFaction : attackingFaction;
                EndBattle(winner);
            }
        }
        
        private void OnPointCaptured(FactionType capturingTeam, int pointsAwarded)
        {
            if (!isServer || !battleActive)
                return;
            
            foreach (var stats in playerStats.Values)
            {
                if (stats.Faction == capturingTeam)
                {
                    stats.CapturePoints++;
                    stats.Score += 5;
                }
            }
            
            CheckBattleEndConditions();
        }
        
        #endregion
        
        #region Battle End Conditions
        
        [Server]
        private void CheckBattleEndConditions()
        {
            if (!battleActive)
                return;
            
            int blueScore = 0;
            int redScore = 0;
            
            if (ScoreNetworkSync.Instance != null)
            {
                blueScore = ScoreNetworkSync.Instance.BlueScore;
                redScore = ScoreNetworkSync.Instance.RedScore;
            }
            
            if (blueScore >= targetScore)
            {
                EndBattle(FactionType.Blue);
            }
            else if (redScore >= targetScore)
            {
                EndBattle(FactionType.Red);
            }
        }
        
        [Server]
        private void EndBattleByTimeout()
        {
            int blueScore = ScoreNetworkSync.Instance?.BlueScore ?? 0;
            int redScore = ScoreNetworkSync.Instance?.RedScore ?? 0;
            
            FactionType winner = FactionType.None;
            if (blueScore > redScore)
                winner = FactionType.Blue;
            else if (redScore > blueScore)
                winner = FactionType.Red;
            else
                winner = defendingFaction;
            
            EndBattle(winner);
        }
        
        [Server]
        private void EndBattle(FactionType winner)
        {
            if (!battleActive)
                return;
            
            battleActive = false;
            winningFaction = winner;
            
            BattleResult result = CalculateBattleResult(winner);
            
            SendResultsToWarMap(result);
            
            OnBattleEnded?.Invoke(result);
            
            Debug.Log($"[BattleIntegration] Battle ended! Winner: {winner}");
            
            StartCoroutine(ReturnToWarMap());
        }
        
        #endregion
        
        #region Result Calculation
        
        [Server]
        private BattleResult CalculateBattleResult(FactionType winner)
        {
            var result = new BattleResult
            {
                WinnerFaction = winner,
                LoserFaction = (winner == attackingFaction) ? defendingFaction : attackingFaction,
                BattleDuration = BattleDuration,
                PlayersParticipated = playerStats.Count
            };
            
            int winnerScore = 0;
            int loserScore = 0;
            
            if (ScoreNetworkSync.Instance != null)
            {
                if (winner == FactionType.Blue)
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
            
            float scoreDiff = winnerScore - loserScore;
            result.ControlChange = Mathf.Clamp(scoreDiff * 0.5f, 10f, 50f);
            
            foreach (var stats in playerStats.Values)
            {
                result.PlayerScores[stats.PlayerId] = stats.Score;
            }
            
            return result;
        }
        
        #endregion
        
        #region War Map Communication
        
        [Server]
        private void SendResultsToWarMap(BattleResult result)
        {
            SaveBattleResults(result);
            
            if (WarMapManager.Instance != null)
            {
                WarMapManager.Instance.EndBattle(battleNodeID, result);
            }
            else
            {
                Debug.Log("[BattleIntegration] Saving battle results for war map server");
            }
            
            // Clear spawn history for this node
            if (NodeOccupancy.Instance != null)
            {
                NodeOccupancy.Instance.ClearSpawnHistory(battleNodeID);
            }
        }
        
        private void SaveBattleResults(BattleResult result)
        {
            PlayerPrefs.SetInt("LastBattleNode", battleNodeID);
            PlayerPrefs.SetInt("LastBattleWinner", (int)result.WinnerFaction);
            PlayerPrefs.SetFloat("LastBattleControlChange", result.ControlChange);
            PlayerPrefs.Save();
        }
        
        private System.Collections.IEnumerator ReturnToWarMap()
        {
            yield return new WaitForSeconds(5f);
            
            if (NetworkManager.singleton != null)
            {
                Debug.Log("[BattleIntegration] Returning to War Map...");
            }
        }
        
        #endregion
        
        #region Client RPCs
        
        [ClientRpc]
        public void RpcShowBattleResults(FactionType winner)
        {
            Debug.Log($"[BattleIntegration-Client] Battle ended! Winner: {winner}");
        }
        
        [ClientRpc]
        public void RpcUpdateSpawnTickets(int blueTickets, int redTickets)
        {
            // UI can use this to display remaining spawn tickets
            Debug.Log($"[BattleIntegration-Client] Spawn tickets - Blue: {blueTickets}, Red: {redTickets}");
        }
        
        #endregion
        
        #region Data Classes
        
        [System.Serializable]
        private class PlayerBattleStats
        {
            public string PlayerId;
            public FactionType Faction;
            public int Kills;
            public int Deaths;
            public int CapturePoints;
            public int Score;
            public float JoinTime;
            public float PlayTime;
            public string SpawnedFromSquadId;
            public uint SpawnedFromSquadOwner;
        }
        
        #endregion
    }
}
