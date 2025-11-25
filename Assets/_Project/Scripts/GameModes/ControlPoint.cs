using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ElitesAndPawns.GameModes
{
    /// <summary>
    /// Control Point for King of the Hill gamemode.
    /// Detects players in the capture zone and manages capture progress.
    /// Runs independently on all clients - syncs via isDead SyncVar in PlayerHealth.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ControlPoint : MonoBehaviour
    {
        [Header("Capture Settings")]
        [SerializeField] private float captureRadius = 10f;
        [SerializeField] private float captureTime = 5f; // Time to capture when uncontested
        [SerializeField] private float captureRate = 1f; // Capture progress per second
        [SerializeField] private float decayRate = 0.5f; // How fast capture progress decays
        
        [Header("Visual Settings")]
        [SerializeField] private Color neutralColor = Color.gray;
        [SerializeField] private Color blueColor = Color.blue;
        [SerializeField] private Color redColor = Color.red;
        [SerializeField] private Color contestedColor = Color.yellow;
        
        [Header("Components")]
        [SerializeField] private MeshRenderer ringRenderer;
        [SerializeField] private ParticleSystem captureParticles;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = true;
        [SerializeField] private bool showGizmos = true;
        
        // State
        private Core.FactionType currentOwner = Core.FactionType.None;
        private Core.FactionType captureTeam = Core.FactionType.None;
        private float captureProgress = 0f; // 0 to 1
        private bool isContested = false;
        
        // Players in zone
        private Dictionary<uint, Core.FactionType> playersInZone = new Dictionary<uint, Core.FactionType>();
        private List<Networking.NetworkPlayer> bluePlayersInZone = new List<Networking.NetworkPlayer>();
        private List<Networking.NetworkPlayer> redPlayersInZone = new List<Networking.NetworkPlayer>();
        
        // Events
        public delegate void PointCaptured(Core.FactionType team);
        public static event PointCaptured OnPointCaptured;
        
        public delegate void CaptureProgressChanged(float progress, Core.FactionType capturingTeam);
        public static event CaptureProgressChanged OnCaptureProgressChanged;
        
        public delegate void ContestedStateChanged(bool contested);
        public static event ContestedStateChanged OnContestedStateChanged;
        
        // Properties
        public Core.FactionType CurrentOwner => currentOwner;
        public float CaptureProgress => captureProgress;
        public bool IsContested => isContested;
        public Core.FactionType CapturingTeam => captureTeam;
        public int BluePlayersCount => bluePlayersInZone.Count;
        public int RedPlayersCount => redPlayersInZone.Count;
        
        private void Start()
        {
            // Ensure trigger collider
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;
            
            // Set initial visual state
            UpdateVisuals();
            
            if (debugMode)
            {
                Debug.Log("[ControlPoint] Initialized");
            }
        }
        
        private void Update()
        {
            UpdateCaptureProgress();
            UpdateVisuals();
        }
        
        private void OnTriggerEnter(Collider other)
        {
            // Check if it's a player
            var networkPlayer = other.GetComponent<Networking.NetworkPlayer>();
            if (networkPlayer == null) return;
            
            // Add to appropriate team list
            if (networkPlayer.Faction == Core.FactionType.Blue)
            {
                if (!bluePlayersInZone.Contains(networkPlayer))
                {
                    bluePlayersInZone.Add(networkPlayer);
                    playersInZone[networkPlayer.netId] = Core.FactionType.Blue;
                    
                    if (debugMode)
                    {
                        Debug.Log($"[ControlPoint] Blue player entered: {networkPlayer.PlayerName}");
                    }
                }
            }
            else if (networkPlayer.Faction == Core.FactionType.Red)
            {
                if (!redPlayersInZone.Contains(networkPlayer))
                {
                    redPlayersInZone.Add(networkPlayer);
                    playersInZone[networkPlayer.netId] = Core.FactionType.Red;
                    
                    if (debugMode)
                    {
                        Debug.Log($"[ControlPoint] Red player entered: {networkPlayer.PlayerName}");
                    }
                }
            }
            
            UpdateContestedState();
        }
        
        private void OnTriggerExit(Collider other)
        {
            // Check if it's a player
            var networkPlayer = other.GetComponent<Networking.NetworkPlayer>();
            if (networkPlayer == null) return;
            
            // Remove from appropriate team list
            if (bluePlayersInZone.Contains(networkPlayer))
            {
                bluePlayersInZone.Remove(networkPlayer);
                playersInZone.Remove(networkPlayer.netId);
                
                if (debugMode)
                {
                    Debug.Log($"[ControlPoint] Blue player left: {networkPlayer.PlayerName}");
                }
            }
            else if (redPlayersInZone.Contains(networkPlayer))
            {
                redPlayersInZone.Remove(networkPlayer);
                playersInZone.Remove(networkPlayer.netId);
                
                if (debugMode)
                {
                    Debug.Log($"[ControlPoint] Red player left: {networkPlayer.PlayerName}");
                }
            }
            
            UpdateContestedState();
        }
        
        private void UpdateContestedState()
        {
            // Clean up dead players before checking contested state
            CleanupPlayerLists();
            
            bool wasContested = isContested;
            // Contested = EQUAL numbers from both teams (numerical tie)
            isContested = bluePlayersInZone.Count > 0 && redPlayersInZone.Count > 0 && 
                          bluePlayersInZone.Count == redPlayersInZone.Count;
            
            if (wasContested != isContested)
            {
                OnContestedStateChanged?.Invoke(isContested);
                
                if (debugMode)
                {
                    Debug.Log($"[ControlPoint] Contested state: {isContested} (Blue: {bluePlayersInZone.Count}, Red: {redPlayersInZone.Count})");
                }
            }
        }
        
        /// <summary>
        /// Remove dead or null players from the zone lists.
        /// Called every frame to handle players whose colliders were disabled without triggering OnTriggerExit.
        /// </summary>
        private void CleanupPlayerLists()
        {
            // Clean blue players
            bluePlayersInZone.RemoveAll(player => 
            {
                if (player == null)
                {
                    if (debugMode) Debug.Log("[ControlPoint] Removed null blue player from zone");
                    return true;
                }
                
                var playerHealth = player.GetComponent<Player.PlayerHealth>();
                if (playerHealth != null && playerHealth.IsDead)
                {
                    if (debugMode) Debug.Log($"[ControlPoint] Removed dead blue player {player.PlayerName} from zone");
                    return true;
                }
                
                return false;
            });
            
            // Clean red players
            redPlayersInZone.RemoveAll(player => 
            {
                if (player == null)
                {
                    if (debugMode) Debug.Log("[ControlPoint] Removed null red player from zone");
                    return true;
                }
                
                var playerHealth = player.GetComponent<Player.PlayerHealth>();
                if (playerHealth != null && playerHealth.IsDead)
                {
                    if (debugMode) Debug.Log($"[ControlPoint] Removed dead red player {player.PlayerName} from zone");
                    return true;
                }
                
                return false;
            });
            
            // Also clean the dictionary
            var deadPlayers = new System.Collections.Generic.List<uint>();
            foreach (var kvp in playersInZone)
            {
                var player = System.Array.Find(
                    bluePlayersInZone.ToArray().Concat(redPlayersInZone.ToArray()).ToArray(),
                    p => p != null && p.netId == kvp.Key
                );
                
                if (player == null)
                {
                    deadPlayers.Add(kvp.Key);
                }
            }
            
            foreach (uint netId in deadPlayers)
            {
                playersInZone.Remove(netId);
            }
        }
        
        private void UpdateCaptureProgress()
        {
            // CRITICAL: Remove dead/null players from lists
            CleanupPlayerLists();
            
            // Don't update if contested
            if (isContested)
            {
                // Contested - no progress in either direction
                return;
            }
            
            // Determine which team is in the zone and their numerical advantage
            Core.FactionType dominantTeam = Core.FactionType.None;
            int teamAdvantage = 0;
            
            if (bluePlayersInZone.Count > redPlayersInZone.Count)
            {
                dominantTeam = Core.FactionType.Blue;
                // Net advantage = your players minus enemy players
                teamAdvantage = bluePlayersInZone.Count - redPlayersInZone.Count;
            }
            else if (redPlayersInZone.Count > bluePlayersInZone.Count)
            {
                dominantTeam = Core.FactionType.Red;
                // Net advantage = your players minus enemy players
                teamAdvantage = redPlayersInZone.Count - bluePlayersInZone.Count;
            }
            // If equal counts (and > 0), contested is already set to true and we returned early
            
            // Update capture progress
            if (dominantTeam != Core.FactionType.None)
            {
                // A team is in the zone
                if (currentOwner == dominantTeam)
                {
                    // Friendly team in zone - maintain full capture
                    if (captureProgress < 1f)
                    {
                        // Still capturing
                        captureTeam = dominantTeam;
                        float progressDelta = (captureRate / captureTime) * teamAdvantage * Time.deltaTime;
                        float oldProgress = captureProgress;
                        captureProgress = Mathf.Clamp01(captureProgress + progressDelta);
                        
                        // Check if just captured
                        if (captureProgress >= 1f && oldProgress < 1f)
                        {
                            CapturePoint(dominantTeam);
                        }
                        
                        OnCaptureProgressChanged?.Invoke(captureProgress, captureTeam);
                    }
                    // else: Point is fully captured and owned, stay at 100%
                }
                else if (currentOwner != Core.FactionType.None)
                {
                    // Enemy team in zone - decay the current owner's control
                    captureTeam = dominantTeam; // Enemy is attempting to capture
                    float oldProgress = captureProgress;
                    captureProgress -= decayRate * teamAdvantage * Time.deltaTime;
                    
                    if (captureProgress <= 0)
                    {
                        // Ownership lost, point becomes neutral
                        captureProgress = 0;
                        currentOwner = Core.FactionType.None;
                        
                        if (debugMode)
                        {
                            Debug.Log($"[ControlPoint] Point neutralized by {dominantTeam} team");
                        }
                    }
                    
                    OnCaptureProgressChanged?.Invoke(captureProgress, captureTeam);
                }
                else
                {
                    // Point is neutral (currentOwner == None)
                    // Check if there's partial progress from a different team
                    if (captureProgress > 0 && captureTeam != Core.FactionType.None && captureTeam != dominantTeam)
                    {
                        // Different team has partial progress - DECAY FIRST
                        float oldProgress = captureProgress;
                        captureProgress -= decayRate * teamAdvantage * Time.deltaTime;
                        captureProgress = Mathf.Max(0, captureProgress);
                        
                        if (captureProgress <= 0)
                        {
                            // Now neutralized, enemy can start capturing
                            captureTeam = dominantTeam;
                            
                            if (debugMode)
                            {
                                Debug.Log($"[ControlPoint] Partial progress neutralized, {dominantTeam} can now capture");
                            }
                        }
                        
                        OnCaptureProgressChanged?.Invoke(captureProgress, captureTeam);
                    }
                    else
                    {
                        // Neutral point with no conflicting progress, team can capture
                        captureTeam = dominantTeam;
                        float progressDelta = (captureRate / captureTime) * teamAdvantage * Time.deltaTime;
                        float oldProgress = captureProgress;
                        captureProgress = Mathf.Clamp01(captureProgress + progressDelta);
                        
                        // Check if captured
                        if (captureProgress >= 1f && oldProgress < 1f)
                        {
                            CapturePoint(dominantTeam);
                        }
                        
                        OnCaptureProgressChanged?.Invoke(captureProgress, captureTeam);
                    }
                }
            }
            else
            {
                // No one in zone
                // If point is fully captured, maintain ownership
                // Only decay if point was being captured but not fully owned
                if (captureProgress > 0 && captureProgress < 1f)
                {
                    // Point was being captured but not finished - decay back to neutral
                    captureProgress -= decayRate * Time.deltaTime;
                    captureProgress = Mathf.Max(0, captureProgress);
                    
                    if (captureProgress <= 0)
                    {
                        captureTeam = Core.FactionType.None;
                    }
                    
                    OnCaptureProgressChanged?.Invoke(captureProgress, captureTeam);
                }
                // else: Point is either fully captured (stays owned) or at 0 (stays neutral)
            }
        }
        
        private void CapturePoint(Core.FactionType team)
        {
            Core.FactionType previousOwner = currentOwner;
            currentOwner = team;
            captureProgress = 1f;
            captureTeam = team;
            
            OnPointCaptured?.Invoke(team);
            
            if (debugMode)
            {
                Debug.Log($"[ControlPoint] Point captured by {team} team! (was {previousOwner})");
            }
            
            // Visual feedback
            if (captureParticles != null)
            {
                captureParticles.Play();
            }
        }
        
        private void UpdateVisuals()
        {
            if (ringRenderer == null) return;
            
            Color targetColor;
            
            if (isContested)
            {
                targetColor = contestedColor;
            }
            else if (currentOwner == Core.FactionType.Blue)
            {
                targetColor = blueColor;
            }
            else if (currentOwner == Core.FactionType.Red)
            {
                targetColor = redColor;
            }
            else if (captureTeam != Core.FactionType.None && captureProgress > 0)
            {
                // Point is being captured, show progress
                Color captureColor = captureTeam == Core.FactionType.Blue ? blueColor : redColor;
                targetColor = Color.Lerp(neutralColor, captureColor, captureProgress);
            }
            else
            {
                targetColor = neutralColor;
            }
            
            ringRenderer.material.color = targetColor;
            
            // Update particle color
            if (captureParticles != null)
            {
                var main = captureParticles.main;
                main.startColor = targetColor;
            }
        }
        
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            
            // Draw capture radius
            Gizmos.color = new Color(1, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(transform.position, captureRadius);
            
            // Draw current state
            if (Application.isPlaying)
            {
                Color stateColor = neutralColor;
                if (isContested)
                    stateColor = contestedColor;
                else if (currentOwner == Core.FactionType.Blue)
                    stateColor = blueColor;
                else if (currentOwner == Core.FactionType.Red)
                    stateColor = redColor;
                
                Gizmos.color = stateColor;
                Gizmos.DrawSphere(transform.position, 1f);
                
                // Draw capture progress
                if (captureProgress > 0)
                {
                    Gizmos.color = captureTeam == Core.FactionType.Blue ? blueColor : redColor;
                    Vector3 progressHeight = Vector3.up * (captureProgress * 5f);
                    Gizmos.DrawLine(transform.position, transform.position + progressHeight);
                    Gizmos.DrawWireCube(transform.position + progressHeight, Vector3.one * 0.5f);
                }
            }
        }
        
        /// <summary>
        /// Reset the control point to neutral
        /// </summary>
        public void ResetPoint()
        {
            currentOwner = Core.FactionType.None;
            captureTeam = Core.FactionType.None;
            captureProgress = 0f;
            isContested = false;
            playersInZone.Clear();
            bluePlayersInZone.Clear();
            redPlayersInZone.Clear();
            UpdateVisuals();
            
            if (debugMode)
            {
                Debug.Log("[ControlPoint] Point reset to neutral");
            }
        }
    }
}
