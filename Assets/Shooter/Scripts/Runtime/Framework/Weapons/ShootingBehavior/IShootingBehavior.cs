using System;
using UnityEngine;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Defines the contract for weapon shooting behavior implementations.
    /// Implementations can define different shooting mechanics such as hitscan, projectile-based, or beam weapons.
    /// </summary>
    public interface IShootingBehavior
    {
        /// <summary>
        /// Executes a single shot using the provided shooting context.
        /// </summary>
        /// <param name="context">The <see cref="ShootingContext"/> containing all data needed to perform the shot.</param>
        void Shoot(ShootingContext context);

        /// <summary>
        /// Determines whether the weapon can currently shoot.
        /// </summary>
        /// <returns>True if shooting is allowed, false otherwise.</returns>
        bool CanShoot();

        /// <summary>
        /// Updates ongoing shooting behavior with a new direction.
        /// Used for weapons that maintain continuous fire or need direction updates during shooting.
        /// </summary>
        /// <param name="updatedDirection">The new shooting direction.</param>
        /// <param name="deltaTime">Time elapsed since the last update.</param>
        void UpdateShooting(Vector3 updatedDirection, float deltaTime);

        /// <summary>
        /// Stops the current shooting behavior.
        /// Used to clean up or reset state when the weapon stops firing.
        /// </summary>
        void StopShooting();
    }

    /// <summary>
    /// Contains all necessary data for executing a weapon shot.
    /// This struct is passed to <see cref="IShootingBehavior"/> implementations to provide context for shooting operations.
    /// </summary>
    [Serializable]
    public struct ShootingContext
    {
        #region Fields & Properties

        /// <summary>
        /// The GameObject that owns this weapon.
        /// </summary>
        public GameObject owner;

        /// <summary>
        /// The world-space origin point for the shot.
        /// </summary>
        public Vector3 origin;

        /// <summary>
        /// The direction vector for the shot.
        /// </summary>
        public Vector3 direction;

        /// <summary>
        /// The muzzle transform where the shot originates (typically used for visual effects).
        /// </summary>
        public Transform muzzle;

        /// <summary>
        /// The amount of damage this shot should deal.
        /// </summary>
        public float damage;

        /// <summary>
        /// The network client ID of the weapon owner.
        /// </summary>
        public ulong ownerClientId;

        /// <summary>
        /// The layer mask used to determine what can be hit by this shot.
        /// </summary>
        public LayerMask hitMask;

        /// <summary>
        /// The current spread angle applied to this shot in degrees.
        /// </summary>
        public float currentSpread;

        /// <summary>
        /// Reference to the weapon firing this shot.
        /// </summary>
        public IWeapon Weapon;

        #endregion

        #region Callbacks

        /// <summary>
        /// Callback invoked when ammo is consumed by the shot.
        /// The int parameter represents the amount of ammo consumed.
        /// </summary>
        public Action<int> OnAmmoConsumed;

        /// <summary>
        /// Callback invoked when a target is hit.
        /// Parameters are the hit GameObject and the <see cref="HitInfo"/> containing hit details.
        /// </summary>
        public Action<GameObject, HitInfo> OnTargetHit;

        /// <summary>
        /// Callback invoked when the hit point is calculated.
        /// Parameters are the hit position and hit normal vector.
        /// </summary>
        public Action<Vector3, Vector3> OnHitPointCalculated;

        /// <summary>
        /// Function to check if the weapon has sufficient ammo.
        /// The int parameter is the required ammo amount, returns true if available.
        /// </summary>
        public Func<int, bool> HasAmmoCheck;

        #endregion
    }
}
