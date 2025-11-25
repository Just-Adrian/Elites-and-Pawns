using UnityEngine;
using Mirror;

/// <summary>
/// Simple team debug display - attach to NetworkManager
/// </summary>
public class SimpleTeamDebug : MonoBehaviour
{
    private ElitesAndPawns.Core.SimpleTeamManager teamManager;
    private bool showDebug = false;
    
    void Start()
    {
        teamManager = ElitesAndPawns.Core.SimpleTeamManager.Instance;
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            showDebug = !showDebug;
            if (showDebug && teamManager != null)
            {
                teamManager.DebugPrintTeams();
            }
        }
        
        // Quick score testing (server only)
        if (NetworkServer.active && teamManager != null)
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                teamManager.AddScore(ElitesAndPawns.Core.FactionType.Blue, 10);
                Debug.Log("[TeamDebug] Blue +10");
            }
            if (Input.GetKeyDown(KeyCode.F3))
            {
                teamManager.AddScore(ElitesAndPawns.Core.FactionType.Red, 10);
                Debug.Log("[TeamDebug] Red +10");
            }
        }
    }
    
    void OnGUI()
    {
        if (!showDebug || teamManager == null) return;
        
        GUI.Box(new Rect(Screen.width - 310, 10, 300, 150), "TEAM STATUS");
        GUI.Label(new Rect(Screen.width - 300, 40, 280, 30), 
            $"BLUE: {teamManager.BluePlayerCount} players | Score: {teamManager.BlueScore}");
        GUI.Label(new Rect(Screen.width - 300, 70, 280, 30), 
            $"RED: {teamManager.RedPlayerCount} players | Score: {teamManager.RedScore}");
        GUI.Label(new Rect(Screen.width - 300, 110, 280, 40), 
            "F1: Toggle | F2/F3: Add Score");
    }
}
