using Mirror;
using UnityEngine;

namespace ElitesAndPawns.Networking
{
    /// <summary>
    /// Custom Network Manager for Elites and Pawns True.
    /// Handles player connections, spawning, and faction assignment.
    /// </summary>
    public class ElitesNetworkManager : NetworkManager
    {
        [Header("Elites Configuration")]
        [SerializeField] private int maxPlayersPerTeam = 8; // 8v8 = 16 total
        [SerializeField] private bool autoAssignFaction = true;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Track connected players per faction
        private int bluePlayerCount = 0;
        private int redPlayerCount = 0;
        private int greenPlayerCount = 0;

        public override void OnStartServer()
        {
            base.OnStartServer();
            
            if (debugMode)
            {
                Debug.Log("[ElitesNetworkManager] Server started");
            }
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            // Spawn player at appropriate position
            Transform startPos = GetStartPosition();
            GameObject player = startPos != null
                ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
                : Instantiate(playerPrefab);

            // Add player to connection
            NetworkServer.AddPlayerForConnection(conn, player);

            // Assign faction (MVP: Blue only, but ready for expansion)
            if (autoAssignFaction && player.TryGetComponent<NetworkPlayer>(out var networkPlayer))
            {
                Core.FactionType faction = GetBalancedFaction();
                networkPlayer.SetFaction(faction);

                if (debugMode)
                {
                    Debug.Log($"[ElitesNetworkManager] Player connected. Assigned to {faction} faction. " +
                              $"Total players - Blue: {bluePlayerCount}, Red: {redPlayerCount}, Green: {greenPlayerCount}");
                }
            }
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // Track faction before disconnect
            if (conn.identity != null && conn.identity.TryGetComponent<NetworkPlayer>(out var networkPlayer))
            {
                Core.FactionType faction = networkPlayer.Faction;
                DecrementFactionCount(faction);

                if (debugMode)
                {
                    Debug.Log($"[ElitesNetworkManager] Player from {faction} faction disconnected. " +
                              $"Remaining - Blue: {bluePlayerCount}, Red: {redPlayerCount}, Green: {greenPlayerCount}");
                }
            }

            base.OnServerDisconnect(conn);
        }

        /// <summary>
        /// Get the least populated faction for team balance
        /// MVP: Returns Blue only
        /// </summary>
        private Core.FactionType GetBalancedFaction()
        {
            // MVP: Only Blue faction available
            bluePlayerCount++;
            return Core.FactionType.Blue;

            // Post-MVP: Balance across all factions
            /*
            if (bluePlayerCount <= redPlayerCount && bluePlayerCount <= greenPlayerCount)
            {
                bluePlayerCount++;
                return Core.FactionType.Blue;
            }
            else if (redPlayerCount <= greenPlayerCount)
            {
                redPlayerCount++;
                return Core.FactionType.Red;
            }
            else
            {
                greenPlayerCount++;
                return Core.FactionType.Green;
            }
            */
        }

        /// <summary>
        /// Decrement faction player count when player leaves
        /// </summary>
        private void DecrementFactionCount(Core.FactionType faction)
        {
            switch (faction)
            {
                case Core.FactionType.Blue:
                    bluePlayerCount = Mathf.Max(0, bluePlayerCount - 1);
                    break;
                case Core.FactionType.Red:
                    redPlayerCount = Mathf.Max(0, redPlayerCount - 1);
                    break;
                case Core.FactionType.Green:
                    greenPlayerCount = Mathf.Max(0, greenPlayerCount - 1);
                    break;
            }
        }

        /// <summary>
        /// Check if server is full
        /// </summary>
        public bool IsServerFull()
        {
            return numPlayers >= maxPlayersPerTeam * 2; // 8v8 = 16 total
        }

        /// <summary>
        /// Get current player counts per faction
        /// </summary>
        public (int blue, int red, int green) GetFactionCounts()
        {
            return (bluePlayerCount, redPlayerCount, greenPlayerCount);
        }
    }
}
