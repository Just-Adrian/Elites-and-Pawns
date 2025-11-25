using UnityEngine;
using Mirror;
using ElitesAndPawns.Core;
using ElitesAndPawns.Networking;

namespace ElitesAndPawns.Debug
{
    /// <summary>
    /// Debug helper for testing the team system.
    /// Attach to NetworkManager GameObject for testing.
    /// </summary>
    public class TeamSystemDebugger : MonoBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private bool showDebugGUI = true;
        [SerializeField] private KeyCode debugMenuKey = KeyCode.F1;
        
        private SimpleTeamManager teamManager;
        private ElitesNetworkManager networkManager;
        private bool showDebugMenu = false;
        
        private void Start()
        {
            teamManager = SimpleTeamManager.Instance;
            networkManager = GetComponent<ElitesNetworkManager>();
        }
        
        private void Update()
        {
            if (Input.GetKeyDown(debugMenuKey))
            {
                showDebugMenu = !showDebugMenu;
                
                if (showDebugMenu && teamManager != null)
                {
                    teamManager.DebugPrintTeams();
                }
            }
            
            // Debug shortcuts (only on server)
            if (NetworkServer.active)
            {
                // F2 - Add score to Blue team
                if (Input.GetKeyDown(KeyCode.F2))
                {
                    teamManager?.AddScore(FactionType.Blue, 10);
                    Debug.Log("[TeamDebug] Added 10 points to Blue team");
                }
                
                // F3 - Add score to Red team
                if (Input.GetKeyDown(KeyCode.F3))
                {
                    teamManager?.AddScore(FactionType.Red, 10);
                    Debug.Log("[TeamDebug] Added 10 points to Red team");
                }
                
                // F4 - Reset scores
                if (Input.GetKeyDown(KeyCode.F4))
                {
                    teamManager?.ResetScores();
                    Debug.Log("[TeamDebug] Scores reset");
                }
            }
        }
        
        private void OnGUI()
        {
            if (!showDebugGUI || !showDebugMenu) return;
            
            // Create debug window
            float windowWidth = 400;
            float windowHeight = 300;
            float x = Screen.width - windowWidth - 10;
            float y = 10;
            
            GUI.Box(new Rect(x, y, windowWidth, windowHeight), "Team System Debug");
            
            float yOffset = y + 30;
            float lineHeight = 25;
            
            if (teamManager != null)
            {
                // Team counts
                GUI.Label(new Rect(x + 10, yOffset, windowWidth - 20, lineHeight), 
                    $"Blue Team: {teamManager.BluePlayerCount} players | Score: {teamManager.BlueScore}");
                yOffset += lineHeight;
                
                GUI.Label(new Rect(x + 10, yOffset, windowWidth - 20, lineHeight), 
                    $"Red Team: {teamManager.RedPlayerCount} players | Score: {teamManager.RedScore}");
                yOffset += lineHeight;
                
                // Separator
                yOffset += 10;
                GUI.Label(new Rect(x + 10, yOffset, windowWidth - 20, lineHeight), 
                    "================================");
                yOffset += lineHeight;
                
                // Player list
                var bluePlayers = teamManager.BluePlayers;
                var redPlayers = teamManager.RedPlayers;
                
                GUI.Label(new Rect(x + 10, yOffset, windowWidth - 20, lineHeight), 
                    "Blue Team Players:");
                yOffset += lineHeight;
                
                foreach (var playerNetId in bluePlayers)
                {
                    NetworkIdentity netIdentity = NetworkServer.spawned.ContainsKey(playerNetId) 
                        ? NetworkServer.spawned[playerNetId] 
                        : null;
                    
                    if (netIdentity != null)
                    {
                        var networkPlayer = netIdentity.GetComponent<NetworkPlayer>();
                        if (networkPlayer != null)
                        {
                            GUI.Label(new Rect(x + 20, yOffset, windowWidth - 30, lineHeight), 
                                $"- {networkPlayer.PlayerName} (ID: {playerNetId})");
                            yOffset += lineHeight;
                        }
                    }
                }
                
                yOffset += 10;
                GUI.Label(new Rect(x + 10, yOffset, windowWidth - 20, lineHeight), 
                    "Red Team Players:");
                yOffset += lineHeight;
                
                foreach (var playerNetId in redPlayers)
                {
                    NetworkIdentity netIdentity = NetworkServer.spawned.ContainsKey(playerNetId) 
                        ? NetworkServer.spawned[playerNetId] 
                        : null;
                    
                    if (netIdentity != null)
                    {
                        var networkPlayer = netIdentity.GetComponent<NetworkPlayer>();
                        if (networkPlayer != null)
                        {
                            GUI.Label(new Rect(x + 20, yOffset, windowWidth - 30, lineHeight), 
                                $"- {networkPlayer.PlayerName} (ID: {playerNetId})");
                            yOffset += lineHeight;
                        }
                    }
                }
                
                // Controls info
                yOffset = y + windowHeight - 80;
                GUI.Label(new Rect(x + 10, yOffset, windowWidth - 20, lineHeight), 
                    "Controls:");
                yOffset += lineHeight;
                GUI.Label(new Rect(x + 10, yOffset, windowWidth - 20, lineHeight), 
                    "F1 - Toggle this menu");
                yOffset += lineHeight;
                
                if (NetworkServer.active)
                {
                    GUI.Label(new Rect(x + 10, yOffset, windowWidth - 20, lineHeight), 
                        "F2/F3 - Add score to Blue/Red | F4 - Reset scores");
                }
            }
            else
            {
                GUI.Label(new Rect(x + 10, yOffset, windowWidth - 20, lineHeight), 
                    "TeamManager not found!");
            }
        }
    }
}
