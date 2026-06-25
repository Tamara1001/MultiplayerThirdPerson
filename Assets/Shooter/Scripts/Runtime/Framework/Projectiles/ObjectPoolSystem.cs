using UnityEngine;
using Unity.Netcode;
using System.Collections;
using Unity.Netcode.Components;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Generic NetworkObject pool system used throughout the demo.
    /// </summary>
    public class ObjectPoolSystem : MonoBehaviour
    {
        public static Dictionary<GameObject, ObjectPoolSystem> ExistingPoolSystems = new Dictionary<GameObject, ObjectPoolSystem>();
        private static List<IPoolSystemTracker> s_PoolSystemTrackers = new List<IPoolSystemTracker>();

        public static void PoolSystemTrackerRegistration(IPoolSystemTracker tracker, bool register = true)
        {
            if (register)
            {
                if (!s_PoolSystemTrackers.Contains(tracker))
                {
                    s_PoolSystemTrackers.Add(tracker);
                }
            }
            else
            {
                s_PoolSystemTrackers.Remove(tracker);
            }
        }

        private void UpdatePoolSystemTrackers(ObjectPoolSystem poolSystem, float progress, bool isLoading = true)
        {
            foreach (var tracker in s_PoolSystemTrackers)
            {
                tracker.TrackPoolSystemLoading(poolSystem, progress, isLoading);
            }
        }

        public static ObjectPoolSystem GetPoolSystem(GameObject gameObject)
        {
            if (ExistingPoolSystems.ContainsKey(gameObject))
            {
                return ExistingPoolSystems[gameObject];
            }

            return null;
        }

        [Tooltip("The network prefabs to pool.")]
        public List<GameObject> networkPrefabs = new List<GameObject>();

        [Tooltip("How many instances of each network prefab you want available")]
        public int objectPoolSize = 50;

        [Tooltip("For organization purposes: when true, non-spawned instances will be migrated to the object pool's scene. (default is true)")]
        public bool poolInSystemScene = true;

        [Tooltip("When enabled, the pool will be used to spawn/recycle NetworkObjects")]
        public bool usePoolForSpawn = true;

        [Tooltip("Enable this to persist the pool objects between sessions (after first load, the pool is pre-loaded).")]
        public bool dontDestroyOnSceneUnload = false;

        [Tooltip("When true, an additional set of properties will be available that you can globally set on all pool object instances.")]
        public bool extendedProperties = false;

        [Tooltip("When true, the spawned objects will be configured to use unreliable deltas. Use this option to prevent stutter if packets are dropped due to poor network conditions.")]
        public bool useUnreliableDeltas = true;

        [Tooltip("When true, debug info will be logged about when objects are despawned and returned to the pool.")]
        public bool debugHandlerDestroy = false;

        [Tooltip("When enabled, this will expose more transform settings are applied to all spawned NetworkObjects.")]
        public bool enableTransformOverrides;

        [Tooltip("Enables half float precision.")]
        public bool halfFloat;

        [Tooltip("Enables quaternion synchronization.")]
        public bool quaternionSynchronization;

        [Tooltip("Enables quaternion compression.")]
        public bool quaternionCompression;

        [Tooltip("Enables interpolation.")] public bool interpolate;

        [Tooltip("When enabled, this pool will rebuild itself each time it is initialized upon loading a scene.")]
        public bool forceRebuildPool;

        // Dictionary to store separate pools for each prefab
        private Dictionary<GameObject, Stack<NetworkObject>> m_PrefabPools = new Dictionary<GameObject, Stack<NetworkObject>>();

        // Dictionary to store prefab handlers
        private Dictionary<GameObject, PrefabHandler> m_PrefabHandlers = new Dictionary<GameObject, PrefabHandler>();

        private NetworkVariable<bool> m_UsePoolForSpawn = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>
        /// Individual prefab handler that knows which specific prefab it handles
        /// </summary>
        private class PrefabHandler : INetworkPrefabInstanceHandler
        {
            private ObjectPoolSystem m_PoolSystem;
            private GameObject m_Prefab;

            public PrefabHandler(ObjectPoolSystem poolSystem, GameObject prefab)
            {
                m_PoolSystem = poolSystem;
                m_Prefab = prefab;
            }

            public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
            {
                var instance = m_PoolSystem.GetInstance(m_Prefab, !NetworkManager.Singleton.DistributedAuthorityMode && NetworkManager.Singleton.LocalClientId == ownerClientId);
                instance.transform.position = position;
                instance.transform.rotation = rotation;
                return instance;
            }

            public void Destroy(NetworkObject networkObject)
            {
                m_PoolSystem.ReturnToPool(networkObject);
            }
        }

        /// <summary>
        /// When a pooled object's state changes (active to not-active in the scene hierarchy), this method is invoked.
        /// </summary>
        private void HandleInstanceStateChange(GameObject instance, bool isSpawning = false)
        {
            if (poolInSystemScene)
            {
                if (!isSpawning)
                {
                    if (instance.transform.parent != null)
                    {
                        instance.transform.SetParent(null);
                    }

                    if (gameObject.scene.IsValid())
                    {
                        SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
                    }
                }
                else
                {
                    SceneManager.MoveGameObjectToScene(instance, SceneManager.GetActiveScene());
                }
            }

            instance.SetActive(isSpawning);
        }

        private void Start()
        {
            if (forceRebuildPool)
            {
                NetworkManager.Singleton.OnClientStopped += OnClientStopped;
                CleanOutPools();
            }

            Initialize();
        }

        private void Initialize()
        {
            bool anyNewPrefabs = false;

            foreach (var prefab in networkPrefabs)
            {
                if (prefab == null) continue;

                if (!ExistingPoolSystems.ContainsKey(prefab))
                {
                    anyNewPrefabs = true;
                    break;
                }
            }

            if (anyNewPrefabs)
            {
                // Register handlers for each prefab
                foreach (var prefab in networkPrefabs)
                {
                    if (prefab == null) continue;

                    if (!ExistingPoolSystems.ContainsKey(prefab))
                    {
                        var handler = new PrefabHandler(this, prefab);
                        m_PrefabHandlers[prefab] = handler;
                        NetworkManager.Singleton.PrefabHandler.AddHandler(prefab, handler);
                        ExistingPoolSystems.Add(prefab, this);

                        // Initialize pool for this prefab
                        m_PrefabPools[prefab] = new Stack<NetworkObject>();
                    }
                }

                if (dontDestroyOnSceneUnload)
                {
                    DontDestroyOnLoad(gameObject);
                }

                StartCoroutine(CreatePrefabPools());
            }
            else
            {
                // Re-register handlers for existing prefabs
                foreach (var prefab in networkPrefabs)
                {
                    if (prefab == null) continue;

                    if (ExistingPoolSystems.ContainsKey(prefab))
                    {
                        NetworkManager.Singleton.PrefabHandler.AddHandler(prefab, ExistingPoolSystems[prefab].m_PrefabHandlers[prefab]);
                    }
                }

                if (dontDestroyOnSceneUnload)
                {
                    Destroy(gameObject);
                }
            }
        }

        private void OnDestroy()
        {
            if ((forceRebuildPool || !dontDestroyOnSceneUnload))
            {
                CleanOutPools();
            }
        }

        private void OnClientStarted()
        {
            NetworkManager.Singleton.OnClientStopped += OnClientStopped;
        }

        private void OnClientStopped(bool obj)
        {
            NetworkManager.Singleton.OnClientStopped -= OnClientStopped;
            NetworkManager.Singleton.OnClientStarted += OnClientStarted;
            if (forceRebuildPool)
            {
                foreach (var prefab in networkPrefabs)
                {
                    if (prefab != null && ExistingPoolSystems.ContainsKey(prefab))
                    {
                        NetworkManager.Singleton.PrefabHandler.RemoveHandler(prefab);
                    }
                }
                CleanOutPools();
                Initialize();
            }
        }

        /// <summary>
        /// Coroutine that instantiates all the objects over time
        /// </summary>
        private IEnumerator CreatePrefabPools()
        {
            int totalObjectsToCreate = networkPrefabs.Count * objectPoolSize;
            int objectsCreated = 0;

            var splitCount = Mathf.Max(1, (int)(objectPoolSize * 0.1f));

            foreach (var prefab in networkPrefabs)
            {
                if (prefab == null) continue;

                var pool = m_PrefabPools[prefab];

                while (pool.Count < objectPoolSize)
                {
                    for (int i = 0; i < splitCount; i++)
                    {
                        var instance = Instantiate(prefab);
                        instance.name = instance.name.Replace("(Clone)", "");
                        instance.name += $"_{pool.Count.ToString()}_{prefab.name}";
                        HandleInstanceStateChange(instance);
                        var networkObject = instance.GetComponent<NetworkObject>();
                        networkObject.SetSceneObjectStatus();

                        if (extendedProperties)
                        {
                            var networkTransforms = instance.GetComponentsInChildren<NetworkTransform>();
                            foreach (var networkTransform in networkTransforms)
                            {
                                networkTransform.UseUnreliableDeltas = useUnreliableDeltas;
                                if (networkTransform != null && enableTransformOverrides)
                                {
                                    networkTransform.UseHalfFloatPrecision = halfFloat;
                                    networkTransform.UseQuaternionSynchronization = quaternionSynchronization;
                                    networkTransform.UseQuaternionCompression = quaternionCompression;
                                    networkTransform.Interpolate = interpolate;
                                }
                            }
                        }

                        pool.Push(networkObject);
                        // When not being used, parent under the pool system to make hierarchy browsing easier
                        // Turn off AutoObjectParentSync to avoid any errors with parenting
                        networkObject.AutoObjectParentSync = false;
                        instance.transform.parent = transform;

                        objectsCreated++;

                        if (pool.Count >= objectPoolSize)
                        {
                            break;
                        }
                    }

                    UpdatePoolSystemTrackers(this, objectsCreated / (float)totalObjectsToCreate);
                    yield return null;
                }
            }

            UpdatePoolSystemTrackers(this, 1.0f);
        }

        private void CleanOutPools()
        {
            foreach (var pool in m_PrefabPools.Values)
            {
                foreach (var poolObject in pool)
                {
                    if (poolObject != null && poolObject.gameObject != null)
                    {
                        Destroy(poolObject.gameObject);
                    }
                }
            }

            m_PrefabPools.Clear();

            foreach (var prefab in networkPrefabs)
            {
                if (prefab != null && ExistingPoolSystems.ContainsKey(prefab))
                {
                    ExistingPoolSystems.Remove(prefab);
                }
            }

            m_PrefabHandlers.Clear();
        }

        /// <summary>
        /// Gets an instance of the specified prefab from its pool
        /// </summary>
        public NetworkObject GetInstance(GameObject prefab, bool isSpawningLocally = false)
        {
            if (prefab == null || !m_PrefabPools.ContainsKey(prefab))
            {
                Debug.LogError($"Prefab {prefab?.name} not found in pool system!");
                return null;
            }

            var pool = m_PrefabPools[prefab];
            NetworkObject returnValue = null;

            if (m_UsePoolForSpawn.Value && pool.TryPop(out NetworkObject instance))
            {
                // When being used, remove the parent and turn AutoObjectParentSync back on again
                instance.transform.parent = null;
                instance.AutoObjectParentSync = true;
                HandleInstanceStateChange(instance.gameObject, true);
                instance.DeferredDespawnTick = 0;
                returnValue = instance;
            }
            else
            {
                if (m_UsePoolForSpawn.Value && NetworkManager.Singleton.LogLevel >= LogLevel.Developer)
                {
                    NetworkLog.LogWarningServer($"[Object Pool ({name}) Exhausted] Instantiating new instances during network session for prefab {prefab.name}!");
                }

                returnValue = Instantiate(prefab).GetComponent<NetworkObject>();
                returnValue.gameObject.name += "_NP";
            }

            if (isSpawningLocally)
            {
                var networkTransform = returnValue.GetComponent<NetworkTransform>();
                if (networkTransform != null && enableTransformOverrides)
                {
                    networkTransform.UseHalfFloatPrecision = halfFloat;
                    networkTransform.UseQuaternionSynchronization = quaternionSynchronization;
                    networkTransform.UseQuaternionCompression = quaternionCompression;
                    networkTransform.Interpolate = interpolate;
                }
            }

            return returnValue;
        }

        /// <summary>
        /// Returns a network object to its appropriate pool
        /// </summary>
        public void ReturnToPool(NetworkObject networkObject)
        {
            if (networkObject == null) return;

            // Find which prefab this object belongs to
            GameObject originalPrefab = null;
            foreach (var prefab in networkPrefabs)
            {
                if (prefab != null && networkObject.name.Contains(prefab.name))
                {
                    originalPrefab = prefab;
                    break;
                }
            }

            if (originalPrefab == null || !m_PrefabPools.ContainsKey(originalPrefab))
            {
                // Fallback - just destroy it
                Destroy(networkObject.gameObject);
                return;
            }

            var pool = m_PrefabPools[originalPrefab];

            if (!m_UsePoolForSpawn.Value && networkObject.gameObject.name.Contains("_NP"))
            {
                Destroy(networkObject.gameObject);
            }
            else
            {
                if (!debugHandlerDestroy)
                {
                    HandleInstanceStateChange(networkObject.gameObject);
                    pool.Push(networkObject);
                }
                else
                {
                    if (networkObject.IsSpawned)
                    {
                        Debug.LogError($"[{networkObject.name}] Is still spawned but is being put back into pool!");
                    }

                    if (!pool.Contains(networkObject))
                    {
                        HandleInstanceStateChange(networkObject.gameObject);
                        pool.Push(networkObject);
                    }
                    else
                    {
                        Debug.LogError($"[ObjectPoolSystem] PrefabHandler invoked twice for {networkObject.name}!");
                    }
                }

                networkObject.transform.position = Vector3.zero;
                networkObject.transform.rotation = Quaternion.identity;
                // When not being used, parent under the pool system to make hierarchy browsing easier
                // Turn off AutoObjectParentSync to avoid any errors with parenting
                networkObject.AutoObjectParentSync = false;
                networkObject.transform.parent = transform;
            }
        }
    }

    public interface IPoolSystemTracker
    {
        void TrackPoolSystemLoading(ObjectPoolSystem poolSystem, float progress, bool isLoading = true);
    }
}
