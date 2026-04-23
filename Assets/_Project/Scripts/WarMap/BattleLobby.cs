using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using ElitesAndPawns.Core;

namespace ElitesAndPawns.WarMap
{
    /// <summary>
    /// Manages the pre-battle lobby where players wait before the battle starts.
    /// Handles countdown, player readiness, and battle initiation.
    /// </summary>
    public class BattleLobby : NetworkBehaviour
    {
        #region Singleton
        
        private static BattleLobby _instance;
        public static BattleLobby Instance => _instance;
        
        #endregion
        
        #region Configuration
        
        [Header("Lobby Settings")]
        [SerializeField] private float defaultCountdownTime = 30f;
        [SerializeField] private float minCountdownTime = 10f;
        [SerializeField] private int minPlayersToStart = 1; // For testing, increase for production
        [SerializeField] private bool autoStartWhenReady = true;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = true;
        
        #endregion
        
        #region Synced State
        
        [SyncVar(hook = nameof(OnLobbyStateChanged))]
        private LobbyState currentState = LobbyState.Inactive;
        
        [SyncVar(hook = nameof(OnCountdownChanged))]
        private float countdownRemaining;
        
        [SyncVar]
        private int attackerPlayerCount;
        
        [SyncVar]
        private int defenderPlayerCount;
        
        [SyncVar]
        private int attackerReadyCount;
        
        [SyncVar]
        private int defenderReadyCount;
        
        #endregion
        
        #region Server State
        
        private BattleParameters battleParameters;
        private HashSet<uint> attackerPlayers = new HashSet<uint>();
        private HashSet<uint> defenderPlayers = new HashSet<uint>();
        private HashSet<uint> readyPlayers = new HashSet<uint>();
        private float countdownStartTime;
        
        #endregion
        
        #region Events
        
        public static event Action<LobbyState> OnLobbyStateChanged_Event;
        public static event Action<float> OnCountdownTick; // remaining seconds
        public static event Action OnLobbyStarted;
        public static event Action OnBattleStarting;
        public static event Action<uint, bool> OnPlayerReadyChanged; // netId, isReady
        
        #endregion
        
        #region Properties
        
        public LobbyState State => currentState;
        public float CountdownRemaining => countdownRemaining;
        public int AttackerCount => attackerPlayerCount;
        public int DefenderCount => defenderPlayerCount;
        public int AttackerReady => attackerReadyCount;
        public int DefenderReady => defenderReadyCount;
        public int TotalPlayers => attackerPlayerCount + defenderPlayerCount;
        public bool CanStart => TotalPlayers >= minPlayersToStart;
        public BattleParameters Parameters => battleParameters;
        
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
            if (!isServer) return;
            
            if (currentState == LobbyState.Countdown)
            {
                UpdateCountdown();
            }
        }
        
        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
        
        #endregion
        
        #region Lobby Initialization
        
        /// <summary>
        /// Initialize the lobby with battle parameters.
        /// </summary>
        [Server]
        public void InitializeLobby(BattleParameters parameters)
        {
            battleParameters = parameters;
            
            attackerPlayers.Clear();
            defenderPlayers.Clear();
            readyPlayers.Clear();
            
            attackerPlayerCount = 0;
            defenderPlayerCount = 0;
            attackerReadyCount = 0;
            defenderReadyCount = 0;
            
            countdownRemaining = parameters.LobbyCountdown > 0 ? parameters.LobbyCountdown : defaultCountdownTime;
            
            currentState = LobbyState.WaitingForPlayers;
            
            Debug.Log($"[BattleLobby] Lobby initialized for node {parameters.NodeId}");
            Debug.Log($"[BattleLobby] {parameters.AttackingFaction} (Attacker) vs {parameters.DefendingFaction} (Defender)");
            
            RpcNotifyLobbyStarted(parameters.NodeId, parameters.NodeName, 
                                   parameters.AttackingFaction, parameters.DefendingFaction);
            
            OnLobbyStarted?.Invoke();
        }
        
        #endregion
        
        #region Player Management
        
        /// <summary>
        /// Join the lobby for a specific faction.
        /// </summary>
        [Command(requiresAuthority = false)]
        public void CmdJoinLobby(uint playerNetId, FactionType faction)
        {
            if (currentState == LobbyState.Inactive || currentState == LobbyState.BattleStarting)
            {
                Debug.LogWarning($"[BattleLobby] Cannot join - lobby state is {currentState}");
                return;
            }
            
            // Remove from other faction if already in
            if (attackerPlayers.Contains(playerNetId))
            {
                attackerPlayers.Remove(playerNetId);
            }
            if (defenderPlayers.Contains(playerNetId))
            {
                defenderPlayers.Remove(playerNetId);
            }
            readyPlayers.Remove(playerNetId);
            
            // Add to appropriate faction
            if (faction == battleParameters.AttackingFaction)
            {
                attackerPlayers.Add(playerNetId);
                Debug.Log($"[BattleLobby] Player {playerNetId} joined as attacker");
            }
            else if (faction == battleParameters.DefendingFaction)
            {
                defenderPlayers.Add(playerNetId);
                Debug.Log($"[BattleLobby] Player {playerNetId} joined as defender");
            }
            
            UpdatePlayerCounts();
            CheckLobbyState();
        }
        
        /// <summary>
        /// Leave the lobby.
        /// </summary>
        [Command(requiresAuthority = false)]
        public void CmdLeaveLobby(uint playerNetId)
        {
            attackerPlayers.Remove(playerNetId);
            defenderPlayers.Remove(playerNetId);
            readyPlayers.Remove(playerNetId);
            
            Debug.Log($"[BattleLobby] Player {playerNetId} left lobby");
            
            UpdatePlayerCounts();
            CheckLobbyState();
        }
        
        /// <summary>
        /// Toggle ready state.
        /// </summary>
        [Command(requiresAuthority = false)]
        public void CmdSetReady(uint playerNetId, bool isReady)
        {
            if (!attackerPlayers.Contains(playerNetId) && !defenderPlayers.Contains(playerNetId))
            {
                Debug.LogWarning($"[BattleLobby] Player {playerNetId} not in lobby, cannot set ready");
                return;
            }
            
            if (isReady)
            {
                readyPlayers.Add(playerNetId);
            }
            else
            {
                readyPlayers.Remove(playerNetId);
            }
            
            UpdatePlayerCounts();
            RpcNotifyPlayerReadyChanged(playerNetId, isReady);
            OnPlayerReadyChanged?.Invoke(playerNetId, isReady);
            
            CheckLobbyState();
        }
        
        /// <summary>
        /// Server-side join (bypass command).
        /// </summary>
        [Server]
        public void ServerJoinLobby(uint playerNetId, FactionType faction)
        {
            CmdJoinLobby(playerNetId, faction);
        }
        
        [Server]
        private void UpdatePlayerCounts()
        {
            attackerPlayerCount = attackerPlayers.Count;
            defenderPlayerCount = defenderPlayers.Count;
            
            attackerReadyCount = 0;
            defenderReadyCount = 0;
            
            foreach (uint netId in readyPlayers)
            {
                if (attackerPlayers.Contains(netId))
                    attackerReadyCount++;
                else if (defenderPlayers.Contains(netId))
                    defenderReadyCount++;
            }
        }
        
        #endregion
        
        #region Countdown
        
        [Server]
        private void CheckLobbyState()
        {
            if (currentState == LobbyState.BattleStarting)
                return;
            
            bool hasEnoughPlayers = TotalPlayers >= minPlayersToStart;
            bool allReady = readyPlayers.Count == TotalPlayers && TotalPlayers > 0;
            
            if (hasEnoughPlayers)
            {
                if (currentState == LobbyState.WaitingForPlayers)
                {
                    // Enough players, start countdown
                    StartCountdown();
                }
                else if (currentState == LobbyState.Countdown && allReady && autoStartWhenReady)
                {
                    // All players ready, accelerate countdown
                    if (countdownRemaining > minCountdownTime)
                    {
                        countdownRemaining = minCountdownTime;
                        Debug.Log($"[BattleLobby] All players ready! Countdown accelerated to {minCountdownTime}s");
                    }
                }
            }
            else if (currentState == LobbyState.Countdown)
            {
                // Lost players, pause countdown
                currentState = LobbyState.WaitingForPlayers;
                Debug.Log("[BattleLobby] Not enough players, countdown paused");
            }
        }
        
        [Server]
        private void StartCountdown()
        {
            currentState = LobbyState.Countdown;
            countdownStartTime = Time.time;
            countdownRemaining = battleParameters?.LobbyCountdown ?? defaultCountdownTime;
            
            Debug.Log($"[BattleLobby] Countdown started: {countdownRemaining}s");
        }
        
        [Server]
        private void UpdateCountdown()
        {
            float elapsed = Time.time - countdownStartTime;
            float totalTime = battleParameters?.LobbyCountdown ?? defaultCountdownTime;
            countdownRemaining = Mathf.Max(0, totalTime - elapsed);
            
            OnCountdownTick?.Invoke(countdownRemaining);
            
            if (countdownRemaining <= 0)
            {
                StartBattle();
            }
        }
        
        [Server]
        private void StartBattle()
        {
            currentState = LobbyState.BattleStarting;
            
            Debug.Log("[BattleLobby] Starting battle!");
            
            OnBattleStarting?.Invoke();
            RpcNotifyBattleStarting();
            
            // Register all lobby players with BattleManager
            if (BattleManager.Instance != null)
            {
                BattleManager.Instance.StartLobby();
                
                // Small delay then start actual battle
                Invoke(nameof(TriggerBattleStart), 3f);
            }
        }
        
        [Server]
        private void TriggerBattleStart()
        {
            if (BattleManager.Instance != null)
            {
                BattleManager.Instance.StartBattle();
            }
            
            currentState = LobbyState.Inactive;
        }
        
        /// <summary>
        /// Force start the battle (for testing).
        /// </summary>
        [Server]
        public void ForceStart()
        {
            if (currentState == LobbyState.WaitingForPlayers || currentState == LobbyState.Countdown)
            {
                Debug.Log("[BattleLobby] Force starting battle");
                StartBattle();
            }
        }
        
        #endregion
        
        #region Queries
        
        /// <summary>
        /// Check if a player is in the lobby.
        /// </summary>
        public bool IsPlayerInLobby(uint playerNetId)
        {
            return attackerPlayers.Contains(playerNetId) || defenderPlayers.Contains(playerNetId);
        }
        
        /// <summary>
        /// Get which faction a player is on.
        /// </summary>
        public FactionType GetPlayerFaction(uint playerNetId)
        {
            if (attackerPlayers.Contains(playerNetId))
                return battleParameters?.AttackingFaction ?? FactionType.None;
            if (defenderPlayers.Contains(playerNetId))
                return battleParameters?.DefendingFaction ?? FactionType.None;
            return FactionType.None;
        }
        
        /// <summary>
        /// Check if a player is ready.
        /// </summary>
        public bool IsPlayerReady(uint playerNetId)
        {
            return readyPlayers.Contains(playerNetId);
        }
        
        #endregion
        
        #region RPCs
        
        [ClientRpc]
        private void RpcNotifyLobbyStarted(int nodeId, string nodeName, FactionType attacker, FactionType defender)
        {
            Debug.Log($"[BattleLobby] Lobby started for {nodeName} - {attacker} vs {defender}");
            OnLobbyStarted?.Invoke();
        }
        
        [ClientRpc]
        private void RpcNotifyPlayerReadyChanged(uint playerNetId, bool isReady)
        {
            OnPlayerReadyChanged?.Invoke(playerNetId, isReady);
        }
        
        [ClientRpc]
        private void RpcNotifyBattleStarting()
        {
            Debug.Log("[BattleLobby] Battle starting!");
            OnBattleStarting?.Invoke();
        }
        
        #endregion
        
        #region Hooks
        
        private void OnLobbyStateChanged(LobbyState oldState, LobbyState newState)
        {
            Debug.Log($"[BattleLobby] State: {oldState} -> {newState}");
            OnLobbyStateChanged_Event?.Invoke(newState);
        }
        
        private void OnCountdownChanged(float oldValue, float newValue)
        {
            OnCountdownTick?.Invoke(newValue);
        }
        
        #endregion
    }
    
    /// <summary>
    /// States for the battle lobby.
    /// </summary>
    public enum LobbyState
    {
        Inactive,
        WaitingForPlayers,
        Countdown,
        BattleStarting
    }
}
