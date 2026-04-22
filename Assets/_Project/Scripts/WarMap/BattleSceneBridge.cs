using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
using ElitesAndPawns.Core;

namespace ElitesAndPawns.WarMap
{
    /// <summary>
    /// Manages loading and unloading FPS battle scenes additively.
    /// Allows war map to continue running while battles happen.
    /// </summary>
    public class BattleSceneBridge : NetworkBehaviour
    {
        #region Singleton
        
        private static BattleSceneBridge _instance;
        public static BattleSceneBridge Instance => _instance;
        
        #endregion
        
        #region Configuration
        
        [Header("Scene Settings")]
        [SerializeField] private string defaultBattleScene = "NetworkTest";
        [SerializeField] private float sceneLoadTimeout = 30f;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = true;
        
        #endregion
        
        #region State
        
        // Active battles (nodeId -> battle info)
        private Dictionary<int, ActiveBattle> activeBattles = new Dictionary<int, ActiveBattle>();
        
        // Pending parameters waiting for scene load
        private Dictionary<int, BattleParameters> pendingBattles = new Dictionary<int, BattleParameters>();
        
        #endregion
        
        #region Events
        
        public static event Action<int, string> OnBattleSceneLoading; // nodeId, sceneName
        public static event Action<int, string> OnBattleSceneLoaded; // nodeId, sceneName
        public static event Action<int> OnBattleSceneUnloading; // nodeId
        public static event Action<int> OnBattleSceneUnloaded; // nodeId
        
        #endregion
        
        #region Properties
        
        public int ActiveBattleCount => activeBattles.Count;
        
        #endregion
        
        #region Unity Lifecycle
        
        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }
        
        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Start a new battle at a contested node.
        /// Loads the battle scene additively and initializes the battle.
        /// </summary>
        [Server]
        public void StartBattle(BattleParameters parameters)
        {
            if (parameters == null)
            {
                Debug.LogError("[BattleSceneBridge] Cannot start battle - parameters are null!");
                return;
            }
            
            int nodeId = parameters.NodeId;
            
            // Check if battle already exists for this node
            if (activeBattles.ContainsKey(nodeId))
            {
                Debug.LogWarning($"[BattleSceneBridge] Battle already active at node {nodeId}");
                return;
            }
            
            string sceneName = !string.IsNullOrEmpty(parameters.BattleSceneName) 
                ? parameters.BattleSceneName 
                : defaultBattleScene;
            
            Debug.Log($"[BattleSceneBridge] Starting battle for node {nodeId}, loading scene '{sceneName}'");
            
            // Store pending parameters
            pendingBattles[nodeId] = parameters;
            
            // Start loading scene
            StartCoroutine(LoadBattleSceneCoroutine(nodeId, sceneName));
            
            OnBattleSceneLoading?.Invoke(nodeId, sceneName);
            RpcNotifyBattleLoading(nodeId, sceneName);
        }
        
        /// <summary>
        /// End a battle and unload its scene.
        /// </summary>
        [Server]
        public void EndBattle(int nodeId)
        {
            if (!activeBattles.TryGetValue(nodeId, out ActiveBattle battle))
            {
                Debug.LogWarning($"[BattleSceneBridge] No active battle at node {nodeId}");
                return;
            }
            
            Debug.Log($"[BattleSceneBridge] Ending battle at node {nodeId}");
            
            StartCoroutine(UnloadBattleSceneCoroutine(nodeId, battle.SceneName));
            
            OnBattleSceneUnloading?.Invoke(nodeId);
            RpcNotifyBattleUnloading(nodeId);
        }
        
        /// <summary>
        /// Check if a battle is active at a node.
        /// </summary>
        public bool IsBattleActive(int nodeId)
        {
            return activeBattles.ContainsKey(nodeId);
        }
        
        /// <summary>
        /// Get active battle info for a node.
        /// </summary>
        public ActiveBattle GetActiveBattle(int nodeId)
        {
            return activeBattles.TryGetValue(nodeId, out ActiveBattle battle) ? battle : null;
        }
        
        /// <summary>
        /// Get all active battles.
        /// </summary>
        public IEnumerable<ActiveBattle> GetAllActiveBattles()
        {
            return activeBattles.Values;
        }
        
        #endregion
        
        #region Scene Loading
        
        private IEnumerator LoadBattleSceneCoroutine(int nodeId, string sceneName)
        {
            // Check if scene exists
            // Note: In production, verify scene is in build settings
            
            // Load scene additively
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            
            if (loadOp == null)
            {
                Debug.LogError($"[BattleSceneBridge] Failed to start loading scene '{sceneName}'");
                pendingBattles.Remove(nodeId);
                yield break;
            }
            
            float startTime = Time.time;
            
            while (!loadOp.isDone)
            {
                if (Time.time - startTime > sceneLoadTimeout)
                {
                    Debug.LogError($"[BattleSceneBridge] Scene load timed out for '{sceneName}'");
                    pendingBattles.Remove(nodeId);
                    yield break;
                }
                
                if (debugMode && loadOp.progress < 0.9f)
                {
                    Debug.Log($"[BattleSceneBridge] Loading '{sceneName}': {loadOp.progress:P0}");
                }
                
                yield return null;
            }
            
            Debug.Log($"[BattleSceneBridge] Scene '{sceneName}' loaded successfully");
            
            // Get the loaded scene
            Scene loadedScene = SceneManager.GetSceneByName(sceneName);
            
            // Initialize battle in the loaded scene
            yield return StartCoroutine(InitializeBattleInScene(nodeId, loadedScene));
        }
        
        private IEnumerator InitializeBattleInScene(int nodeId, Scene scene)
        {
            // Wait a frame for scene objects to initialize
            yield return null;
            
            if (!pendingBattles.TryGetValue(nodeId, out BattleParameters parameters))
            {
                Debug.LogError($"[BattleSceneBridge] No pending parameters for node {nodeId}");
                yield break;
            }
            
            // Find or create BattleManager in the scene
            BattleManager battleManager = null;
            BattleLobby battleLobby = null;
            
            // Search in loaded scene
            GameObject[] rootObjects = scene.GetRootGameObjects();
            foreach (var obj in rootObjects)
            {
                if (battleManager == null)
                    battleManager = obj.GetComponentInChildren<BattleManager>();
                if (battleLobby == null)
                    battleLobby = obj.GetComponentInChildren<BattleLobby>();
            }
            
            // Create BattleManager if not found
            if (battleManager == null)
            {
                GameObject managerObj = new GameObject("BattleManager");
                SceneManager.MoveGameObjectToScene(managerObj, scene);
                battleManager = managerObj.AddComponent<BattleManager>();
                managerObj.AddComponent<NetworkIdentity>();
                NetworkServer.Spawn(managerObj);
                Debug.Log("[BattleSceneBridge] Created BattleManager in battle scene");
            }
            
            // Create BattleLobby if not found
            if (battleLobby == null)
            {
                GameObject lobbyObj = new GameObject("BattleLobby");
                SceneManager.MoveGameObjectToScene(lobbyObj, scene);
                battleLobby = lobbyObj.AddComponent<BattleLobby>();
                lobbyObj.AddComponent<NetworkIdentity>();
                NetworkServer.Spawn(lobbyObj);
                Debug.Log("[BattleSceneBridge] Created BattleLobby in battle scene");
            }
            
            // Wait another frame for network objects
            yield return null;
            
            // Initialize battle
            battleManager.InitializeBattle(parameters);
            battleLobby.InitializeLobby(parameters);
            
            // Track active battle
            var activeBattle = new ActiveBattle
            {
                NodeId = nodeId,
                SceneName = scene.name,
                Parameters = parameters,
                BattleManager = battleManager,
                BattleLobby = battleLobby,
                StartTime = Time.time
            };
            
            activeBattles[nodeId] = activeBattle;
            pendingBattles.Remove(nodeId);
            
            Debug.Log($"[BattleSceneBridge] Battle initialized at node {nodeId}");
            
            OnBattleSceneLoaded?.Invoke(nodeId, scene.name);
            RpcNotifyBattleLoaded(nodeId, scene.name);
        }
        
        private IEnumerator UnloadBattleSceneCoroutine(int nodeId, string sceneName)
        {
            // Clean up battle state first
            if (activeBattles.TryGetValue(nodeId, out ActiveBattle battle))
            {
                // Destroy networked objects
                if (battle.BattleManager != null)
                {
                    NetworkServer.Destroy(battle.BattleManager.gameObject);
                }
                if (battle.BattleLobby != null)
                {
                    NetworkServer.Destroy(battle.BattleLobby.gameObject);
                }
            }
            
            activeBattles.Remove(nodeId);
            
            // Wait a frame for cleanup
            yield return null;
            
            // Check if scene is still loaded
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded)
            {
                Debug.LogWarning($"[BattleSceneBridge] Scene '{sceneName}' already unloaded");
                OnBattleSceneUnloaded?.Invoke(nodeId);
                yield break;
            }
            
            // Unload scene
            AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(sceneName);
            
            if (unloadOp == null)
            {
                Debug.LogError($"[BattleSceneBridge] Failed to start unloading scene '{sceneName}'");
                yield break;
            }
            
            while (!unloadOp.isDone)
            {
                yield return null;
            }
            
            Debug.Log($"[BattleSceneBridge] Scene '{sceneName}' unloaded");
            
            OnBattleSceneUnloaded?.Invoke(nodeId);
            RpcNotifyBattleUnloaded(nodeId);
        }
        
        #endregion
        
        #region RPCs
        
        [ClientRpc]
        private void RpcNotifyBattleLoading(int nodeId, string sceneName)
        {
            if (!isServer)
            {
                Debug.Log($"[BattleSceneBridge] Battle loading at node {nodeId}: {sceneName}");
                OnBattleSceneLoading?.Invoke(nodeId, sceneName);
            }
        }
        
        [ClientRpc]
        private void RpcNotifyBattleLoaded(int nodeId, string sceneName)
        {
            if (!isServer)
            {
                Debug.Log($"[BattleSceneBridge] Battle loaded at node {nodeId}: {sceneName}");
                OnBattleSceneLoaded?.Invoke(nodeId, sceneName);
            }
        }
        
        [ClientRpc]
        private void RpcNotifyBattleUnloading(int nodeId)
        {
            if (!isServer)
            {
                Debug.Log($"[BattleSceneBridge] Battle unloading at node {nodeId}");
                OnBattleSceneUnloading?.Invoke(nodeId);
            }
        }
        
        [ClientRpc]
        private void RpcNotifyBattleUnloaded(int nodeId)
        {
            if (!isServer)
            {
                Debug.Log($"[BattleSceneBridge] Battle unloaded at node {nodeId}");
                OnBattleSceneUnloaded?.Invoke(nodeId);
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Information about an active battle.
    /// </summary>
    [Serializable]
    public class ActiveBattle
    {
        public int NodeId;
        public string SceneName;
        public BattleParameters Parameters;
        public BattleManager BattleManager;
        public BattleLobby BattleLobby;
        public float StartTime;
        
        public float Duration => Time.time - StartTime;
    }
}
