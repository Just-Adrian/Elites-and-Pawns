using Mirror;
using UnityEngine;
using System.Collections;
using ElitesAndPawns.Core;
using ElitesAndPawns.WarMap;

namespace ElitesAndPawns.Networking
{
    /// <summary>
    /// Network identity for a player. Handles synchronization and faction assignment.
    /// This component is attached to the player prefab.
    /// 
    /// Supports both RTS (WarMap) and FPS battle contexts.
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        [Header("Player Info")]
        [SyncVar(hook = nameof(OnPlayerNameChanged))]
        [SerializeField] private string playerName = "Player";

        [SyncVar(hook = nameof(OnFactionChanged))]
        [SerializeField] private FactionType faction = FactionType.Blue;

        [SyncVar]
        [SerializeField] private int playerID;

        // Components (cached)
        private Player.PlayerController playerController;
        private Player.PlayerHealth playerHealth;

        // Properties
        public string PlayerName => playerName;
        public FactionType Faction => faction;
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
        
        private IEnumerator DelayedLocalPlayerSetup()
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

        #region Faction Management

        /// <summary>
        /// Set the player's faction (called by server)
        /// </summary>
        [Server]
        public void SetFaction(FactionType newFaction)
        {
            faction = newFaction;
        }

        /// <summary>
        /// Client requests a specific faction assignment.
        /// Server will validate and apply if allowed.
        /// </summary>
        [Command]
        public void CmdRequestFaction(FactionType requestedFaction)
        {
            // Validate the request
            if (requestedFaction == FactionType.None)
            {
                Debug.LogWarning($"[NetworkPlayer] Player {playerName} requested invalid faction");
                return;
            }
            
            // In FPS battles, assign based on battle parameters
            if (BattleManager.Instance != null)
            {
                // Check which factions are in this battle
                Team attacker = BattleManager.Instance.AttackingFaction;
                Team defender = BattleManager.Instance.DefendingFaction;
                
                // Convert requested faction to Team
                Team requestedTeam = requestedFaction == FactionType.Blue ? Team.Blue :
                                     requestedFaction == FactionType.Red ? Team.Red : Team.Green;
                
                // Only allow if this faction is part of the battle
                if (requestedTeam == attacker || requestedTeam == defender)
                {
                    faction = requestedFaction;
                    Debug.Log($"[NetworkPlayer] {playerName} assigned to {faction}");
                }
                else
                {
                    Debug.LogWarning($"[NetworkPlayer] {playerName} tried to join {requestedFaction} but battle is {attacker} vs {defender}");
                    // Assign to attacker by default
                    faction = attacker == Team.Blue ? FactionType.Blue : FactionType.Red;
                }
            }
            else
            {
                // No battle context - just assign
                faction = requestedFaction;
            }
        }

        /// <summary>
        /// Called when faction changes (on all clients)
        /// </summary>
        private void OnFactionChanged(FactionType oldFaction, FactionType newFaction)
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

        #endregion

        #region Player Name

        /// <summary>
        /// Set the player's name (called by client)
        /// </summary>
        [Command]
        public void CmdSetPlayerName(string newName)
        {
            if (!string.IsNullOrWhiteSpace(newName))
            {
                playerName = newName;
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

        #endregion

        #region Battle Server Info (RTS Context)

        /// <summary>
        /// Client requests info about a battle server to join.
        /// Used by ClientBattleRedirector.
        /// </summary>
        [Command]
        public void CmdRequestBattleServerInfo(int nodeId)
        {
            // Check if battle server exists
            if (DedicatedServerLauncher.Instance != null)
            {
                var info = DedicatedServerLauncher.Instance.GetBattleServerInfo(nodeId);
                if (info.HasValue)
                {
                    // Send back to requesting client
                    TargetReceiveBattleServerInfo(connectionToClient, nodeId, info.Value.address, info.Value.port);
                }
                else
                {
                    // No server yet, notify client
                    TargetReceiveBattleServerInfo(connectionToClient, nodeId, "", 0);
                }
            }
        }

        /// <summary>
        /// Server sends battle server info to specific client.
        /// </summary>
        [TargetRpc]
        private void TargetReceiveBattleServerInfo(NetworkConnectionToClient target, int nodeId, string address, ushort port)
        {
            if (port > 0 && !string.IsNullOrEmpty(address))
            {
                Debug.Log($"[NetworkPlayer] Received battle server info: {address}:{port}");
                
                // Tell ClientBattleRedirector
                if (ClientBattleRedirector.Instance != null)
                {
                    ClientBattleRedirector.Instance.JoinBattle(nodeId, address, port);
                }
            }
            else
            {
                Debug.Log($"[NetworkPlayer] No battle server available for node {nodeId}");
            }
        }

        #endregion

        #region Local Player Setup

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

            // Set player name from FPSAutoConnect if available, or use default
            string desiredName = $"Player_{netId}";
            
            if (FPSAutoConnect.Instance != null && !string.IsNullOrEmpty(FPSAutoConnect.Instance.PlayerName))
            {
                desiredName = FPSAutoConnect.Instance.PlayerName;
            }
            
            CmdSetPlayerName(desiredName);
            
            // Set faction if we have launch args
            if (FPSAutoConnect.Instance != null && FPSAutoConnect.Instance.ClientFaction != Team.None)
            {
                FactionType factionType = FPSAutoConnect.Instance.ClientFaction == Team.Blue ? FactionType.Blue :
                                          FPSAutoConnect.Instance.ClientFaction == Team.Red ? FactionType.Red : FactionType.None;
                if (factionType != FactionType.None)
                {
                    CmdRequestFaction(factionType);
                }
            }
        }

        #endregion

        #region Visuals

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
        private Color GetFactionColor(FactionType factionType)
        {
            return factionType switch
            {
                FactionType.Blue => Color.blue,
                FactionType.Red => Color.red,
                FactionType.Green => Color.green,
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

        #endregion
    }
}
