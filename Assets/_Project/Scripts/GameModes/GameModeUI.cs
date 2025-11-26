using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ElitesAndPawns.GameModes
{
    /// <summary>
    /// UI controller for King of the Hill gamemode.
    /// Displays scores, capture progress, and game state.
    /// </summary>
    public class GameModeUI : MonoBehaviour
    {
        [Header("Score Display")]
        [SerializeField] private TMP_Text blueScoreText;
        [SerializeField] private TMP_Text redScoreText;
        [SerializeField] private TMP_Text timerText;

        [Header("Control Point Display")]
        [SerializeField] private GameObject capturePanel;
        [SerializeField] private Image captureProgressBar;
        [SerializeField] private TMP_Text captureStatusText;
        [SerializeField] private Image captureProgressFill;

        [Header("Game State Display")]
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private TMP_Text victoryText;
        [SerializeField] private TMP_Text finalScoreText;
        [SerializeField] private Button rematchButton;

        [Header("Colors")]
        [SerializeField] private Color blueColor = new Color(0.2f, 0.4f, 1f);
        [SerializeField] private Color redColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] private Color neutralColor = Color.gray;
        [SerializeField] private Color contestedColor = Color.yellow;

        private GameModeManager gameManager;
        private ControlPoint controlPoint;
        private ScoreNetworkSync scoreSync;

        private void Start()
        {
            // Get references
            gameManager = GameModeManager.Instance;
            controlPoint = FindAnyObjectByType<ControlPoint>();
            scoreSync = ScoreNetworkSync.Instance;

            // Subscribe to events
            if (gameManager != null)
            {
                GameModeManager.OnGameStarted += OnGameStarted;
                GameModeManager.OnGameEnded += OnGameEnded;
            }

            // Subscribe to SYNCHRONIZED score events
            ScoreNetworkSync.OnScoreUpdated += OnScoreUpdated;

            if (controlPoint != null)
            {
                ControlPoint.OnCaptureProgressChanged += OnCaptureProgressChanged;
                ControlPoint.OnContestedStateChanged += OnContestedStateChanged;
                ControlPoint.OnPointCaptured += OnPointCaptured;
            }

            // Setup UI
            if (victoryPanel != null)
                victoryPanel.SetActive(false);

            if (rematchButton != null)
                rematchButton.onClick.AddListener(OnRematchClicked);

            // Initial update
            UpdateScoreDisplay();
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (gameManager != null)
            {
                GameModeManager.OnGameStarted -= OnGameStarted;
                GameModeManager.OnGameEnded -= OnGameEnded;
            }

            ScoreNetworkSync.OnScoreUpdated -= OnScoreUpdated;

            if (controlPoint != null)
            {
                ControlPoint.OnCaptureProgressChanged -= OnCaptureProgressChanged;
                ControlPoint.OnContestedStateChanged -= OnContestedStateChanged;
                ControlPoint.OnPointCaptured -= OnPointCaptured;
            }
        }

        private void Update()
        {
            // Update timer
            if (gameManager != null && gameManager.IsGameActive)
            {
                UpdateTimerDisplay();
            }

            // Update control point status
            UpdateControlPointStatus();
        }

        private void OnGameStarted()
        {
            if (victoryPanel != null)
                victoryPanel.SetActive(false);

            if (capturePanel != null)
                capturePanel.SetActive(true);

            UpdateScoreDisplay();
        }

        private void OnGameEnded(Core.FactionType winner)
        {
            ShowVictoryScreen(winner);
        }

        /// <summary>
        /// Called when scores change via SyncVar (works on ALL clients!)
        /// </summary>
        private void OnScoreUpdated(int blueScore, int redScore)
        {
            if (blueScoreText != null)
                blueScoreText.text = blueScore.ToString();

            if (redScoreText != null)
                redScoreText.text = redScore.ToString();
        }

        private void OnCaptureProgressChanged(float progress, Core.FactionType capturingTeam)
        {
            // Set fill amount on the progress fill image
            if (captureProgressFill != null)
            {
                captureProgressFill.fillAmount = progress;

                // Set color based on capturing team
                Color fillColor = neutralColor;
                if (capturingTeam == Core.FactionType.Blue)
                    fillColor = blueColor;
                else if (capturingTeam == Core.FactionType.Red)
                    fillColor = redColor;

                captureProgressFill.color = fillColor;
            }
            
            // Also update the progress bar if it's separate
            if (captureProgressBar != null && captureProgressBar != captureProgressFill)
            {
                captureProgressBar.fillAmount = progress;
            }
        }

        private void OnContestedStateChanged(bool contested)
        {
            UpdateControlPointStatus();
        }

        private void OnPointCaptured(Core.FactionType team)
        {
            UpdateControlPointStatus();
        }

        private void UpdateScoreDisplay()
        {
            if (scoreSync == null) return;

            if (blueScoreText != null)
                blueScoreText.text = scoreSync.BlueScore.ToString();

            if (redScoreText != null)
                redScoreText.text = scoreSync.RedScore.ToString();
        }

        private void UpdateTimerDisplay()
        {
            if (timerText != null && gameManager != null)
            {
                timerText.text = gameManager.GetTimeString();
            }
        }

        private void UpdateControlPointStatus()
        {
            if (controlPoint == null || captureStatusText == null) return;

            string status = "";
            Color statusColor = neutralColor;

            if (controlPoint.IsContested)
            {
                status = "CONTESTED";
                statusColor = contestedColor;
            }
            else if (controlPoint.CurrentOwner == Core.FactionType.Blue)
            {
                status = "BLUE CONTROLLED";
                statusColor = blueColor;
            }
            else if (controlPoint.CurrentOwner == Core.FactionType.Red)
            {
                status = "RED CONTROLLED";
                statusColor = redColor;
            }
            else if (controlPoint.CapturingTeam != Core.FactionType.None)
            {
                status = $"{controlPoint.CapturingTeam.ToString().ToUpper()} CAPTURING";
                statusColor = controlPoint.CapturingTeam == Core.FactionType.Blue ? blueColor : redColor;
            }
            else
            {
                status = "NEUTRAL";
                statusColor = neutralColor;
            }

            captureStatusText.text = status;
            captureStatusText.color = statusColor;
        }

        private void ShowVictoryScreen(Core.FactionType winner)
        {
            if (victoryPanel == null) return;

            victoryPanel.SetActive(true);

            if (victoryText != null)
            {
                if (winner == Core.FactionType.None)
                {
                    victoryText.text = "DRAW!";
                    victoryText.color = neutralColor;
                }
                else
                {
                    victoryText.text = $"{winner.ToString().ToUpper()} TEAM WINS!";
                    victoryText.color = winner == Core.FactionType.Blue ? blueColor : redColor;
                }
            }

            if (finalScoreText != null && scoreSync != null)
            {
                finalScoreText.text = $"Final Score\nBlue: {scoreSync.BlueScore} | Red: {scoreSync.RedScore}";
            }
        }

        private void OnRematchClicked()
        {
            if (gameManager != null && Mirror.NetworkServer.active)
            {
                gameManager.RestartGame();
            }
        }
    }
}