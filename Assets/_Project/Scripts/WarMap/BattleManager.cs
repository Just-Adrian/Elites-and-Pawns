using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using ElitesAndPawns.Core;
using ElitesAndPawns.GameModes;
using ElitesAndPawns.Networking;

namespace ElitesAndPawns.WarMap
{
    /// <summary>
    /// Manages an FPS battle instance, integrating with the war map system.
    /// Handles spawn ticket consumption, reinforcements, retreats, and battle results.
    /// </summary>
    public class BattleManager : NetworkBehaviour
    {
        #region Singleton
        
        private static BattleManager _instance;
        public static BattleManager Instance => _instance;
        
        #endregion
        
        #region Configuration
        
        [Header("Battle Settings")]
        [SerializeField] private float ticketCheckInterval = 1f;
        
        [Header("Integration")]
        [SerializeField] private bool integrateWithGameModeManager = true;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = true;
        
        #endregion
        
        #region Synced State
        
        [SyncVar(hook = nameof(OnBattleStateChanged))]
        private BattleState currentState = BattleState.Inactive;
        
        [SyncVar]
        private int syncedAttackerTickets;
        
        [SyncVar]
        private int syncedDefenderTickets;
        
        [SyncVar]
        private int syncedNodeId;
        
        [SyncVar]
        private Team syncedAttacker;
        
        [SyncVar]
        private Team syncedDefender;
        
        [SyncVar]
        private string syncedNodeName;
        
        #endregion
        
        #region Server State
        
        private BattleParameters battleParameters;
        private float lastTicketCheck;
        private HashSet<uint> playersInBattle = new HashSet<uint>();
        
        // Track player-to-squad mapping
        private Dictionary<uint, string> playerSquadAssignments = new Dictionary<uint, string>();
        
        #endregion
        
        #region Events
        
        public static event Action<BattleState> OnBattleStateChanged_Event;
        public static event Action<Team, int> OnTicketsChanged; // faction, newCount
        public static event Action<Team> OnBattleEnded; // winner
        public static event Action<string> OnSquadReinforced; // squadId
        public static event Action<string> OnSquadRetreated; // squadId
        
        #endregion
        
        #region Properties
        
        public BattleState State => currentState;
        public int AttackerTickets => syncedAttackerTickets;
        public int DefenderTickets => syncedDefenderTickets;
        public int NodeId => syncedNodeId;
        public Team AttackingFaction => syncedAttacker;
        public Team DefendingFaction => syncedDefender;
        public string NodeName => syncedNodeName;
        public BattleParameters Parameters => battleParameters;
        public bool IsBattleActive => currentState == BattleState.InProgress;
        
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
            if (currentState != BattleState.InProgress) return;
            
            // Periodic ticket check
            if (Time.time - lastTicketCheck >= ticketCheckInterval)
            {
                lastTicketCheck = Time.time;
                CheckBattleEndConditions();
            }
        }
        
        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
        
        #endregion
        
        #region Battle Initialization
        
        /// <summary>
        /// Initialize battle with parameters from the war map.
        /// Called by BattleSceneBridge after scene loads.
        /// </summary>
        [Server]
        public void InitializeBattle(BattleParameters parameters)
        {
            if (parameters == null)
            {
                Debug.LogError("[BattleManager] Cannot initialize - parameters are null!");
                return;
            }
            
            battleParameters = parameters;
            
            // Sync to clients
            syncedNodeId = parameters.NodeId;
            syncedNodeName = parameters.NodeName;
            syncedAttacker = parameters.AttackingFaction;
            syncedDefender = parameters.DefendingFaction;
            syncedAttackerTickets = parameters.AttackerSpawnTickets;
            syncedDefenderTickets = parameters.DefenderSpawnTickets;
            
            currentState = BattleState.WaitingForPlayers;
            
            Debug.Log($"[BattleManager] Battle initialized for node {parameters.NodeId} ({parameters.NodeName})");
            Debug.Log($"[BattleManager] {parameters.AttackingFaction} ({parameters.AttackerSpawnTickets} tickets) vs " +
                      $"{parameters.DefendingFaction} ({parameters.DefenderSpawnTickets} tickets)");
            
            // Notify CaptureController that battle is starting
            if (CaptureController.Instance != null)
            {
                CaptureController.Instance.OnBattleStarted(parameters.NodeId);
            }
        }
        
        /// <summary>
        /// Start the battle (called after lobby countdown).
        /// </summary>
        [Server]
        public void StartBattle()
        {
            if (currentState != BattleState.WaitingForPlayers && currentState != BattleState.Lobby)
            {
                Debug.LogWarning($"[BattleManager] Cannot start battle - current state is {currentState}");
                return;
            }
            
            currentState = BattleState.InProgress;
            lastTicketCheck = Time.time;
            
            // Start the game mode if integrated
            if (integrateWithGameModeManager && GameModeManager.Instance != null)
            {
                GameModeManager.Instance.StartGame();
            }
            
            Debug.Log("[BattleManager] Battle started!");
            RpcNotifyBattleStarted();
        }
        
        /// <summary>
        /// Transition to lobby state (waiting for countdown).
        /// </summary>
        [Server]
        public void StartLobby()
        {
            currentState = BattleState.Lobby;
            Debug.Log("[BattleManager] Battle lobby started");
        }
        
        #endregion
        
        #region Spawn Ticket Management
        
        /// <summary>
        /// Request a spawn ticket for a player.
        /// Returns true if ticket was consumed successfully.
        /// </summary>
        [Server]
        public bool RequestSpawnTicket(NetworkPlayer player)
        {
            if (battleParameters == null || currentState != BattleState.InProgress)
                return false;
            
            Team playerFaction = player.Faction == FactionType.Blue ? Team.Blue : 
                                 player.Faction == FactionType.Red ? Team.Red : Team.None;
            
            if (playerFaction == Team.None)
            {
                Debug.LogWarning($"[BattleManager] Player {player.PlayerName} has no faction!");
                return false;
            }
            
            // Try to consume from player's own squad first
            string usedSquadId = battleParameters.ConsumeTicketFromFaction(playerFaction, player.netId);
            
            if (usedSquadId != null)
            {
                // Track which squad this player used
                playerSquadAssignments[player.netId] = usedSquadId;
                
                // Update synced values
                UpdateSyncedTickets();
                
                if (debugMode)
                {
                    Debug.Log($"[BattleManager] {player.PlayerName} consumed spawn ticket from squad {usedSquadId}");
                }
                
                return true;
            }
            
            Debug.Log($"[BattleManager] No spawn tickets available for {playerFaction}!");
            return false;
        }
        
        /// <summary>
        /// Check if a faction has spawn tickets available.
        /// </summary>
        public bool HasSpawnTickets(Team faction)
        {
            if (battleParameters == null) return false;
            return battleParameters.GetFactionTickets(faction) > 0;
        }
        
        /// <summary>
        /// Get remaining tickets for a faction.
        /// </summary>
        public int GetFactionTickets(Team faction)
        {
            if (battleParameters == null) return 0;
            return battleParameters.GetFactionTickets(faction);
        }
        
        [Server]
        private void UpdateSyncedTickets()
        {
            if (battleParameters == null) return;
            
            int oldAttacker = syncedAttackerTickets;
            int oldDefender = syncedDefenderTickets;
            
            syncedAttackerTickets = battleParameters.GetFactionTickets(battleParameters.AttackingFaction);
            syncedDefenderTickets = battleParameters.GetFactionTickets(battleParameters.DefendingFaction);
            
            // Fire events if changed
            if (oldAttacker != syncedAttackerTickets)
            {
                OnTicketsChanged?.Invoke(battleParameters.AttackingFaction, syncedAttackerTickets);
                RpcNotifyTicketsChanged(battleParameters.AttackingFaction, syncedAttackerTickets);
            }
            
            if (oldDefender != syncedDefenderTickets)
            {
                OnTicketsChanged?.Invoke(battleParameters.DefendingFaction, syncedDefenderTickets);
                RpcNotifyTicketsChanged(battleParameters.DefendingFaction, syncedDefenderTickets);
            }
        }
        
        #endregion
        
        #region Reinforcements & Retreats
        
        /// <summary>
        /// Called when a new squad arrives at the contested node.
        /// Adds their manpower to the battle.
        /// </summary>
        [Server]
        public void OnSquadArrived(Squad squad)
        {
            if (battleParameters == null || currentState == BattleState.Inactive)
                return;
            
            var squadData = new SquadBattleData
            {
                SquadId = squad.SquadId,
                OwnerNetId = squad.OwnerNetId,
                OwnerDisplayName = squad.OwnerDisplayName,
                Faction = squad.Faction,
                InitialManpower = squad.Manpower,
                CurrentManpower = squad.Manpower
            };
            
            battleParameters.AddSquad(squadData);
            UpdateSyncedTickets();
            
            OnSquadReinforced?.Invoke(squad.SquadId);
            RpcNotifyReinforcements(squad.SquadId, squad.Faction, squad.Manpower);
            
            Debug.Log($"[BattleManager] Reinforcements: {squad.SquadId} arrived with {squad.Manpower} tickets");
        }
        
        /// <summary>
        /// Called when a squad retreats from the contested node.
        /// Removes their remaining manpower from the battle.
        /// </summary>
        [Server]
        public void HandleSquadRetreated(string squadId)
        {
            if (battleParameters == null)
                return;
            
            battleParameters.RemoveSquad(squadId);
            UpdateSyncedTickets();
            
            OnSquadRetreated?.Invoke(squadId);
            RpcNotifySquadRetreated(squadId);
            
            Debug.Log($"[BattleManager] Squad retreated: {squadId}");
            
            // Check if battle should end due to retreat
            CheckBattleEndConditions();
        }
        
        #endregion
        
        #region Battle End Conditions
        
        [Server]
        private void CheckBattleEndConditions()
        {
            if (battleParameters == null) return;
            
            int attackerTickets = battleParameters.GetFactionTickets(battleParameters.AttackingFaction);
            int defenderTickets = battleParameters.GetFactionTickets(battleParameters.DefendingFaction);
            
            // Check for ticket depletion
            if (attackerTickets <= 0 && defenderTickets > 0)
            {
                EndBattle(battleParameters.DefendingFaction, BattleEndReason.TicketsExhausted);
            }
            else if (defenderTickets <= 0 && attackerTickets > 0)
            {
                EndBattle(battleParameters.AttackingFaction, BattleEndReason.TicketsExhausted);
            }
            else if (attackerTickets <= 0 && defenderTickets <= 0)
            {
                // Both exhausted - check game mode score or declare draw
                Team winner = DetermineWinnerByScore();
                EndBattle(winner, BattleEndReason.MutualExhaustion);
            }
            
            // Also check GameModeManager victory conditions
            if (integrateWithGameModeManager && GameModeManager.Instance != null)
            {
                if (!GameModeManager.Instance.IsGameActive && GameModeManager.Instance.WinningTeam != FactionType.None)
                {
                    Team winner = GameModeManager.Instance.WinningTeam == FactionType.Blue ? Team.Blue : Team.Red;
                    EndBattle(winner, BattleEndReason.ObjectiveCompleted);
                }
            }
        }
        
        private Team DetermineWinnerByScore()
        {
            if (GameModeManager.Instance != null)
            {
                if (GameModeManager.Instance.BlueScore > GameModeManager.Instance.RedScore)
                    return Team.Blue;
                else if (GameModeManager.Instance.RedScore > GameModeManager.Instance.BlueScore)
                    return Team.Red;
            }
            return Team.None; // Draw
        }
        
        /// <summary>
        /// End the battle with a winner.
        /// </summary>
        [Server]
        public void EndBattle(Team winner, BattleEndReason reason)
        {
            if (currentState != BattleState.InProgress)
                return;
            
            currentState = BattleState.Ended;
            
            Debug.Log($"[BattleManager] Battle ended! Winner: {winner}, Reason: {reason}");
            
            // Stop game mode
            if (integrateWithGameModeManager && GameModeManager.Instance != null)
            {
                FactionType winnerFaction = winner == Team.Blue ? FactionType.Blue : 
                                            winner == Team.Red ? FactionType.Red : FactionType.None;
                GameModeManager.Instance.EndGame(winnerFaction);
            }
            
            // Create result
            var result = new FPSBattleResult
            {
                BattleId = battleParameters.BattleId,
                NodeId = battleParameters.NodeId,
                Winner = winner,
                Loser = winner == battleParameters.AttackingFaction ? battleParameters.DefendingFaction : battleParameters.AttackingFaction,
                Reason = reason,
                AttackerTicketsRemaining = battleParameters.GetFactionTickets(battleParameters.AttackingFaction),
                DefenderTicketsRemaining = battleParameters.GetFactionTickets(battleParameters.DefendingFaction),
                AttackerSquadResults = new Dictionary<string, int>(),
                DefenderSquadResults = new Dictionary<string, int>()
            };
            
            // Record tickets consumed per squad
            foreach (var kvp in battleParameters.AttackerSquads)
            {
                result.AttackerSquadResults[kvp.Key] = kvp.Value.TicketsConsumed;
            }
            foreach (var kvp in battleParameters.DefenderSquads)
            {
                result.DefenderSquadResults[kvp.Key] = kvp.Value.TicketsConsumed;
            }
            
            // Notify war map of result
            ReportResultToWarMap(result);
            
            OnBattleEnded?.Invoke(winner);
            RpcNotifyBattleEnded(winner, reason);
        }
        
        [Server]
        private void ReportResultToWarMap(FPSBattleResult result)
        {
            // Update squad manpower on the war map based on consumption
            var allManagers = FindObjectsByType<PlayerSquadManager>(FindObjectsSortMode.None);
            
            // Combine all squad results
            var allResults = new Dictionary<string, int>();
            foreach (var kvp in result.AttackerSquadResults)
                allResults[kvp.Key] = kvp.Value;
            foreach (var kvp in result.DefenderSquadResults)
                allResults[kvp.Key] = kvp.Value;
            
            foreach (var manager in allManagers)
            {
                for (int i = 0; i < manager.SquadCount; i++)
                {
                    var squad = manager.GetSquad(i);
                    if (squad != null && allResults.TryGetValue(squad.SquadId, out int consumed))
                    {
                        // The tickets were already consumed during battle
                        // We need to sync this back to the war map squad
                        manager.ServerConsumeManpower(i, consumed);
                    }
                }
            }
            
            // Notify CaptureController of battle result
            if (CaptureController.Instance != null)
            {
                CaptureController.Instance.OnBattleEnded(result.NodeId, result.Winner);
            }
            
            Debug.Log($"[BattleManager] Battle result reported to war map. Winner: {result.Winner}");
        }
        
        #endregion
        
        #region Player Management
        
        /// <summary>
        /// Register a player as participating in this battle.
        /// </summary>
        [Server]
        public void RegisterPlayer(NetworkPlayer player)
        {
            if (!playersInBattle.Contains(player.netId))
            {
                playersInBattle.Add(player.netId);
                Debug.Log($"[BattleManager] Player registered: {player.PlayerName}");
            }
        }
        
        /// <summary>
        /// Unregister a player from the battle.
        /// </summary>
        [Server]
        public void UnregisterPlayer(NetworkPlayer player)
        {
            playersInBattle.Remove(player.netId);
            playerSquadAssignments.Remove(player.netId);
            Debug.Log($"[BattleManager] Player unregistered: {player.PlayerName}");
        }
        
        /// <summary>
        /// Get the squad a player is spawning from.
        /// </summary>
        public string GetPlayerSquad(uint playerNetId)
        {
            return playerSquadAssignments.TryGetValue(playerNetId, out string squadId) ? squadId : null;
        }
        
        #endregion
        
        #region RPCs
        
        [ClientRpc]
        private void RpcNotifyBattleStarted()
        {
            Debug.Log("[BattleManager] Battle started notification received");
        }
        
        [ClientRpc]
        private void RpcNotifyTicketsChanged(Team faction, int newCount)
        {
            OnTicketsChanged?.Invoke(faction, newCount);
        }
        
        [ClientRpc]
        private void RpcNotifyReinforcements(string squadId, Team faction, int manpower)
        {
            Debug.Log($"[BattleManager] Reinforcements arrived: {faction} +{manpower} tickets");
            OnSquadReinforced?.Invoke(squadId);
        }
        
        [ClientRpc]
        private void RpcNotifySquadRetreated(string squadId)
        {
            OnSquadRetreated?.Invoke(squadId);
        }
        
        [ClientRpc]
        private void RpcNotifyBattleEnded(Team winner, BattleEndReason reason)
        {
            Debug.Log($"[BattleManager] Battle ended - Winner: {winner}, Reason: {reason}");
            OnBattleEnded?.Invoke(winner);
        }
        
        #endregion
        
        #region Hooks
        
        private void OnBattleStateChanged(BattleState oldState, BattleState newState)
        {
            Debug.Log($"[BattleManager] State changed: {oldState} -> {newState}");
            OnBattleStateChanged_Event?.Invoke(newState);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Possible states for a battle.
    /// </summary>
    public enum BattleState
    {
        Inactive,
        WaitingForPlayers,
        Lobby,
        InProgress,
        Ended
    }
    
    /// <summary>
    /// Reasons a battle can end.
    /// </summary>
    public enum BattleEndReason
    {
        TicketsExhausted,
        ObjectiveCompleted,
        TimeLimit,
        MutualExhaustion,
        Surrender
    }
    
    /// <summary>
    /// Result of a completed FPS battle.
    /// </summary>
    [Serializable]
    public class FPSBattleResult
    {
        public string BattleId;
        public int NodeId;
        public Team Winner;
        public Team Loser;
        public BattleEndReason Reason;
        public int AttackerTicketsRemaining;
        public int DefenderTicketsRemaining;
        public Dictionary<string, int> AttackerSquadResults; // SquadId -> TicketsConsumed
        public Dictionary<string, int> DefenderSquadResults;
    }
}
