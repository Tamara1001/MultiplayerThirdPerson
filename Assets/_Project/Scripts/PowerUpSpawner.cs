using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Periodically spawns a random power-up from <see cref="powerUpPrefabs"/> at this
    /// GameObject's position. Only the session owner runs the spawn loop.
    ///
    /// ┌─ MATCH RESET INTEGRATION ───────────────────────────────────────────────────┐
    /// │  <see cref="MatchManager"/> holds a reference to every PowerUpSpawner in   │
    /// │  the scene and calls <see cref="DespawnAll"/> when the match resets.        │
    /// │  All active pickups are tracked in <see cref="m_ActivePickups"/> and        │
    /// │  despawned via <see cref="NetworkObject.Despawn"/> (server authority only). │
    /// └─────────────────────────────────────────────────────────────────────────────┘
    /// </summary>
    public class PowerUpSpawner : MonoBehaviour
    {
        #region Inspector Fields

        [Tooltip("Pool of power-up prefabs to choose from randomly on each spawn cycle.")]
        [SerializeField] private NetworkObject[] powerUpPrefabs;

        [Tooltip("Minimum seconds between spawns.")]
        [SerializeField] private float minInterval = 25f;

        [Tooltip("Maximum seconds between spawns.")]
        [SerializeField] private float maxInterval = 40f;

        #endregion

        #region Private State

        /// <summary>
        /// Tracks all live pickup <see cref="NetworkObject"/>s spawned by this spawner.
        /// Used by <see cref="DespawnAll"/> to clean up the map at match reset.
        ///
        /// A pickup removes itself from this list when it is picked up or despawned
        /// by registering a callback on <see cref="NetworkObject.OnNetworkObjectDespawn"/>
        /// inside <see cref="SpawnPickup"/>.
        /// </summary>
        private readonly List<NetworkObject> m_ActivePickups = new List<NetworkObject>();

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Waits for the network to be ready, then starts the repeating spawn loop.
        /// The spawn loop runs only on the session owner (server host) because only
        /// the server has authority to call <see cref="NetworkObject.Spawn"/>.
        /// </summary>
        private IEnumerator Start()
        {
            // Wait until the NetworkManager is fully initialized before checking authority
            yield return new WaitUntil(() =>
                NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);

            // Only the session owner (host/server) should run the spawn loop
            if (!NetworkManager.Singleton.LocalClient.IsSessionOwner) yield break;

            while (true)
            {
                yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));

                // Clean up any pickups that were collected by players (destroyed) or despawned
                m_ActivePickups.RemoveAll(item => item == null || !item.IsSpawned);

                // Only spawn if there is no active pickup at this location
                if (m_ActivePickups.Count == 0 && powerUpPrefabs != null && powerUpPrefabs.Length > 0)
                {
                    var prefab = powerUpPrefabs[Random.Range(0, powerUpPrefabs.Length)];
                    SpawnPickup(prefab);
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Despawns all currently active pickups managed by this spawner.
        /// Must be called from the server/session owner — only the authority side
        /// can call <see cref="NetworkObject.Despawn"/>.
        ///
        /// Called by <see cref="MatchManager.CleanupWorldObjects"/> when the match resets.
        /// </summary>
        public void DespawnAll()
        {
            // Iterate backwards, despawn any valid spawned objects
            for (int i = m_ActivePickups.Count - 1; i >= 0; i--)
            {
                var netObj = m_ActivePickups[i];

                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn();
                }
            }

            // Clear the list entirely
            m_ActivePickups.Clear();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Instantiates and spawns a pickup from the given prefab.
        /// Adds it to <see cref="m_ActivePickups"/> and registers a despawn callback
        /// so the list stays accurate even when players pick up the item naturally.
        /// </summary>
        /// <param name="prefab">The NetworkObject prefab to spawn.</param>
        private void SpawnPickup(NetworkObject prefab)
        {
            if (prefab == null) return;

            // Instantiate locally first, then spawn onto the network
            var instance = Instantiate(prefab, transform.position, Quaternion.identity);
            instance.Spawn();

            // Track this pickup for cleanup at match reset
            m_ActivePickups.Add(instance);
        }

        #endregion
    }
}