using System;
using UnityEngine;
using Mirror;
using ElitesAndPawns.Core;

namespace ElitesAndPawns.WarMap
{
    /// <summary>
    /// Represents a single squad that a player controls on the war map.
    /// Squads contain manpower (tokens) that translate to spawn tickets in FPS battles.
    /// Each squad can be independently moved between connected nodes.
    /// </summary>
    [Serializable]
    public class Squad
    {
        #region Fields
        
        /// <summary>
        /// Unique identifier for this squad.
        /// Format: {OwnerNetId}_{SquadIndex} (e.g., "12_0" for player 12's first squad)
        /// </summary>
        public string SquadId;
        
        /// <summary>
        /// Network ID of the player who owns this squad.
        /// </summary>
        public uint OwnerNetId;
        
        /// <summary>
        /// Display name of the owning player (for UI purposes).
        /// </summary>
        public string OwnerDisplayName;
        
        /// <summary>
        /// Which faction this squad belongs to.
        /// </summary>
        public Team Faction;
        
        /// <summary>
        /// Current manpower in this squad. Each point = 1 spawn ticket.
        /// </summary>
        public int Manpower;
        
        /// <summary>
        /// Maximum manpower this squad can hold.
        /// </summary>
        public int MaxManpower;
        
        /// <summary>
        /// Node ID where this squad is currently located (-1 if in transit).
        /// </summary>
        public int CurrentNodeId;
        
        /// <summary>
        /// Node ID this squad is traveling to (-1 if stationary).
        /// </summary>
        public int DestinationNodeId;
        
        /// <summary>
        /// Current movement state of the squad.
        /// </summary>
        public SquadMovementState MovementState;
        
        /// <summary>
        /// Time when the squad started moving (for travel time calculation).
        /// </summary>
        public float MovementStartTime;
        
        /// <summary>
        /// Time when the squad will arrive at destination.
        /// </summary>
        public float MovementArrivalTime;
        
        /// <summary>
        /// Index of this squad for the owning player (0, 1, or 2).
        /// </summary>
        public int SquadIndex;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Whether this squad is currently moving between nodes.
        /// </summary>
        public bool IsMoving => MovementState == SquadMovementState.Moving;
        
        /// <summary>
        /// Whether this squad has any manpower available.
        /// </summary>
        public bool HasManpower => Manpower > 0;
        
        /// <summary>
        /// Whether this squad can accept more manpower.
        /// </summary>
        public bool CanResupply => Manpower < MaxManpower;
        
        /// <summary>
        /// How much manpower can be added before reaching max.
        /// </summary>
        public int ResupplyCapacity => MaxManpower - Manpower;
        
        /// <summary>
        /// Progress of current movement (0-1). Returns 1 if not moving.
        /// </summary>
        public float MovementProgress
        {
            get
            {
                if (!IsMoving) return 1f;
                
                float totalTime = MovementArrivalTime - MovementStartTime;
                if (totalTime <= 0) return 1f;
                
                float elapsed = Time.time - MovementStartTime;
                return Mathf.Clamp01(elapsed / totalTime);
            }
        }
        
        /// <summary>
        /// Seconds remaining until arrival. Returns 0 if not moving.
        /// </summary>
        public float TimeToArrival
        {
            get
            {
                if (!IsMoving) return 0f;
                return Mathf.Max(0f, MovementArrivalTime - Time.time);
            }
        }
        
        #endregion
        
        #region Constructors
        
        /// <summary>
        /// Create a new squad for a player.
        /// </summary>
        /// <param name="ownerNetId">Network ID of the owning player</param>
        /// <param name="ownerName">Display name of the owner</param>
        /// <param name="faction">Faction this squad belongs to</param>
        /// <param name="squadIndex">Index (0-2) for this player's squads</param>
        /// <param name="startingNodeId">Node where the squad starts</param>
        /// <param name="maxManpower">Maximum manpower capacity</param>
        public Squad(uint ownerNetId, string ownerName, Team faction, int squadIndex, 
                     int startingNodeId, int maxManpower = 8)
        {
            SquadId = $"{ownerNetId}_{squadIndex}";
            OwnerNetId = ownerNetId;
            OwnerDisplayName = ownerName;
            Faction = faction;
            SquadIndex = squadIndex;
            CurrentNodeId = startingNodeId;
            DestinationNodeId = -1;
            MaxManpower = maxManpower;
            Manpower = 0; // Starts empty, must resupply from faction pool
            MovementState = SquadMovementState.Stationary;
            MovementStartTime = 0f;
            MovementArrivalTime = 0f;
        }
        
        /// <summary>
        /// Default constructor for serialization.
        /// </summary>
        public Squad()
        {
            SquadId = "";
            OwnerNetId = 0;
            OwnerDisplayName = "";
            Faction = Team.None;
            SquadIndex = 0;
            CurrentNodeId = -1;
            DestinationNodeId = -1;
            MaxManpower = 8;
            Manpower = 0;
            MovementState = SquadMovementState.Stationary;
        }
        
        #endregion
        
        #region Methods
        
        /// <summary>
        /// Start moving this squad to a destination node.
        /// </summary>
        /// <param name="destinationNodeId">Target node ID</param>
        /// <param name="travelTime">Time in seconds to reach destination</param>
        public void StartMovement(int destinationNodeId, float travelTime)
        {
            DestinationNodeId = destinationNodeId;
            MovementState = SquadMovementState.Moving;
            MovementStartTime = Time.time;
            MovementArrivalTime = Time.time + travelTime;
            
            Debug.Log($"[Squad] {SquadId} started moving from node {CurrentNodeId} to {destinationNodeId} (ETA: {travelTime:F1}s)");
        }
        
        /// <summary>
        /// Complete the current movement and arrive at destination.
        /// </summary>
        public void CompleteMovement()
        {
            if (!IsMoving)
            {
                Debug.LogWarning($"[Squad] {SquadId} CompleteMovement called but squad is not moving");
                return;
            }
            
            int previousNode = CurrentNodeId;
            CurrentNodeId = DestinationNodeId;
            DestinationNodeId = -1;
            MovementState = SquadMovementState.Stationary;
            MovementStartTime = 0f;
            MovementArrivalTime = 0f;
            
            Debug.Log($"[Squad] {SquadId} arrived at node {CurrentNodeId} (from {previousNode})");
        }
        
        /// <summary>
        /// Cancel current movement and stay at current node.
        /// Only works if squad hasn't passed the point of no return.
        /// </summary>
        /// <returns>True if cancellation succeeded</returns>
        public bool CancelMovement()
        {
            if (!IsMoving)
                return false;
            
            // Can only cancel in first half of journey
            if (MovementProgress > 0.5f)
            {
                Debug.Log($"[Squad] {SquadId} cannot cancel movement - past point of no return");
                return false;
            }
            
            DestinationNodeId = -1;
            MovementState = SquadMovementState.Stationary;
            MovementStartTime = 0f;
            MovementArrivalTime = 0f;
            
            Debug.Log($"[Squad] {SquadId} cancelled movement, staying at node {CurrentNodeId}");
            return true;
        }
        
        /// <summary>
        /// Add manpower to this squad.
        /// </summary>
        /// <param name="amount">Amount to add</param>
        /// <returns>Actual amount added (may be less if at capacity)</returns>
        public int AddManpower(int amount)
        {
            int actualAmount = Mathf.Min(amount, ResupplyCapacity);
            Manpower += actualAmount;
            
            Debug.Log($"[Squad] {SquadId} resupplied +{actualAmount} manpower (now {Manpower}/{MaxManpower})");
            return actualAmount;
        }
        
        /// <summary>
        /// Remove manpower from this squad (e.g., when a player spawns).
        /// </summary>
        /// <param name="amount">Amount to remove</param>
        /// <returns>Actual amount removed (may be less if insufficient)</returns>
        public int RemoveManpower(int amount)
        {
            int actualAmount = Mathf.Min(amount, Manpower);
            Manpower -= actualAmount;
            
            Debug.Log($"[Squad] {SquadId} consumed -{actualAmount} manpower (now {Manpower}/{MaxManpower})");
            return actualAmount;
        }
        
        /// <summary>
        /// Check if this squad can move to a specific node.
        /// Does NOT validate connectivity - that's done by SquadMovementSystem.
        /// </summary>
        public bool CanInitiateMovement()
        {
            // Can't move if already moving
            if (IsMoving)
                return false;
            
            // Can't move if not at a valid node
            if (CurrentNodeId < 0)
                return false;
            
            return true;
        }
        
        #endregion
        
        #region Serialization Helpers
        
        /// <summary>
        /// Convert to a network-friendly format for SyncList.
        /// </summary>
        public SquadSyncData ToSyncData()
        {
            return new SquadSyncData
            {
                SquadId = this.SquadId,
                OwnerNetId = this.OwnerNetId,
                OwnerDisplayName = this.OwnerDisplayName,
                Faction = (int)this.Faction,
                Manpower = this.Manpower,
                MaxManpower = this.MaxManpower,
                CurrentNodeId = this.CurrentNodeId,
                DestinationNodeId = this.DestinationNodeId,
                MovementState = (int)this.MovementState,
                MovementStartTime = this.MovementStartTime,
                MovementArrivalTime = this.MovementArrivalTime,
                SquadIndex = this.SquadIndex
            };
        }
        
        /// <summary>
        /// Restore from network sync data.
        /// </summary>
        public static Squad FromSyncData(SquadSyncData data)
        {
            return new Squad
            {
                SquadId = data.SquadId,
                OwnerNetId = data.OwnerNetId,
                OwnerDisplayName = data.OwnerDisplayName,
                Faction = (Team)data.Faction,
                Manpower = data.Manpower,
                MaxManpower = data.MaxManpower,
                CurrentNodeId = data.CurrentNodeId,
                DestinationNodeId = data.DestinationNodeId,
                MovementState = (SquadMovementState)data.MovementState,
                MovementStartTime = data.MovementStartTime,
                MovementArrivalTime = data.MovementArrivalTime,
                SquadIndex = data.SquadIndex
            };
        }
        
        #endregion
        
        public override string ToString()
        {
            string status = IsMoving 
                ? $"Moving to {DestinationNodeId} ({MovementProgress:P0})" 
                : $"At node {CurrentNodeId}";
            return $"[Squad {SquadId}] {Faction} | {Manpower}/{MaxManpower} | {status}";
        }
    }
    
    /// <summary>
    /// Movement states for a squad.
    /// </summary>
    public enum SquadMovementState
    {
        /// <summary>Squad is stationed at a node.</summary>
        Stationary,
        
        /// <summary>Squad is traveling between nodes.</summary>
        Moving,
        
        /// <summary>Squad is engaged in a battle at current node.</summary>
        InBattle
    }
    
    /// <summary>
    /// Network-serializable version of Squad data for Mirror SyncList.
    /// </summary>
    [Serializable]
    public struct SquadSyncData
    {
        public string SquadId;
        public uint OwnerNetId;
        public string OwnerDisplayName;
        public int Faction;
        public int Manpower;
        public int MaxManpower;
        public int CurrentNodeId;
        public int DestinationNodeId;
        public int MovementState;
        public float MovementStartTime;
        public float MovementArrivalTime;
        public int SquadIndex;
    }
}
