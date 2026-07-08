using UnityEngine;
using Unity.Netcode;
using System.Collections;

namespace Blocks.Gameplay.Core
{
    public class PowerUpSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkObject[] powerUpPrefabs;
        [SerializeField] private float minInterval = 25f;
        [SerializeField] private float maxInterval = 40f;

        private NetworkObject m_CurrentSpawn;

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);
            if (!NetworkManager.Singleton.LocalClient.IsSessionOwner) yield break;

            while (true)
            {
                yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
                if (m_CurrentSpawn == null && powerUpPrefabs.Length > 0)
                {
                    var prefab = powerUpPrefabs[Random.Range(0, powerUpPrefabs.Length)];
                    m_CurrentSpawn = Instantiate(prefab, transform.position, Quaternion.identity);
                    m_CurrentSpawn.Spawn();
                }
            }
        }
    }
}