using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using ElitesAndPawns.Core;

namespace ElitesAndPawns.WarMap
{
    /// <summary>
    /// Manages the faction token economy for the War Map system.
    /// Tokens represent available manpower and are earned ONLY from holding nodes.
    /// Tokens are spent to resupply player squads (1:1 ratio).
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
        
        [Header("Token Generation")]
        [Tooltip("Base tokens each faction gets per cycle regardless of territory")]
        [SerializeField] private int baseTokensPerCycle = 50;
        
        [Tooltip("Seconds between token generation cycles")]
        [SerializeField] private float tokenCycleDuration = 60f;
        
        [Tooltip("Maximum tokens a faction can stockpile")]
        [SerializeField] private int maxTokensPerFaction = 10000;
        
        [Tooltip("Tokens each faction starts with")]
        [SerializeField] private int startingTokens = 500;
        
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
        
        /// <summary>
        /// Get the current token count for a faction.
        /// </summary>
        public int GetFactionTokens(Team faction)
        {
            if (factionTokens.ContainsKey(faction))
                return factionTokens[faction].CurrentTokens;
            return 0;
        }
        
        /// <summary>
        /// Check if a faction can afford a given cost.
        /// </summary>
        public bool CanAfford(Team faction, int cost)
        {
            return GetFactionTokens(faction) >= cost;
        }
        
        /// <summary>
        /// Time in seconds until next token generation cycle.
        /// </summary>
        public float TimeToNextCycle => Mathf.Max(0f, nextCycleTime - Time.time);
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when a faction's token count changes.
        /// Parameters: faction, newTotal
        /// </summary>
        public static event Action<Team, int> OnTokensChanged;
        
        /// <summary>
        /// Fired when tokens are spent.
        /// Parameters: faction, amount, reason
        /// </summary>
        public static event Action<Team, int, string> OnTokensSpent;
        
        /// <summary>
        /// Fired when tokens are earned.
        /// Parameters: faction, amount, reason
        /// </summary>
        public static event Action<Team, int, string> OnTokensEarned;
        
        /// <summary>
        /// Fired when a token generation cycle completes.
        /// </summary>
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
            
            Debug.Log($"[TokenSystem] Initialized with {startingTokens} starting tokens per faction");
        }
        
        #endregion
        
        #region Token Management
        
        /// <summary>
        /// Add tokens to a faction.
        /// </summary>
        /// <param name="faction">The faction to receive tokens</param>
        /// <param name="amount">Amount of tokens to add</param>
        /// <param name="reason">Reason for the addition (for logging/UI)</param>
        [Server]
        public void AddTokens(Team faction, int amount, string reason = "")
        {
            if (!isInitialized || faction == Team.None)
                return;
                
            if (factionTokens.ContainsKey(faction))
            {
                var data = factionTokens[faction];
                int previousTokens = data.CurrentTokens;
                data.CurrentTokens = Mathf.Min(data.CurrentTokens + amount, maxTokensPerFaction);
                int actualAdded = data.CurrentTokens - previousTokens;
                data.TotalEarned += actualAdded;
                
                // Update synced values
                UpdateSyncedTokens(faction, data.CurrentTokens);
                
                // Log transaction
                data.AddTransaction(new TokenTransaction
                {
                    Amount = actualAdded,
                    Reason = reason,
                    Timestamp = Time.time,
                    IsIncome = true
                });
                
                OnTokensEarned?.Invoke(faction, actualAdded, reason);
                OnTokensChanged?.Invoke(faction, data.CurrentTokens);
                
                if (!string.IsNullOrEmpty(reason))
                {
                    Debug.Log($"[TokenSystem] {faction} +{actualAdded} tokens ({reason}). Total: {data.CurrentTokens}");
                }
            }
        }
        
        /// <summary>
        /// Spend tokens from a faction.
        /// </summary>
        /// <param name="faction">The faction spending tokens</param>
        /// <param name="amount">Amount of tokens to spend</param>
        /// <param name="reason">Reason for the expenditure (for logging/UI)</param>
        /// <returns>True if successful, false if insufficient funds</returns>
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
                
                if (!string.IsNullOrEmpty(reason))
                {
                    Debug.Log($"[TokenSystem] {faction} -{amount} tokens ({reason}). Remaining: {data.CurrentTokens}");
                }
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Process a token generation cycle.
        /// Tokens are generated based on controlled nodes (the "fort producing manpower" logic).
        /// </summary>
        [Server]
        private void ProcessTokenCycle()
        {
            if (!isInitialized)
                return;
                
            Debug.Log("[TokenSystem] === Token Generation Cycle ===");
            
            // Get all war map nodes
            WarMapNode[] nodes = FindObjectsByType<WarMapNode>(FindObjectsSortMode.None);
            
            // Calculate token generation for each faction based on held territory
            Dictionary<Team, int> territoryGeneration = new Dictionary<Team, int>
            {
                { Team.Blue, 0 },
                { Team.Red, 0 },
                { Team.Green, 0 }
            };
            
            foreach (var node in nodes)
            {
                // Only generate tokens from uncontested, controlled nodes
                if (node.ControllingFaction != Team.None && !node.IsContested && !node.IsBattleActive)
                {
                    int nodeTokens = node.CalculateTokenGeneration();
                    territoryGeneration[node.ControllingFaction] += nodeTokens;
                }
            }
            
            // Award tokens to each faction
            foreach (var kvp in territoryGeneration)
            {
                Team faction = kvp.Key;
                int territoryTokens = kvp.Value;
                int totalGeneration = territoryTokens + baseTokensPerCycle;
                
                if (totalGeneration > 0)
                {
                    AddTokens(faction, totalGeneration, $"Production (+{baseTokensPerCycle} base, +{territoryTokens} territory)");
                }
            }
            
            OnTokenCycleCompleted?.Invoke();
        }
        
        /// <summary>
        /// Update the network-synced token values.
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
        
        #region Debug & Admin
        
        /// <summary>
        /// Get transaction history for a faction.
        /// </summary>
        public List<TokenTransaction> GetTransactionHistory(Team faction)
        {
            if (factionTokens.TryGetValue(faction, out var data))
            {
                return new List<TokenTransaction>(data.TransactionHistory);
            }
            return new List<TokenTransaction>();
        }
        
        /// <summary>
        /// Get total tokens earned by a faction since war start.
        /// </summary>
        public int GetTotalEarned(Team faction)
        {
            if (factionTokens.TryGetValue(faction, out var data))
                return data.TotalEarned;
            return 0;
        }
        
        /// <summary>
        /// Get total tokens spent by a faction since war start.
        /// </summary>
        public int GetTotalSpent(Team faction)
        {
            if (factionTokens.TryGetValue(faction, out var data))
                return data.TotalSpent;
            return 0;
        }
        
        /// <summary>
        /// Force a token cycle (for testing).
        /// </summary>
        [Server]
        public void ForceTokenCycle()
        {
            ProcessTokenCycle();
        }
        
        /// <summary>
        /// Set tokens directly (for testing/admin).
        /// </summary>
        [Server]
        public void SetTokens(Team faction, int amount)
        {
            if (factionTokens.ContainsKey(faction))
            {
                factionTokens[faction].CurrentTokens = Mathf.Clamp(amount, 0, maxTokensPerFaction);
                UpdateSyncedTokens(faction, factionTokens[faction].CurrentTokens);
                OnTokensChanged?.Invoke(faction, factionTokens[faction].CurrentTokens);
            }
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
        /// Tracks token data for a faction.
        /// </summary>
        [Serializable]
        private class FactionTokenData
        {
            public Team Faction;
            public int CurrentTokens;
            public int TotalEarned;
            public int TotalSpent;
            public List<TokenTransaction> TransactionHistory;
            
            public FactionTokenData(Team faction, int startingTokens)
            {
                Faction = faction;
                CurrentTokens = startingTokens;
                TotalEarned = startingTokens;
                TotalSpent = 0;
                TransactionHistory = new List<TokenTransaction>();
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
        }
        
        /// <summary>
        /// Represents a single token transaction for history tracking.
        /// </summary>
        [Serializable]
        public struct TokenTransaction
        {
            public int Amount;
            public string Reason;
            public float Timestamp;
            public bool IsIncome;
        }
        
        #endregion
    }
}
