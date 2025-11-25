using UnityEngine;
using Mirror;
using System.Collections;

namespace ElitesAndPawns.GameModes
{
    /// <summary>
    /// Manages King of the Hill gamemode logic, scoring, and win conditions.
    /// Scores are synced via ScoreNetworkSync component.
    /// </summary>
    public class GameModeManager : MonoBehaviour
    {
        private static GameModeManager _instance;
        public static GameModeManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<GameModeManager>();
                }
                return _instance;
            }
        }

        [Header("Game Settings")]
        [SerializeField] private int scoreToWin = 300;
        [SerializeField] private float matchTimeLimit = 600f; // 10 minutes
        [SerializeField] private int pointsPerSecond = 1; // Points for holding the control point
        [SerializeField] private int captureBonus = 10; // Bonus points for capturing
        
        [Header("References")]
        [SerializeField] private ControlPoint controlPoint;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = true;
        
        // State
        private bool gameActive = false;
        private float matchTimer = 0f;
        private Core.FactionType winningTeam = Core.FactionType.None;
        private bool hasStartedGame = false;
        
        // Scoring
        private float scoreTimer = 0f;
        private ScoreNetworkSync scoreSync;
        
        // Events
        public delegate void GameStarted();
        public static event GameStarted OnGameStarted;
        
        public delegate void GameEnded(Core.FactionType winner);
        public static event GameEnded OnGameEnded;
        
        // Properties
        public bool IsGameActive => gameActive;
        public float MatchTime => matchTimer;
        public float TimeRemaining => Mathf.Max(0, matchTimeLimit - matchTimer);
        public Core.FactionType WinningTeam => winningTeam;
        public int BlueScore => scoreSync != null ? scoreSync.BlueScore : 0;
        public int RedScore => scoreSync != null ? scoreSync.RedScore : 0;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            
            // Find control point if not assigned
            if (controlPoint == null)
            {
                controlPoint = FindObjectOfType<ControlPoint>();
                if (debugMode)
                {
                    if (controlPoint != null)
                        Debug.Log($"[GameModeManager] Found ControlPoint: {controlPoint.name}");
                    else
                        Debug.LogWarning("[GameModeManager] ControlPoint not found!");
                }
            }
        }
        
        private void Start()
        {
            scoreSync = ScoreNetworkSync.Instance;
            
            if (debugMode)
            {
                if (scoreSync != null)
                    Debug.Log("[GameModeManager] ScoreNetworkSync found");
                else
                    Debug.LogWarning("[GameModeManager] ScoreNetworkSync not found!");
            }
            
            // Subscribe to control point events
            if (controlPoint != null)
            {
                ControlPoint.OnPointCaptured += OnControlPointCaptured;
                if (debugMode)
                    Debug.Log("[GameModeManager] Subscribed to ControlPoint events");
            }
        }
        
        private void OnDestroy()
        {
            if (controlPoint != null)
            {
                ControlPoint.OnPointCaptured -= OnControlPointCaptured;
            }
        }
        
        private void Update()
        {
            // Check if server just started and we haven't started the game yet
            if (NetworkServer.active && !hasStartedGame)
            {
                if (debugMode)
                    Debug.Log("[GameModeManager] Server detected in Update, starting game...");
                StartCoroutine(DelayedGameStart());
                hasStartedGame = true;
            }
            
            if (!gameActive)
            {
                return;
            }
            
            if (!NetworkServer.active)
            {
                return;
            }
            
            // Update match timer
            matchTimer += Time.deltaTime;
            
            // Check time limit
            if (matchTimer >= matchTimeLimit)
            {
                EndGame(GetLeadingTeam());
                return;
            }
            
            // Award points for holding the point
            if (controlPoint != null && controlPoint.CurrentOwner != Core.FactionType.None)
            {
                scoreTimer += Time.deltaTime;
                if (scoreTimer >= 1f)
                {
                    scoreTimer = 0f;
                    AwardPoints(controlPoint.CurrentOwner, pointsPerSecond);
                    
                    if (debugMode)
                    {
                        Debug.Log($"[GameModeManager] Awarding {pointsPerSecond} points to {controlPoint.CurrentOwner} team (Holding point)");
                    }
                }
            }
            
            // Check win conditions
            CheckWinConditions();
        }
        
        private IEnumerator DelayedGameStart()
        {
            yield return new WaitForSeconds(3f);
            StartGame();
        }
        
        public void StartGame()
        {
            if (gameActive)
            {
                if (debugMode)
                    Debug.LogWarning("[GameModeManager] Game already active!");
                return;
            }
            
            gameActive = true;
            matchTimer = 0f;
            scoreTimer = 0f;
            winningTeam = Core.FactionType.None;
            
            // Reset scores
            if (scoreSync != null)
            {
                scoreSync.ResetScores();
            }
            
            // Reset control point
            if (controlPoint != null)
            {
                controlPoint.ResetPoint();
            }
            
            OnGameStarted?.Invoke();
            
            if (debugMode)
            {
                Debug.Log("[GameModeManager] ==== KING OF THE HILL STARTED ====");
                Debug.Log($"[GameModeManager] Score to win: {scoreToWin} | Time limit: {matchTimeLimit}s");
                Debug.Log($"[GameModeManager] Points per second: {pointsPerSecond} | Capture bonus: {captureBonus}");
                Debug.Log($"[GameModeManager] Game is now ACTIVE");
            }
        }
        
        public void EndGame(Core.FactionType winner)
        {
            if (!gameActive) return;
            
            gameActive = false;
            winningTeam = winner;
            
            OnGameEnded?.Invoke(winner);
            
            if (debugMode)
            {
                Debug.Log($"[GameModeManager] ==== GAME ENDED ====");
                Debug.Log($"[GameModeManager] Winner: {winner}");
                Debug.Log($"[GameModeManager] Final scores - Blue: {BlueScore}, Red: {RedScore}");
            }
        }
        
        private void OnControlPointCaptured(Core.FactionType team)
        {
            // Award capture bonus
            AwardPoints(team, captureBonus);
            
            if (debugMode)
            {
                Debug.Log($"[GameModeManager] {team} team captured the point! +{captureBonus} bonus points");
            }
        }
        
        /// <summary>
        /// Award points - ScoreNetworkSync will automatically sync to clients
        /// </summary>
        private void AwardPoints(Core.FactionType team, int points)
        {
            if (scoreSync == null)
            {
                if (debugMode)
                    Debug.LogWarning("[GameModeManager] Cannot award points - ScoreNetworkSync is null!");
                return;
            }
            
            int scoreBefore = team == Core.FactionType.Blue ? scoreSync.BlueScore : scoreSync.RedScore;
            scoreSync.AddScore(team, points);
            int scoreAfter = team == Core.FactionType.Blue ? scoreSync.BlueScore : scoreSync.RedScore;
            
            if (debugMode)
            {
                Debug.Log($"[GameModeManager] Awarded {points} points to {team}. Score: {scoreBefore} → {scoreAfter}");
            }
        }
        
        private void CheckWinConditions()
        {
            if (scoreSync == null) return;
            
            // Check score victory
            if (scoreSync.BlueScore >= scoreToWin)
            {
                EndGame(Core.FactionType.Blue);
            }
            else if (scoreSync.RedScore >= scoreToWin)
            {
                EndGame(Core.FactionType.Red);
            }
        }
        
        private Core.FactionType GetLeadingTeam()
        {
            if (scoreSync == null) return Core.FactionType.None;
            
            if (scoreSync.BlueScore > scoreSync.RedScore)
                return Core.FactionType.Blue;
            else if (scoreSync.RedScore > scoreSync.BlueScore)
                return Core.FactionType.Red;
            else
                return Core.FactionType.None; // Tie
        }
        
        /// <summary>
        /// Get formatted time string (MM:SS)
        /// </summary>
        public string GetTimeString()
        {
            float time = TimeRemaining;
            int minutes = Mathf.FloorToInt(time / 60f);
            int seconds = Mathf.FloorToInt(time % 60f);
            return $"{minutes:00}:{seconds:00}";
        }
        
        /// <summary>
        /// Restart the game
        /// </summary>
        public void RestartGame()
        {
            EndGame(Core.FactionType.None);
            hasStartedGame = false;
            StartCoroutine(DelayedGameStart());
        }
    }
}
