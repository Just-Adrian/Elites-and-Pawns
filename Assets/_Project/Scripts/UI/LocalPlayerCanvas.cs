using Mirror;
using UnityEngine;

namespace ElitesAndPawns.UI
{
    /// <summary>
    /// Enables/disables HUD canvas and sets up camera reference
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class LocalPlayerCanvas : NetworkBehaviour
    {
        private Canvas canvas;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            
            Debug.Log($"[LocalPlayerCanvas] Awake - GameObject: {gameObject.name}");
            Debug.Log($"[LocalPlayerCanvas] Canvas found: {canvas != null}");
            Debug.Log($"[LocalPlayerCanvas] Canvas enabled: {canvas.enabled}");
            Debug.Log($"[LocalPlayerCanvas] Render Mode: {canvas.renderMode}");
            
            // Leave enabled by default
            Debug.Log("[LocalPlayerCanvas] Canvas left ENABLED - will hide for non-local players in OnStartClient");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Debug.Log($"[LocalPlayerCanvas] OnStartClient called. isLocalPlayer: {isLocalPlayer}");
            
            // Only disable for remote players
            if (!isLocalPlayer)
            {
                canvas.enabled = false;
                Debug.Log("[LocalPlayerCanvas] Remote player - Canvas DISABLED");
            }
            else
            {
                // For local player, set up the camera reference
                SetupCanvasCamera();
            }
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            
            Debug.Log("======= [LocalPlayerCanvas] OnStartLocalPlayer CALLED! =======");
            
            // Make absolutely sure it's enabled for local player
            canvas.enabled = true;
            
            // Set up camera reference
            SetupCanvasCamera();
            
            Debug.Log($"[LocalPlayerCanvas] Local player - Canvas ENABLED! canvas.enabled = {canvas.enabled}");
        }
        
        private void SetupCanvasCamera()
        {
            // Find the player camera
            Camera playerCam = GetComponentInParent<Camera>();
            if (playerCam == null)
            {
                playerCam = transform.parent.GetComponentInChildren<Camera>();
            }
            
            if (playerCam != null)
            {
                Debug.Log($"[LocalPlayerCanvas] Found camera: {playerCam.name}");
                
                // CRITICAL: Change to Screen Space - Camera mode
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = playerCam;
                
                // Set a reasonable plane distance
                canvas.planeDistance = 1f;
                
                Debug.Log($"[LocalPlayerCanvas] Canvas set to ScreenSpaceCamera with camera: {playerCam.name}");
            }
            else
            {
                Debug.LogError("[LocalPlayerCanvas] Could not find player camera!");
            }
        }
    }
}
