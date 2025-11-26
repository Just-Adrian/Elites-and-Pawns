using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ElitesAndPawns.Core
{
    /// <summary>
    /// Simple team manager that doesn't require Mirror networking.
    /// Manages team assignment and tracking on the server.
    /// </summary>
    public class SimpleTeamManager : MonoBehaviour
    {
        private static SimpleTeamManager _instance;
        public static SimpleTeamManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<SimpleTeamManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("SimpleTeamManager");
                        _instance = go.AddComponent<SimpleTeamManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        [Header("Team Configuration")]
        [SerializeField] private int maxPlayersPerTeam = 8;
        [SerializeField] private bool autoBalance = true;

        [Header("Team Scores")]
        private int blueScore = 0;
        private int redScore = 0;

        [Header("Team Tracking")]
        private List<uint> bluePlayers = new List<uint>();
        private List<uint> redPlayers = new List<uint>();

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Events
        public delegate void TeamScoreChanged(FactionType team, int newScore);
        public static event TeamScoreChanged OnTeamScoreChanged;

        public delegate void PlayerJoinedTeam(uint netId, FactionType team);
        public static event PlayerJoinedTeam OnPlayerJoinedTeam;

        public delegate void PlayerLeftTeam(uint netId, FactionType team);
        public static event PlayerLeftTeam OnPlayerLeftTeam;

        // Properties
        public int BlueScore => blueScore;
        public int RedScore => redScore;
        public int BluePlayerCount => bluePlayers.Count;
        public int RedPlayerCount => redPlayers.Count;
        public List<uint> BluePlayers => new List<uint>(bluePlayers);
        public List<uint> RedPlayers => new List<uint>(redPlayers);

        private void Awake()
        {
            // Singleton pattern
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (debugMode)
            {
                Debug.Log("[SimpleTeamManager] Initialized");
            }
        }

        /// <summary>
        /// Get the team that needs more players for balance
        /// </summary>
        public FactionType GetBalancedTeam()
        {
            if (bluePlayers.Count <= redPlayers.Count)
            {
                return FactionType.Blue;
            }
            else
            {
                return FactionType.Red;
            }
        }

        /// <summary>
        /// Add a player to a team
        /// </summary>
        public void AddPlayerToTeam(uint playerNetId, FactionType team)
        {
            // Remove from any existing team first
            RemovePlayerFromTeams(playerNetId);

            // Add to new team
            switch (team)
            {
                case FactionType.Blue:
                    bluePlayers.Add(playerNetId);
                    break;
                case FactionType.Red:
                    redPlayers.Add(playerNetId);
                    break;
                default:
                    Debug.LogWarning($"[SimpleTeamManager] Attempted to add player to invalid team: {team}");
                    return;
            }

            if (debugMode)
            {
                Debug.Log($"[SimpleTeamManager] Player {playerNetId} joined {team} team. " +
                         $"Blue: {bluePlayers.Count}, Red: {redPlayers.Count}");
            }

            // Fire event
            OnPlayerJoinedTeam?.Invoke(playerNetId, team);
        }

        /// <summary>
        /// Remove a player from all teams
        /// </summary>
        public void RemovePlayerFromTeams(uint playerNetId)
        {
            FactionType? previousTeam = null;

            if (bluePlayers.Contains(playerNetId))
            {
                bluePlayers.Remove(playerNetId);
                previousTeam = FactionType.Blue;
            }

            if (redPlayers.Contains(playerNetId))
            {
                redPlayers.Remove(playerNetId);
                previousTeam = FactionType.Red;
            }

            if (previousTeam.HasValue)
            {
                if (debugMode)
                {
                    Debug.Log($"[SimpleTeamManager] Player {playerNetId} removed from {previousTeam} team");
                }
                OnPlayerLeftTeam?.Invoke(playerNetId, previousTeam.Value);
            }
        }

        /// <summary>
        /// Get a player's current team
        /// </summary>
        public FactionType? GetPlayerTeam(uint playerNetId)
        {
            if (bluePlayers.Contains(playerNetId))
                return FactionType.Blue;
            if (redPlayers.Contains(playerNetId))
                return FactionType.Red;
            return null;
        }

        /// <summary>
        /// Add points to a team's score
        /// </summary>
        public void AddScore(FactionType team, int points)
        {
            switch (team)
            {
                case FactionType.Blue:
                    blueScore += points;
                    OnTeamScoreChanged?.Invoke(FactionType.Blue, blueScore);
                    break;
                case FactionType.Red:
                    redScore += points;
                    OnTeamScoreChanged?.Invoke(FactionType.Red, redScore);
                    break;
            }

            if (debugMode)
            {
                Debug.Log($"[SimpleTeamManager] {team} team scored {points} points. " +
                         $"Score - Blue: {blueScore}, Red: {redScore}");
            }
        }

        /// <summary>
        /// Reset scores
        /// </summary>
        public void ResetScores()
        {
            blueScore = 0;
            redScore = 0;

            if (debugMode)
            {
                Debug.Log("[SimpleTeamManager] Scores reset");
            }
        }

        /// <summary>
        /// Get all players from both teams
        /// </summary>
        public List<uint> GetAllPlayers()
        {
            List<uint> allPlayers = new List<uint>();
            allPlayers.AddRange(bluePlayers);
            allPlayers.AddRange(redPlayers);
            return allPlayers;
        }

        /// <summary>
        /// Check if a player is on a specific team
        /// </summary>
        public bool IsPlayerOnTeam(uint playerNetId, FactionType team)
        {
            return team switch
            {
                FactionType.Blue => bluePlayers.Contains(playerNetId),
                FactionType.Red => redPlayers.Contains(playerNetId),
                _ => false
            };
        }

        /// <summary>
        /// Get the opposing team
        /// </summary>
        public FactionType GetOpposingTeam(FactionType team)
        {
            return team switch
            {
                FactionType.Blue => FactionType.Red,
                FactionType.Red => FactionType.Blue,
                _ => FactionType.None
            };
        }

        /// <summary>
        /// Check team balance
        /// </summary>
        public void CheckTeamBalance()
        {
            int diff = Mathf.Abs(bluePlayers.Count - redPlayers.Count);
            
            if (diff > 1)
            {
                if (debugMode)
                {
                    Debug.LogWarning($"[SimpleTeamManager] Teams unbalanced! Blue: {bluePlayers.Count}, Red: {redPlayers.Count}");
                }
            }
        }

        // Debug methods
        public void DebugPrintTeams()
        {
            Debug.Log($"[SimpleTeamManager] === TEAM STATUS ===");
            Debug.Log($"Blue Team ({bluePlayers.Count} players): {string.Join(", ", bluePlayers)}");
            Debug.Log($"Red Team ({redPlayers.Count} players): {string.Join(", ", redPlayers)}");
            Debug.Log($"Scores - Blue: {blueScore}, Red: {redScore}");
        }
    }
}
