using System;
using UnityEngine;
using Mirror;
using ElitesAndPawns.Core;
using ElitesAndPawns.Networking;

namespace ElitesAndPawns.WarMap
{
    /// <summary>
    /// UI overlay for battles, shown on the war map.
    /// Displays active battles and allows players to join.
    /// </summary>
    public class BattleUI : MonoBehaviour
    {
        #region Configuration
        
        [Header("UI Settings")]
        [SerializeField] private int panelWidth = 300;
        [SerializeField] private int panelHeight = 400;
        
        #endregion
        
        #region State
        
        private bool showBattleList = true;
        private Vector2 scrollPosition;
        
        // GUI Styles
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private GUIStyle buttonStyle;
        private GUIStyle labelStyle;
        private bool stylesInitialized;
        
        #endregion
        
        #region Unity Lifecycle
        
        void OnGUI()
        {
            InitializeStyles();
            
            // Draw battle panel on the left side (below node info if present)
            DrawBattlePanel();
            
            // Draw lobby UI if in lobby
            DrawLobbyUI();
        }
        
        void InitializeStyles()
        {
            if (stylesInitialized) return;
            
            headerStyle = new GUIStyle(GUI.skin.box);
            headerStyle.fontSize = 14;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = Color.white;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.textColor = Color.white;
            
            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.fontSize = 12;
            
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = Color.white;
            labelStyle.fontSize = 12;
            
            stylesInitialized = true;
        }
        
        #endregion
        
        #region Battle Panel
        
        void DrawBattlePanel()
        {
            if (BattleSceneBridge.Instance == null) return;
            if (BattleSceneBridge.Instance.ActiveBattleCount == 0) return;
            
            // Position on lower left
            Rect panelRect = new Rect(10, Screen.height - panelHeight - 10, panelWidth, panelHeight);
            
            GUILayout.BeginArea(panelRect);
            GUILayout.BeginVertical(boxStyle);
            
            GUILayout.Box("⚔ ACTIVE BATTLES ⚔", headerStyle);
            
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            
            foreach (var battle in BattleSceneBridge.Instance.GetAllActiveBattles())
            {
                DrawBattleEntry(battle);
            }
            
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        void DrawBattleEntry(ActiveBattle battle)
        {
            GUILayout.BeginVertical(boxStyle);
            
            // Header
            string attackerIcon = GetFactionIcon(battle.Parameters.AttackingFaction);
            string defenderIcon = GetFactionIcon(battle.Parameters.DefendingFaction);
            
            GUILayout.Label($"{attackerIcon} vs {defenderIcon} @ {battle.Parameters.NodeName}", headerStyle);
            
            // Ticket counts
            int attackerTickets = battle.BattleManager?.AttackerTickets ?? battle.Parameters.AttackerSpawnTickets;
            int defenderTickets = battle.BattleManager?.DefenderTickets ?? battle.Parameters.DefenderSpawnTickets;
            
            GUILayout.Label($"Attackers: {attackerTickets} tickets", labelStyle);
            GUILayout.Label($"Defenders: {defenderTickets} tickets", labelStyle);
            
            // State
            if (battle.BattleLobby != null)
            {
                switch (battle.BattleLobby.State)
                {
                    case LobbyState.WaitingForPlayers:
                        GUILayout.Label($"Status: Waiting for players ({battle.BattleLobby.TotalPlayers})", labelStyle);
                        break;
                        
                    case LobbyState.Countdown:
                        GUILayout.Label($"Status: Starting in {battle.BattleLobby.CountdownRemaining:F0}s", labelStyle);
                        break;
                        
                    case LobbyState.BattleStarting:
                        GUILayout.Label("Status: Battle starting!", labelStyle);
                        break;
                }
            }
            else if (battle.BattleManager != null)
            {
                GUILayout.Label($"Status: {battle.BattleManager.State}", labelStyle);
            }
            
            // Duration
            GUILayout.Label($"Duration: {FormatTime(battle.Duration)}", labelStyle);
            
            // Join buttons
            GUILayout.BeginHorizontal();
            
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button($"Join {battle.Parameters.AttackingFaction}", buttonStyle))
            {
                JoinBattle(battle.NodeId, battle.Parameters.AttackingFaction);
            }
            
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button($"Join {battle.Parameters.DefendingFaction}", buttonStyle))
            {
                JoinBattle(battle.NodeId, battle.Parameters.DefendingFaction);
            }
            
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            
            GUILayout.EndVertical();
            GUILayout.Space(5);
        }
        
        #endregion
        
        #region Lobby UI
        
        void DrawLobbyUI()
        {
            if (BattleLobby.Instance == null) return;
            if (BattleLobby.Instance.State == LobbyState.Inactive) return;
            
            // Find if local player is in lobby
            var localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
            if (localPlayer == null) return;
            
            if (!BattleLobby.Instance.IsPlayerInLobby(localPlayer.netId)) return;
            
            // Draw centered lobby panel
            int lobbyWidth = 400;
            int lobbyHeight = 300;
            Rect lobbyRect = new Rect(
                (Screen.width - lobbyWidth) / 2,
                (Screen.height - lobbyHeight) / 2,
                lobbyWidth, lobbyHeight
            );
            
            GUILayout.BeginArea(lobbyRect);
            GUILayout.BeginVertical(boxStyle);
            
            var parameters = BattleLobby.Instance.Parameters;
            
            // Header
            GUILayout.Box($"⚔ BATTLE FOR {parameters?.NodeName ?? "Unknown"} ⚔", headerStyle);
            
            // Factions
            GUILayout.BeginHorizontal();
            
            // Attackers
            GUILayout.BeginVertical(boxStyle);
            GUILayout.Label($"{GetFactionIcon(parameters?.AttackingFaction ?? Team.None)} ATTACKERS", labelStyle);
            GUILayout.Label($"Players: {BattleLobby.Instance.AttackerCount}", labelStyle);
            GUILayout.Label($"Ready: {BattleLobby.Instance.AttackerReady}", labelStyle);
            GUILayout.Label($"Tickets: {parameters?.AttackerSpawnTickets ?? 0}", labelStyle);
            GUILayout.EndVertical();
            
            GUILayout.Label("VS", headerStyle);
            
            // Defenders
            GUILayout.BeginVertical(boxStyle);
            GUILayout.Label($"{GetFactionIcon(parameters?.DefendingFaction ?? Team.None)} DEFENDERS", labelStyle);
            GUILayout.Label($"Players: {BattleLobby.Instance.DefenderCount}", labelStyle);
            GUILayout.Label($"Ready: {BattleLobby.Instance.DefenderReady}", labelStyle);
            GUILayout.Label($"Tickets: {parameters?.DefenderSpawnTickets ?? 0}", labelStyle);
            GUILayout.EndVertical();
            
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // Status
            switch (BattleLobby.Instance.State)
            {
                case LobbyState.WaitingForPlayers:
                    GUILayout.Label("Waiting for more players...", labelStyle);
                    break;
                    
                case LobbyState.Countdown:
                    GUILayout.Box($"Battle starts in: {BattleLobby.Instance.CountdownRemaining:F0}s", headerStyle);
                    break;
                    
                case LobbyState.BattleStarting:
                    GUILayout.Box("BATTLE STARTING!", headerStyle);
                    break;
            }
            
            GUILayout.Space(10);
            
            // Ready button
            bool isReady = BattleLobby.Instance.IsPlayerReady(localPlayer.netId);
            
            GUI.backgroundColor = isReady ? Color.green : Color.gray;
            string readyText = isReady ? "✓ READY" : "Click to Ready Up";
            
            if (GUILayout.Button(readyText, buttonStyle, GUILayout.Height(40)))
            {
                BattleLobby.Instance.CmdSetReady(localPlayer.netId, !isReady);
            }
            GUI.backgroundColor = Color.white;
            
            // Leave button
            if (GUILayout.Button("Leave Lobby", buttonStyle))
            {
                BattleLobby.Instance.CmdLeaveLobby(localPlayer.netId);
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        #endregion
        
        #region Actions
        
        void JoinBattle(int nodeId, Team faction)
        {
            // Launch FPS as a separate process
            if (FPSLauncher.Instance != null)
            {
                string playerName = "Soldier"; // Could get from local player or UI
                FPSLauncher.Instance.LaunchFPS(nodeId, faction, playerName);
                Debug.Log($"[BattleUI] Launching FPS for battle at node {nodeId} as {faction}");
            }
            else
            {
                Debug.LogError("[BattleUI] FPSLauncher not found! Add it to the scene.");
            }
        }
        
        #endregion
        
        #region Helpers
        
        string GetFactionIcon(Team faction)
        {
            return faction switch
            {
                Team.Blue => "🔵",
                Team.Red => "🔴",
                Team.Green => "🟢",
                _ => "⚪"
            };
        }
        
        string FormatTime(float seconds)
        {
            int mins = Mathf.FloorToInt(seconds / 60);
            int secs = Mathf.FloorToInt(seconds % 60);
            return $"{mins}:{secs:D2}";
        }
        
        #endregion
    }
}
