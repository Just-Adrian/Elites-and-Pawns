using UnityEngine;
using Mirror;

namespace ElitesAndPawns.GameModes
{
    /// <summary>
    /// Automatically configures GameModeCanvas to render correctly for the local player.
    /// Sets Canvas to Screen Space - Camera mode and assigns the local player's camera.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class GameModeCanvasSetup : MonoBehaviour
    {
        private Canvas canvas;
        private bool isSetup = false;
        
        private void Awake()
        {
            canvas = GetComponent<Canvas>();
        }
        
        private void Update()
        {
            // Keep trying until we find the local player's camera
            if (!isSetup)
            {
                SetupCanvas();
            }
        }
        
        private void SetupCanvas()
        {
            if (canvas == null) return;
            
            // Find the local player's camera
            Camera playerCamera = FindLocalPlayerCamera();
            
            if (playerCamera == null)
            {
                return; // Will retry next frame
            }
            
            // Set Canvas to Screen Space - Camera mode
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = playerCamera;
            canvas.planeDistance = 1f;
            
            isSetup = true;
        }
        
        private Camera FindLocalPlayerCamera()
        {
            // Find all NetworkPlayers in the scene
            Networking.NetworkPlayer[] networkPlayers = FindObjectsByType<Networking.NetworkPlayer>(FindObjectsSortMode.None);
            
            foreach (Networking.NetworkPlayer netPlayer in networkPlayers)
            {
                // Check if this is the local player
                if (netPlayer.isLocalPlayer)
                {
                    // Find the camera in this player's hierarchy
                    Camera cam = netPlayer.GetComponentInChildren<Camera>();
                    if (cam != null)
                    {
                        return cam;
                    }
                }
            }
            
            return null;
        }
    }
}
