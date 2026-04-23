using System;
using System.Collections.Generic;
using UnityEngine;
using ElitesAndPawns.Core;

namespace ElitesAndPawns.WarMap
{
    /// <summary>
    /// Contains all parameters needed to initialize an FPS battle from the war map.
    /// This is passed when loading a battle scene.
    /// </summary>
    [Serializable]
    public class BattleParameters
    {
        #region Core Battle Info
        
        /// <summary>
        /// Unique identifier for this battle instance.
        /// </summary>
        public string BattleId;
        
        /// <summary>
        /// The war map node being fought over.
        /// </summary>
        public int NodeId;
        
        /// <summary>
        /// Name of the node (for UI display).
        /// </summary>
        public string NodeName;
        
        /// <summary>
        /// The faction attacking the node.
        /// </summary>
        public FactionType AttackingFaction;
        
        /// <summary>
        /// The faction defending the node.
        /// </summary>
        public FactionType DefendingFaction;
        
        #endregion
        
        #region Spawn Tickets
        
        /// <summary>
        /// Initial spawn tickets for attackers (sum of attacker squad manpower).
        /// </summary>
        public int AttackerSpawnTickets;
        
        /// <summary>
        /// Initial spawn tickets for defenders (sum of defender squad manpower).
        /// </summary>
        public int DefenderSpawnTickets;
        
        /// <summary>
        /// Squad data for attackers (for tracking individual squad ticket consumption).
        /// Key: SquadId, Value: Manpower available.
        /// </summary>
        public Dictionary<string, SquadBattleData> AttackerSquads = new Dictionary<string, SquadBattleData>();
        
        /// <summary>
        /// Squad data for defenders.
        /// </summary>
        public Dictionary<string, SquadBattleData> DefenderSquads = new Dictionary<string, SquadBattleData>();
        
        #endregion
        
        #region Battle Settings
        
        /// <summary>
        /// Type of battle (affects victory conditions).
        /// </summary>
        public BattleType Type;
        
        /// <summary>
        /// Time limit for the battle in seconds (0 = no limit).
        /// </summary>
        public float TimeLimit;
        
        /// <summary>
        /// Score required to win (for KOTH mode).
        /// </summary>
        public int ScoreToWin;
        
        /// <summary>
        /// Minimum players required to start the battle.
        /// </summary>
        public int MinPlayersToStart;
        
        /// <summary>
        /// Lobby countdown time in seconds.
        /// </summary>
        public float LobbyCountdown;
        
        #endregion
        
        #region Scene Info
        
        /// <summary>
        /// Name of the FPS battle scene to load.
        /// </summary>
        public string BattleSceneName;
        
        /// <summary>
        /// Time when the battle was created.
        /// </summary>
        public float CreationTime;
        
        #endregion
        
        #region Constructors
        
        public BattleParameters()
        {
            BattleId = Guid.NewGuid().ToString().Substring(0, 8);
            CreationTime = Time.time;
            Type = BattleType.KingOfTheHill;
            TimeLimit = 600f; // 10 minutes
            ScoreToWin = 300;
            MinPlayersToStart = 1; // For testing
            LobbyCountdown = 30f;
            BattleSceneName = "NetworkTest";
        }
        
        /// <summary>
        /// Create battle parameters from a contested node.
        /// </summary>
        public static BattleParameters FromContestedNode(int nodeId, FactionType attacker, FactionType defender)
        {
            var node = WarMapManager.Instance?.GetNodeByID(nodeId);
            
            var parameters = new BattleParameters
            {
                NodeId = nodeId,
                NodeName = node?.NodeName ?? $"Node {nodeId}",
                AttackingFaction = attacker,
                DefendingFaction = defender,
            };
            
            // Gather squad data from NodeOccupancy
            if (NodeOccupancy.Instance != null)
            {
                var squadsAtNode = NodeOccupancy.Instance.GetSquadsAtNode(nodeId);
                
                foreach (var squadPresence in squadsAtNode)
                {
                    var squadData = new SquadBattleData
                    {
                        SquadId = squadPresence.SquadId,
                        OwnerNetId = squadPresence.OwnerNetId,
                        OwnerDisplayName = squadPresence.OwnerDisplayName,
                        Faction = squadPresence.Faction,
                        InitialManpower = squadPresence.Manpower,
                        CurrentManpower = squadPresence.Manpower
                    };
                    
                    if (squadPresence.Faction == attacker)
                    {
                        parameters.AttackerSquads[squadPresence.SquadId] = squadData;
                        parameters.AttackerSpawnTickets += squadPresence.Manpower;
                    }
                    else if (squadPresence.Faction == defender)
                    {
                        parameters.DefenderSquads[squadPresence.SquadId] = squadData;
                        parameters.DefenderSpawnTickets += squadPresence.Manpower;
                    }
                }
            }
            
            Debug.Log($"[BattleParameters] Created battle for node {nodeId}: " +
                      $"{attacker} ({parameters.AttackerSpawnTickets} tickets) vs " +
                      $"{defender} ({parameters.DefenderSpawnTickets} tickets)");
            
            return parameters;
        }
        
        #endregion
        
        #region Methods
        
        /// <summary>
        /// Get total remaining spawn tickets for a faction.
        /// </summary>
        public int GetFactionTickets(FactionType faction)
        {
            int total = 0;
            var squads = faction == AttackingFaction ? AttackerSquads : DefenderSquads;
            
            foreach (var squad in squads.Values)
            {
                total += squad.CurrentManpower;
            }
            
            return total;
        }
        
        /// <summary>
        /// Consume a spawn ticket from a specific squad.
        /// Returns true if successful.
        /// </summary>
        public bool ConsumeTicketFromSquad(string squadId)
        {
            if (AttackerSquads.TryGetValue(squadId, out var attackerSquad))
            {
                if (attackerSquad.CurrentManpower > 0)
                {
                    attackerSquad.CurrentManpower--;
                    attackerSquad.TicketsConsumed++;
                    return true;
                }
            }
            
            if (DefenderSquads.TryGetValue(squadId, out var defenderSquad))
            {
                if (defenderSquad.CurrentManpower > 0)
                {
                    defenderSquad.CurrentManpower--;
                    defenderSquad.TicketsConsumed++;
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Consume a spawn ticket from any squad of a faction.
        /// Prefers squads owned by the specified player.
        /// Returns the squadId that was used, or null if no tickets available.
        /// </summary>
        public string ConsumeTicketFromFaction(FactionType faction, uint preferredOwnerNetId = 0)
        {
            var squads = faction == AttackingFaction ? AttackerSquads : DefenderSquads;
            
            // First, try to use a squad owned by the preferred player
            if (preferredOwnerNetId != 0)
            {
                foreach (var kvp in squads)
                {
                    if (kvp.Value.OwnerNetId == preferredOwnerNetId && kvp.Value.CurrentManpower > 0)
                    {
                        kvp.Value.CurrentManpower--;
                        kvp.Value.TicketsConsumed++;
                        return kvp.Key;
                    }
                }
            }
            
            // Fall back to any squad with available tickets
            foreach (var kvp in squads)
            {
                if (kvp.Value.CurrentManpower > 0)
                {
                    kvp.Value.CurrentManpower--;
                    kvp.Value.TicketsConsumed++;
                    return kvp.Key;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Add a new squad to the battle (reinforcements).
        /// </summary>
        public void AddSquad(SquadBattleData squadData)
        {
            if (squadData.Faction == AttackingFaction)
            {
                AttackerSquads[squadData.SquadId] = squadData;
                AttackerSpawnTickets += squadData.CurrentManpower;
                Debug.Log($"[BattleParameters] Reinforcements arrived: {squadData.SquadId} (+{squadData.CurrentManpower} attacker tickets)");
            }
            else if (squadData.Faction == DefendingFaction)
            {
                DefenderSquads[squadData.SquadId] = squadData;
                DefenderSpawnTickets += squadData.CurrentManpower;
                Debug.Log($"[BattleParameters] Reinforcements arrived: {squadData.SquadId} (+{squadData.CurrentManpower} defender tickets)");
            }
        }
        
        /// <summary>
        /// Remove a squad from the battle (retreat).
        /// </summary>
        public void RemoveSquad(string squadId)
        {
            if (AttackerSquads.TryGetValue(squadId, out var attackerSquad))
            {
                AttackerSquads.Remove(squadId);
                Debug.Log($"[BattleParameters] Squad retreated: {squadId} ({attackerSquad.CurrentManpower} attacker tickets lost)");
            }
            else if (DefenderSquads.TryGetValue(squadId, out var defenderSquad))
            {
                DefenderSquads.Remove(squadId);
                Debug.Log($"[BattleParameters] Squad retreated: {squadId} ({defenderSquad.CurrentManpower} defender tickets lost)");
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Data for a single squad participating in a battle.
    /// </summary>
    [Serializable]
    public class SquadBattleData
    {
        public string SquadId;
        public uint OwnerNetId;
        public string OwnerDisplayName;
        public FactionType Faction;
        public int InitialManpower;
        public int CurrentManpower;
        public int TicketsConsumed;
    }
    
    /// <summary>
    /// Types of battles that can occur.
    /// </summary>
    public enum BattleType
    {
        /// <summary>Standard King of the Hill - hold point to score.</summary>
        KingOfTheHill,
        
        /// <summary>Attackers must capture, defenders must hold until time runs out.</summary>
        AttackDefense,
        
        /// <summary>Both sides fight to eliminate all enemy spawn tickets.</summary>
        Elimination
    }
}
