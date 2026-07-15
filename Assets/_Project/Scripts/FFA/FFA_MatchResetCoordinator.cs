using UnityEngine;
using Unity.Netcode;
using Blocks.Gameplay.Core;
using Blocks.Gameplay.Shooter;

namespace FFA
{
    /// <summary>
    /// Coordinates match reset operations that cross Assembly Definition boundaries.
    /// 
    /// ┌─ THE ASMDEF PROBLEM ────────────────────────────────────────────────────────┐
    /// │  MatchManager is in the Core assembly. It cannot directly reference the     │
    /// │  Shooter assembly (WeaponController) or Assembly-CSharp (PowerUpSpawner)    │
    /// │  without causing a circular dependency, because Shooter depends on Core.    │
    /// └─────────────────────────────────────────────────────────────────────────────┘
    /// 
    /// ┌─ THE SOLUTION ──────────────────────────────────────────────────────────────┐
    /// │  This coordinator lives in Assembly-CSharp (no asmdef), which means it      │
    /// │  can see ALL assemblies. It listens to MatchManager.Phase and performs      │
    /// │  the cross-assembly reset logic (ammo refill and world cleanup) when the    │
    /// │  match restarts.                                                            │
    /// └─────────────────────────────────────────────────────────────────────────────┘
    /// 
    /// Setup:
    /// - Place this on a new GameObject in the scene (e.g., "MatchResetCoordinator").
    /// - Assign all PowerUpSpawner instances to the spawners array.
    /// </summary>
    public class FFA_MatchResetCoordinator : MonoBehaviour
    {
        [Header("World Cleanup")]
        [Tooltip("All PowerUpSpawner instances in the scene. Their active pickups are despawned when the match resets.")]
        [SerializeField] private PowerUpSpawner[] powerUpSpawners;

        private void Start()
        {
            // Wait for MatchManager to spawn before subscribing
            StartCoroutine(WaitAndSubscribe());
        }

        private System.Collections.IEnumerator WaitAndSubscribe()
        {
            yield return new WaitUntil(() => MatchManager.Instance != null);
            MatchManager.Instance.OnPhaseChanged += HandlePhaseChanged;
        }

        private void OnDestroy()
        {
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
            }
        }

        private void HandlePhaseChanged(MatchManager.MatchPhase phase)
        {
            // Reset logic only happens on the server/session owner during the Restarting phase
            if (phase != MatchManager.MatchPhase.Restarting || !NetworkManager.Singleton.IsServer)
                return;

            // 1. Cleanup World Objects (PowerUps and Projectiles)
            CleanupWorldObjects();

            // 2. Refill Ammo for all players
            RefillAllWeapons();
        }

        private void CleanupWorldObjects()
        {
            // Despawn pickups from every registered spawner
            if (powerUpSpawners != null)
            {
                foreach (var spawner in powerUpSpawners)
                {
                    if (spawner != null) spawner.DespawnAll();
                }
            }

            // Force-despawn any lingering in-flight projectiles
            var projectiles = FindObjectsByType<ModularProjectile>(FindObjectsSortMode.None);
            foreach (var projectile in projectiles)
            {
                if (projectile.HasAuthority && projectile.IsSpawned)
                {
                    projectile.NetworkObject.Despawn();
                }
            }
        }

        private void RefillAllWeapons()
        {
            // Iterate all connected clients and route the ammo reset RPC
            foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
            {
                var playerObj = kvp.Value.PlayerObject;
                if (playerObj == null) continue;

                if (playerObj.TryGetComponent<WeaponController>(out var weaponController))
                {
                    // Routes to the owner who writes to each AmmoHandler's NetworkVariable
                    weaponController.RequestAmmoResetRpc();
                }
            }
        }
    }
}
