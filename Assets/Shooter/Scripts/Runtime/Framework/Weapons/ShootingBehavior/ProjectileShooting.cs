using UnityEngine;
using Unity.Netcode;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Implements projectile-based shooting behavior that spawns and launches physical projectile objects.
    /// Supports both object pooling for performance and legacy rigidbody-based projectiles.
    /// </summary>
    public class ProjectileShooting : NetworkBehaviour, IShootingBehavior
    {
        #region Fields & Properties

        [Header("Projectile Settings")]
        [Tooltip("The networked projectile prefab to spawn when shooting.")]
        [SerializeField] private NetworkObject projectilePrefab;
        [Tooltip("Speed applied to legacy projectiles (only used if projectile lacks ModularProjectile component).")]
        [SerializeField] private float projectileSpeed = 20f;

        private ObjectPoolSystem m_ProjectilePool;

        #endregion

        #region Unity Methods

        private void Start()
        {
            // Attempt to find an existing object pool for the projectile prefab
            if (projectilePrefab != null &&
                ObjectPoolSystem.ExistingPoolSystems.TryGetValue(projectilePrefab.gameObject, out var pool))
            {
                m_ProjectilePool = pool;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Spawns and launches a projectile in the specified direction with spread applied.
        /// Uses object pooling if available, otherwise instantiates a new projectile.
        /// </summary>
        /// <param name="context">The <see cref="ShootingContext"/> containing all data needed to perform the shot.</param>
        public void Shoot(ShootingContext context)
        {
            NetworkObject projectile = GetProjectile();
            if (projectile == null) return;

            Vector3 spawnPosition = context.muzzle != null ? context.muzzle.position : context.origin;
            Vector3 fireDirection = ApplySpread(context.direction, context.currentSpread);

            projectile.transform.SetPositionAndRotation(spawnPosition, Quaternion.LookRotation(fireDirection));

            var baseProjectile = projectile.GetComponent<ModularProjectile>();
            if (baseProjectile != null)
            {
                baseProjectile.LaunchWithContext(spawnPosition, fireDirection, Vector3.zero, context.Weapon as ModularWeapon, context,
                    context.owner);
            }
            else
            {
                // Legacy fallback for projectiles without ModularProjectile component
                Rigidbody rb = projectile.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = fireDirection * projectileSpeed;
                }
            }

            // Spawn projectile on the network if not already spawned
            if (!projectile.IsSpawned)
            {
                projectile.SpawnWithOwnership(context.ownerClientId);
            }

            context.OnAmmoConsumed?.Invoke(1);
        }

        /// <summary>
        /// Determines whether the weapon can currently shoot.
        /// </summary>
        /// <returns>True if a projectile prefab is assigned, false otherwise.</returns>
        public bool CanShoot() => projectilePrefab != null;

        /// <summary>
        /// Updates ongoing shooting behavior with a new direction. Not used for projectile shooting.
        /// </summary>
        /// <param name="updatedDirection">The updated shooting direction.</param>
        /// <param name="deltaTime">Time elapsed since the last update.</param>
        public void UpdateShooting(Vector3 updatedDirection, float deltaTime) { }

        /// <summary>
        /// Stops the shooting behavior. Not used for projectile shooting.
        /// </summary>
        public void StopShooting() { }

        #endregion

        #region Private Methods

        /// <summary>
        /// Gets a projectile instance from the object pool or instantiates a new one.
        /// Prioritizes using the object pool for performance if available.
        /// </summary>
        /// <returns>A <see cref="NetworkObject"/> projectile instance, or null if no prefab is assigned.</returns>
        private NetworkObject GetProjectile()
        {
            if (m_ProjectilePool != null)
            {
                return m_ProjectilePool.GetInstance(projectilePrefab.gameObject, IsOwner);
            }
            else if (projectilePrefab != null)
            {
                return Instantiate(projectilePrefab);
            }

            return null;
        }

        /// <summary>
        /// Applies random spread to the firing direction based on the spread angle.
        /// Uses a random point within a unit circle to create cone-shaped spread.
        /// </summary>
        /// <param name="direction">The base firing direction.</param>
        /// <param name="spreadAngle">The maximum spread angle in degrees.</param>
        /// <returns>The direction with spread applied.</returns>
        private Vector3 ApplySpread(Vector3 direction, float spreadAngle)
        {
            if (spreadAngle <= 0)
            {
                return direction;
            }

            Vector2 randomCircle = Random.insideUnitCircle;
            Quaternion spreadRotation = Quaternion.Euler(randomCircle.y * spreadAngle, randomCircle.x * spreadAngle, 0);
            return spreadRotation * direction;
        }

        #endregion
    }
}
