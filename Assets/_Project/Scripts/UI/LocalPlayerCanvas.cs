using Mirror;
using UnityEngine;

namespace ElitesAndPawns.UI
{
    /// <summary>
    /// Enables/disables HUD canvas and sets up camera reference for local player only.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class LocalPlayerCanvas : NetworkBehaviour
    {
        private Canvas canvas;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Only enable for local player
            if (!isLocalPlayer)
            {
                canvas.enabled = false;
            }
            else
            {
                SetupCanvasCamera();
            }
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            
            // Ensure enabled for local player
            canvas.enabled = true;
            SetupCanvasCamera();
        }
        
        private void SetupCanvasCamera()
        {
            // Find the player camera in parent hierarchy
            Camera playerCam = GetComponentInParent<Camera>();
            if (playerCam == null)
            {
                playerCam = transform.parent.GetComponentInChildren<Camera>();
            }
            
            if (playerCam != null)
            {
                // Set to Screen Space - Camera mode for proper rendering
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = playerCam;
                canvas.planeDistance = 1f;
            }
            else
            {
                Debug.LogError("[LocalPlayerCanvas] Could not find player camera!");
            }
        }
    }
}
