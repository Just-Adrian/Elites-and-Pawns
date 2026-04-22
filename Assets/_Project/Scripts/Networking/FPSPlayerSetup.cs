using UnityEngine;
using Mirror;
using ElitesAndPawns.Core;

namespace ElitesAndPawns.Networking
{
    /// <summary>
    /// Attached to FPS player prefab. Handles faction assignment and initial setup
    /// when spawned from the war map.
    /// </summary>
    public class FPSPlayerSetup : NetworkBehaviour
    {
        [SyncVar]
        private Team faction = Team.None;
        
        [SyncVar]
        private int targetNodeId = -1;
        
        public Team Faction => faction;
        public int TargetNodeId => targetNodeId;
        
        /// <summary>
        /// Called by server when spawning this player.
        /// </summary>
        public void SetFaction(Team newFaction)
        {
            faction = newFaction;
            Debug.Log($"[FPSPlayerSetup] Faction set to: {faction}");
        }
        
        /// <summary>
        /// Called by server when spawning this player.
        /// </summary>
        public void SetNodeId(int nodeId)
        {
            targetNodeId = nodeId;
            Debug.Log($"[FPSPlayerSetup] Target node set to: {targetNodeId}");
        }
        
        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            
            Debug.Log($"[FPSPlayerSetup] Local player started! Faction={faction}, Node={targetNodeId}");
            
            // Enable camera, controls, etc. for local player
            EnableLocalPlayerComponents();
        }
        
        void EnableLocalPlayerComponents()
        {
            // Enable the camera for local player
            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                cam.gameObject.SetActive(true);
                cam.enabled = true;
                Debug.Log("[FPSPlayerSetup] Enabled player camera");
            }
            
            // Enable audio listener
            var listener = GetComponentInChildren<AudioListener>(true);
            if (listener != null)
            {
                listener.enabled = true;
            }
            
            // Make sure CharacterController or movement is enabled
            var charController = GetComponent<CharacterController>();
            if (charController != null)
            {
                charController.enabled = true;
            }
        }
    }
}
