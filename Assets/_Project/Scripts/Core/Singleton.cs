using UnityEngine;

namespace ElitesAndPawns.Core
{
    /// <summary>
    /// Generic singleton pattern for MonoBehaviour classes.
    /// Ensures only one instance exists and provides global access point.
    /// </summary>
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T instance;
        private static readonly object lockObject = new object();
        private static bool applicationIsQuitting = false;

        public static T Instance
        {
            get
            {
                if (applicationIsQuitting)
                {
                    Debug.LogWarning($"[Singleton] Instance of {typeof(T)} already destroyed on application quit. Returning null.");
                    return null;
                }

                lock (lockObject)
                {
                    if (instance == null)
                    {
                        // Find existing instance
                        instance = FindObjectOfType<T>();

                        if (instance == null)
                        {
                            // Create new instance
                            GameObject singletonObject = new GameObject($"{typeof(T).Name} (Singleton)");
                            instance = singletonObject.AddComponent<T>();
                            DontDestroyOnLoad(singletonObject);

                            Debug.Log($"[Singleton] Created singleton instance of {typeof(T)}");
                        }
                        else
                        {
                            Debug.Log($"[Singleton] Using existing instance of {typeof(T)}");
                        }
                    }

                    return instance;
                }
            }
        }

        protected virtual void Awake()
        {
            if (instance == null)
            {
                instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Debug.LogWarning($"[Singleton] Duplicate instance of {typeof(T)} detected. Destroying duplicate.");
                Destroy(gameObject);
            }
        }

        protected virtual void OnApplicationQuit()
        {
            applicationIsQuitting = true;
        }

        protected virtual void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
