using Mirror;
using UnityEngine;

namespace ElitesAndPawns.Networking
{
    /// <summary>
    /// Debug script to detect NetworkManager destruction.
    /// Attach this to NetworkManager GameObject temporarily to debug.
    /// </summary>
    public class NetworkManagerDebug : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log($"[NetworkManagerDebug] NetworkManager Awake on GameObject: {gameObject.name}");
        }

        private void Start()
        {
            Debug.Log($"[NetworkManagerDebug] NetworkManager Start on GameObject: {gameObject.name}");
        }

        private void OnDestroy()
        {
            Debug.LogError($"[NetworkManagerDebug] NetworkManager DESTROYED! GameObject: {gameObject.name}");
            Debug.LogError($"[NetworkManagerDebug] Stack trace: {System.Environment.StackTrace}");
        }

        private void Update()
        {
            // Check if there are multiple NetworkManagers
            var managers = FindObjectsOfType<NetworkManager>();
            if (managers.Length > 1)
            {
                Debug.LogError($"[NetworkManagerDebug] MULTIPLE NetworkManagers detected! Count: {managers.Length}");
                foreach (var manager in managers)
                {
                    Debug.LogError($"[NetworkManagerDebug] - NetworkManager on: {manager.gameObject.name}", manager.gameObject);
                }
            }
        }
    }
}
