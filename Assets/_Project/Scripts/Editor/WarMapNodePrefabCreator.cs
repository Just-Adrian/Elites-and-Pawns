using UnityEngine;
using UnityEditor;
using Mirror;

namespace ElitesAndPawns.Editor
{
    /// <summary>
    /// Editor utility to create the WarMapNode prefab.
    /// Use: Tools → Elites and Pawns → Create WarMapNode Prefab
    /// </summary>
    public static class WarMapNodePrefabCreator
    {
        [MenuItem("Tools/Elites and Pawns/Create WarMapNode Prefab")]
        public static void CreateWarMapNodePrefab()
        {
            // Create the prefab directory if it doesn't exist
            string prefabDir = "Assets/_Project/Prefabs/WarMap";
            if (!AssetDatabase.IsValidFolder(prefabDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "WarMap");
            }
            
            string prefabPath = $"{prefabDir}/WarMapNode.prefab";
            
            // Check if prefab already exists
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                if (!EditorUtility.DisplayDialog("Prefab Exists", 
                    "WarMapNode prefab already exists. Overwrite?", "Yes", "No"))
                {
                    return;
                }
            }
            
            // Create the root GameObject
            GameObject nodeGO = new GameObject("WarMapNode");
            
            // Add NetworkIdentity FIRST (required by NetworkBehaviour)
            nodeGO.AddComponent<NetworkIdentity>();
            
            // Add WarMapNode component
            nodeGO.AddComponent<ElitesAndPawns.WarMap.WarMapNode>();
            
            // Create visual sphere as child
            GameObject sphereGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereGO.name = "Visual";
            sphereGO.transform.SetParent(nodeGO.transform);
            sphereGO.transform.localPosition = Vector3.zero;
            sphereGO.transform.localScale = Vector3.one * 1.5f;
            
            // The sphere's collider will be used for clicking
            
            // Save as prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(nodeGO, prefabPath);
            
            // Cleanup the scene object
            Object.DestroyImmediate(nodeGO);
            
            // Select the new prefab
            Selection.activeObject = prefab;
            
            Debug.Log($"✓ Created WarMapNode prefab at: {prefabPath}");
            Debug.Log("IMPORTANT: Add this prefab to NetworkManager's 'Registered Spawnable Prefabs' list!");
            
            // Try to auto-register with NetworkManager
            AutoRegisterPrefab(prefab);
        }
        
        private static void AutoRegisterPrefab(GameObject prefab)
        {
            // Find NetworkManager in the scene or project
            var networkManagers = Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
            
            if (networkManagers.Length > 0)
            {
                var nm = networkManagers[0];
                
                // Check if already registered
                if (!nm.spawnPrefabs.Contains(prefab))
                {
                    nm.spawnPrefabs.Add(prefab);
                    EditorUtility.SetDirty(nm);
                    Debug.Log("✓ Auto-registered WarMapNode prefab with NetworkManager in scene!");
                }
            }
            else
            {
                Debug.LogWarning("No NetworkManager found in scene. Please manually add the prefab to NetworkManager's spawn list.");
            }
        }
        
        [MenuItem("Tools/Elites and Pawns/Register WarMapNode with NetworkManager")]
        public static void RegisterWithNetworkManager()
        {
            string prefabPath = "Assets/_Project/Prefabs/WarMap/WarMapNode.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            if (prefab == null)
            {
                Debug.LogError("WarMapNode prefab not found! Run 'Create WarMapNode Prefab' first.");
                return;
            }
            
            var networkManagers = Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
            
            if (networkManagers.Length == 0)
            {
                Debug.LogError("No NetworkManager found in scene!");
                return;
            }
            
            foreach (var nm in networkManagers)
            {
                if (!nm.spawnPrefabs.Contains(prefab))
                {
                    nm.spawnPrefabs.Add(prefab);
                    EditorUtility.SetDirty(nm);
                    Debug.Log($"✓ Registered WarMapNode prefab with {nm.name}");
                }
                else
                {
                    Debug.Log($"WarMapNode already registered with {nm.name}");
                }
            }
        }
    }
}
