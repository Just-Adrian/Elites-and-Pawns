using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using ElitesAndPawns.Core;

namespace ElitesAndPawns.WarMap
{
    /// <summary>
    /// Manages the token economy that bridges the RTS and FPS layers.
    /// Tokens are the primary resource for both strategic and tactical gameplay.
    /// </summary>
    public class TokenSystem : NetworkBehaviour
    {
        #region Singleton
        
        private static TokenSystem _instance;
        public static TokenSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<TokenSystem>();
                }
                return _instance;
            }
        }
        
        #endregion
        
        #region Fields
        
        [Header("Token Configuration")]
        [SerializeField] private int baseTokensPerCycle = 100;
        [SerializeField] private float tokenCycleDuration = 60f; // Seconds between token generation
        [SerializeField] private int maxTokensPerFaction = 10000;
        [SerializeField] private int startingTokens = 500;
        
        [Header("Battle Costs")]
        [SerializeField] private int attackCost = 100; // Cost to initiate an attack
        [SerializeField] private int reinforcementCost = 50; // Cost to send reinforcements
        [SerializeField] private int fortifyCost = 75; // Cost to fortify a node
        
        [Header("FPS Rewards")]
        [SerializeField] private int killReward = 10;
        [SerializeField] private int captureReward = 25;
        [SerializeField] private int winBonusReward = 100;
        [SerializeField] private int participationReward = 20;
        
        [Header("Current State")]
        private Dictionary<Team, FactionTokenData> factionTokens = new Dictionary<Team, FactionTokenData>();
        private float nextCycleTime;
        private bool isInitialized = false;
        
        // Network synced token values
        [SyncVar(hook = nameof(OnBlueTokensChanged))]
        private int blueTokens;
        
        [SyncVar(hook = nameof(OnRedTokensChanged))]
        private int redTokens;
        
        [SyncVar(hook = nameof(OnGreenTokensChanged))]
        private int greenTokens;
        
        #endregion
        
        #region Properties
        
        public int GetFactionTokens(Team faction)
        {
            if (factionTokens.ContainsKey(faction))
                return factionTokens[faction].CurrentTokens;
            return 0;
        }
        
        public bool CanAfford(Team faction, int cost)
        {
            return GetFactionTokens(faction) >= cost;
        }
        
        #endregion
        
        #region Events
        
        public static event Action<Team, int> OnTokensChanged;
        public static event Action<Team, int, string> OnTokensSpent;
        public static event Action<Team, int, string> OnTokensEarned;
        public static event Action OnTokenCycleCompleted;
        
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
            InitializeTokenSystem();
        }
        
        void Update()
        {
            if (!isServer)
                return;
                
            // Process token generation cycles
            if (Time.time >= nextCycleTime)
            {
                ProcessTokenCycle();
                nextCycleTime = Time.time + tokenCycleDuration;
            }
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeTokenSystem()
        {
            // Initialize faction token data
            factionTokens[Team.Blue] = new FactionTokenData(Team.Blue, startingTokens);
            factionTokens[Team.Red] = new FactionTokenData(Team.Red, startingTokens);
            factionTokens[Team.Green] = new FactionTokenData(Team.Green, startingTokens);
            
            // Sync initial values
            if (isServer)
            {
                blueTokens = startingTokens;
                redTokens = startingTokens;
                greenTokens = startingTokens;
            }
            
            nextCycleTime = Time.time + tokenCycleDuration;
            isInitialized = true;
            
            Debug.Log("[TokenSystem] Initialized with starting tokens: " + startingTokens);
        }
        
        #endregion
        
        #region Token Management
        
        /// <summary>
        /// Add tokens to a faction
        /// </summary>
        [Server]
        public void AddTokens(Team faction, int amount, string reason = "")
        {
            if (!isInitialized || faction == Team.None)
                return;
                
            if (factionTokens.ContainsKey(faction))
            {
                var data = factionTokens[faction];
                data.CurrentTokens = Mathf.Min(data.CurrentTokens + amount, maxTokensPerFaction);
                data.TotalEarned += amount;
                
                // Update synced values
                UpdateSyncedTokens(faction, data.CurrentTokens);
                
                // Log transaction
                data.AddTransaction(new TokenTransaction
                {
                    Amount = amount,
                    Reason = reason,
                    Timestamp = Time.time,
                    IsIncome = true
                });
                
                OnTokensEarned?.Invoke(faction, amount, reason);
                OnTokensChanged?.Invoke(faction, data.CurrentTokens);
                
                Debug.Log($"[TokenSystem] {faction} earned {amount} tokens. Reason: {reason}. Total: {data.CurrentTokens}");
            }
        }
        
        /// <summary>
        /// Spend tokens from a faction
        /// </summary>
        [Server]
        public bool SpendTokens(Team faction, int amount, string reason = "")
        {
            if (!isInitialized || faction == Team.None)
                return false;
                
            if (!CanAfford(faction, amount))
            {
                Debug.LogWarning($"[TokenSystem] {faction} cannot afford {amount} tokens. Current: {GetFactionTokens(faction)}");
                return false;
            }
            
            if (factionTokens.ContainsKey(faction))
            {
                var data = factionTokens[faction];
                data.CurrentTokens -= amount;
                data.TotalSpent += amount;
                
                // Update synced values
                UpdateSyncedTokens(faction, data.CurrentTokens);
                
                // Log transaction
                data.AddTransaction(new TokenTransaction
                {
                    Amount = amount,
                    Reason = reason,
                    Timestamp = Time.time,
                    IsIncome = false
                });
                
                OnTokensSpent?.Invoke(faction, amount, reason);
                OnTokensChanged?.Invoke(faction, data.CurrentTokens);
                
                Debug.Log($"[TokenSystem] {faction} spent {amount} tokens. Reason: {reason}. Remaining: {data.CurrentTokens}");
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Process a token generation cycle
        /// </summary>
        [Server]
        private void ProcessTokenCycle()
        {
            if (!isInitialized)
                return;
                
            Debug.Log("[TokenSystem] Processing token generation cycle...");
            
            // Get all war map nodes
            WarMapNode[] nodes = FindObjectsByType<WarMapNode>(FindObjectsSortMode.None);
            
            // Calculate token generation for each faction
            Dictionary<Team, int> tokenGeneration = new Dictionary<Team, int>
            {
                { Team.Blue, 0 },
                { Team.Red, 0 },
                { Team.Green, 0 }
            };
            
            foreach (var node in nodes)
            {
                if (node.ControllingFaction != Team.None && !node.IsContested)
                {
                    int nodeTokens = node.CalculateTokenGeneration();
                    tokenGeneration[node.ControllingFaction] += nodeTokens;
                }
            }
            
            // Add base token generation
            foreach (var faction in tokenGeneration.Keys)
            {
                if (faction != Team.None)
                {
                    int totalGeneration = tokenGeneration[faction] + baseTokensPerCycle;
                    AddTokens(faction, totalGeneration, "Cycle Generation");
                }
            }
            
            OnTokenCycleCompleted?.Invoke();
        }
        
        /// <summary>
        /// Update the synced token values
        /// </summary>
        [Server]
        private void UpdateSyncedTokens(Team faction, int newValue)
        {
            switch (faction)
            {
                case Team.Blue:
                    blueTokens = newValue;
                    break;
                case Team.Red:
                    redTokens = newValue;
                    break;
                case Team.Green:
                    greenTokens = newValue;
                    break;
            }
        }
        
        #endregion
        
        #region Battle Integration
        
        /// <summary>
        /// Process tokens for starting a battle
        /// </summary>
        [Server]
        public bool InitiateBattle(Team attackingFaction, WarMapNode targetNode)
        {
            if (!SpendTokens(attackingFaction, attackCost, $"Attack on {targetNode.NodeName}"))
            {
                return false;
            }
            
            // Battle can proceed
            targetNode.StartBattle(attackingFaction);
            return true;
        }
        
        /// <summary>
        /// Process battle rewards based on FPS match results
        /// </summary>
        [Server]
        public void ProcessBattleRewards(BattleResult result)
        {
            // Winner gets bonus tokens
            if (result.WinnerFaction != Team.None)
            {
                AddTokens(result.WinnerFaction, winBonusReward, "Battle Victory");
                
                // Add tokens based on control gained
                int controlBonus = Mathf.RoundToInt(result.ControlChange * 2);
                AddTokens(result.WinnerFaction, controlBonus, "Territory Control");
            }
            
            // Process individual player contributions
            foreach (var kvp in result.PlayerScores)
            {
                // In a real implementation, we'd track which faction each player belongs to
                // For now, we'll assume this is handled elsewhere
            }
        }
        
        /// <summary>
        /// Award tokens for FPS gameplay actions
        /// </summary>
        [Server]
        public void AwardPlayerTokens(Team faction, string playerId, TokenRewardType rewardType)
        {
            int reward = 0;
            string reason = "";
            
            switch (rewardType)
            {
                case TokenRewardType.Kill:
                    reward = killReward;
                    reason = "Enemy Elimination";
                    break;
                case TokenRewardType.Capture:
                    reward = captureReward;
                    reason = "Point Capture";
                    break;
                case TokenRewardType.Participation:
                    reward = participationReward;
                    reason = "Battle Participation";
                    break;
            }
            
            if (reward > 0)
            {
                AddTokens(faction, reward, reason);
                
                // Track individual player contributions (for future leaderboards)
                if (factionTokens.ContainsKey(faction))
                {
                    factionTokens[faction].RecordPlayerContribution(playerId, reward);
                }
            }
        }
        
        #endregion
        
        #region Strategic Actions
        
        /// <summary>
        /// Fortify a node using tokens
        /// </summary>
        [Server]
        public bool FortifyNode(Team faction, WarMapNode node)
        {
            if (node.ControllingFaction != faction)
            {
                Debug.LogWarning($"[TokenSystem] {faction} cannot fortify {node.NodeName} - not owned");
                return false;
            }
            
            if (!SpendTokens(faction, fortifyCost, $"Fortify {node.NodeName}"))
            {
                return false;
            }
            
            // Increase control percentage
            float currentControl = node.ControlPercentage;
            node.SetControl(faction, Mathf.Min(100f, currentControl + 25f));
            
            return true;
        }
        
        /// <summary>
        /// Send reinforcements to an ongoing battle
        /// </summary>
        [Server]
        public bool SendReinforcements(Team faction, WarMapNode battleNode)
        {
            if (!battleNode.IsBattleActive)
            {
                Debug.LogWarning($"[TokenSystem] No active battle at {battleNode.NodeName}");
                return false;
            }
            
            if (!SpendTokens(faction, reinforcementCost, $"Reinforce {battleNode.NodeName}"))
            {
                return false;
            }
            
            // In a real implementation, this would affect the ongoing FPS battle
            // For now, we'll just log it
            Debug.Log($"[TokenSystem] {faction} sent reinforcements to {battleNode.NodeName}");
            
            return true;
        }
        
        #endregion
        
        #region Network Sync Hooks
        
        private void OnBlueTokensChanged(int oldValue, int newValue)
        {
            if (factionTokens.ContainsKey(Team.Blue))
            {
                factionTokens[Team.Blue].CurrentTokens = newValue;
                OnTokensChanged?.Invoke(Team.Blue, newValue);
            }
        }
        
        private void OnRedTokensChanged(int oldValue, int newValue)
        {
            if (factionTokens.ContainsKey(Team.Red))
            {
                factionTokens[Team.Red].CurrentTokens = newValue;
                OnTokensChanged?.Invoke(Team.Red, newValue);
            }
        }
        
        private void OnGreenTokensChanged(int oldValue, int newValue)
        {
            if (factionTokens.ContainsKey(Team.Green))
            {
                factionTokens[Team.Green].CurrentTokens = newValue;
                OnTokensChanged?.Invoke(Team.Green, newValue);
            }
        }
        
        #endregion
        
        #region Data Classes
        
        /// <summary>
        /// Tracks token data for a faction
        /// </summary>
        [Serializable]
        private class FactionTokenData
        {
            public Team Faction;
            public int CurrentTokens;
            public int TotalEarned;
            public int TotalSpent;
            public List<TokenTransaction> TransactionHistory;
            public Dictionary<string, int> PlayerContributions;
            
            public FactionTokenData(Team faction, int startingTokens)
            {
                Faction = faction;
                CurrentTokens = startingTokens;
                TotalEarned = startingTokens;
                TotalSpent = 0;
                TransactionHistory = new List<TokenTransaction>();
                PlayerContributions = new Dictionary<string, int>();
            }
            
            public void AddTransaction(TokenTransaction transaction)
            {
                TransactionHistory.Add(transaction);
                
                // Keep only last 100 transactions
                if (TransactionHistory.Count > 100)
                {
                    TransactionHistory.RemoveAt(0);
                }
            }
            
            public void RecordPlayerContribution(string playerId, int amount)
            {
                if (!PlayerContributions.ContainsKey(playerId))
                    PlayerContributions[playerId] = 0;
                    
                PlayerContributions[playerId] += amount;
            }
        }
        
        /// <summary>
        /// Represents a single token transaction
        /// </summary>
        [Serializable]
        private struct TokenTransaction
        {
            public int Amount;
            public string Reason;
            public float Timestamp;
            public bool IsIncome;
        }
        
        /// <summary>
        /// Types of token rewards in FPS gameplay
        /// </summary>
        public enum TokenRewardType
        {
            Kill,
            Capture,
            Participation,
            Assist,
            Objective
        }
        
        #endregion
    }
}