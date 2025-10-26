using UnityEngine;

namespace ElitesAndPawns.Core
{
    /// <summary>
    /// Central game manager that handles overall game state and scene transitions.
    /// Persists across scene loads.
    /// </summary>
    public class GameManager : Singleton<GameManager>
    {
        [Header("Game State")]
        [SerializeField] private GameState currentState = GameState.MainMenu;
        [SerializeField] private FactionType playerFaction = FactionType.Blue; // MVP: Blue only

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Events
        public event System.Action<GameState> OnGameStateChanged;

        // Properties
        public GameState CurrentState => currentState;
        public FactionType PlayerFaction => playerFaction;
        public bool IsDebugMode => debugMode;

        protected override void Awake()
        {
            base.Awake();
            
            if (debugMode)
            {
                Debug.Log("[GameManager] Initialized");
            }
        }

        private void Start()
        {
            // Initialize game systems here
            InitializeGame();
        }

        /// <summary>
        /// Initialize core game systems
        /// </summary>
        private void InitializeGame()
        {
            if (debugMode)
            {
                Debug.Log("[GameManager] Initializing game systems...");
            }

            // Set initial state
            ChangeGameState(GameState.MainMenu);

            // TODO: Initialize other managers (NetworkManager, AudioManager, etc.)
        }

        /// <summary>
        /// Change the current game state
        /// </summary>
        public void ChangeGameState(GameState newState)
        {
            if (currentState == newState) return;

            GameState oldState = currentState;
            currentState = newState;

            if (debugMode)
            {
                Debug.Log($"[GameManager] State changed: {oldState} → {newState}");
            }

            // Invoke state change event
            OnGameStateChanged?.Invoke(newState);

            // Handle state-specific logic
            OnStateEnter(newState);
        }

        /// <summary>
        /// Called when entering a new game state
        /// </summary>
        private void OnStateEnter(GameState state)
        {
            switch (state)
            {
                case GameState.MainMenu:
                    // TODO: Load main menu scene
                    break;

                case GameState.WarMapView:
                    // TODO: Load war map scene
                    break;

                case GameState.BattleLoading:
                    // TODO: Show loading screen
                    break;

                case GameState.InBattle:
                    // TODO: Initialize battle systems
                    break;

                case GameState.PostBattle:
                    // TODO: Show results screen
                    break;

                case GameState.Paused:
                    Time.timeScale = 0f;
                    break;
            }

            // Unpause if not in paused state
            if (state != GameState.Paused)
            {
                Time.timeScale = 1f;
            }
        }

        /// <summary>
        /// Set the player's chosen faction (MVP: Blue only)
        /// </summary>
        public void SetPlayerFaction(FactionType faction)
        {
            playerFaction = faction;
            
            if (debugMode)
            {
                Debug.Log($"[GameManager] Player faction set to: {faction}");
            }
        }

        /// <summary>
        /// Transition to War Map (RTS layer)
        /// </summary>
        public void TransitionToWarMap()
        {
            if (debugMode)
            {
                Debug.Log("[GameManager] Transitioning to War Map...");
            }

            ChangeGameState(GameState.WarMapView);
            // TODO: Load war map scene
        }

        /// <summary>
        /// Transition to Battle (FPS layer)
        /// </summary>
        public void TransitionToBattle()
        {
            if (debugMode)
            {
                Debug.Log("[GameManager] Transitioning to Battle...");
            }

            ChangeGameState(GameState.BattleLoading);
            // TODO: Load battle scene additively
        }

        /// <summary>
        /// Return to War Map after battle
        /// </summary>
        public void ReturnToWarMap()
        {
            if (debugMode)
            {
                Debug.Log("[GameManager] Returning to War Map...");
            }

            // TODO: Unload battle scene
            ChangeGameState(GameState.WarMapView);
        }

        /// <summary>
        /// Pause the game
        /// </summary>
        public void PauseGame()
        {
            ChangeGameState(GameState.Paused);
        }

        /// <summary>
        /// Resume the game
        /// </summary>
        public void ResumeGame()
        {
            // Return to previous non-paused state
            // For now, assume we're in battle
            ChangeGameState(GameState.InBattle);
        }

        /// <summary>
        /// Quit the application
        /// </summary>
        public void QuitGame()
        {
            if (debugMode)
            {
                Debug.Log("[GameManager] Quitting game...");
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
