using Mirror;
using UnityEngine;
using System.Collections;

namespace ElitesAndPawns.Networking
{
    /// <summary>
    /// Network identity for a player. Handles synchronization and faction assignment.
    /// This component is attached to the player prefab.
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        [Header("Player Info")]
        [SyncVar(hook = nameof(OnPlayerNameChanged))]
        [SerializeField] private string playerName = "Player";

        [SyncVar(hook = nameof(OnFactionChanged))]
        [SerializeField] private Core.FactionType faction = Core.FactionType.Blue;

        [SyncVar]
        [SerializeField] private int playerID;

        // Components (cached)
        private Player.PlayerController playerController;
        private Player.PlayerHealth playerHealth;

        // Properties
        public string PlayerName => playerName;
        public Core.FactionType Faction => faction;
        public int PlayerID => playerID;

        private void Awake()
        {
            // Cache components
            playerController = GetComponent<Player.PlayerController>();
            playerHealth = GetComponent<Player.PlayerHealth>();
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            
            // Delay setup to ensure client is ready
            StartCoroutine(DelayedLocalPlayerSetup());
        }
        
        private System.Collections.IEnumerator DelayedLocalPlayerSetup()
        {
            // Wait a frame to ensure everything is initialized
            yield return null;
            
            // Wait for client to be ready
            while (!NetworkClient.ready)
            {
                yield return new WaitForSeconds(0.1f);
            }
            
            // Setup local player specifics (camera, input, etc.)
            SetupLocalPlayer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Apply faction visuals
            ApplyFactionVisuals();
        }

        /// <summary>
        /// Set the player's faction (called by server)
        /// </summary>
        [Server]
        public void SetFaction(Core.FactionType newFaction)
        {
            faction = newFaction;
        }

        /// <summary>
        /// Set the player's name (called by client)
        /// </summary>
        [Command]
        public void CmdSetPlayerName(string newName)
        {
            playerName = newName;
        }

        /// <summary>
        /// Called when faction changes (on all clients)
        /// </summary>
        private void OnFactionChanged(Core.FactionType oldFaction, Core.FactionType newFaction)
        {
            ApplyFactionVisuals();
            
            // Update HUD if this is the local player
            if (isLocalPlayer)
            {
                UI.PlayerHUD hud = GetComponentInChildren<UI.PlayerHUD>();
                if (hud != null)
                {
                    hud.OnFactionChanged(newFaction);
                }
            }
        }

        /// <summary>
        /// Called when player name changes (on all clients)
        /// </summary>
        private void OnPlayerNameChanged(string oldName, string newName)
        {
            // Update nameplate UI if exists
            UpdateNameplate();
        }

        /// <summary>
        /// Setup local player (camera, input, etc.)
        /// </summary>
        private void SetupLocalPlayer()
        {
            // Enable local player camera
            Camera localCamera = GetComponentInChildren<Camera>();
            if (localCamera != null)
            {
                localCamera.enabled = true;
                localCamera.tag = "MainCamera";
            }

            // Enable input for local player
            if (playerController != null)
            {
                playerController.enabled = true;
            }

            // Set player name from GameManager or use default
            string desiredName = $"Player_{netId}"; // Default
            CmdSetPlayerName(desiredName);
        }

        /// <summary>
        /// Apply faction-specific visuals (color, etc.)
        /// </summary>
        private void ApplyFactionVisuals()
        {
            // Get the player's renderer
            Renderer playerRenderer = GetComponentInChildren<Renderer>();
            if (playerRenderer != null)
            {
                // Apply faction color
                Color factionColor = GetFactionColor(faction);
                playerRenderer.material.color = factionColor;
            }
        }

        /// <summary>
        /// Get color for faction
        /// </summary>
        private Color GetFactionColor(Core.FactionType factionType)
        {
            return factionType switch
            {
                Core.FactionType.Blue => Color.blue,
                Core.FactionType.Red => Color.red,
                Core.FactionType.Green => Color.green,
                _ => Color.white
            };
        }

        /// <summary>
        /// Update nameplate UI
        /// </summary>
        private void UpdateNameplate()
        {
            // TODO: Update 3D nameplate above player's head
        }
    }
}
