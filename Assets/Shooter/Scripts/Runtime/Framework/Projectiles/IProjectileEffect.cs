using System;
using UnityEngine;
using Unity.Netcode.Components;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Defines the complete behavior of a projectile, including its lifecycle, collision, and impact effects.
    /// This interface merges the responsibilities of the previous IImpactBehavior and IProjectileBehavior.
    /// </summary>
    public interface IProjectileEffect : IContactEventHandlerWithInfo
    {
        /// <summary>
        /// Initializes the effect with a reference to its projectile chassis.
        /// </summary>
        void Initialize(ModularProjectile projectile);

        /// <summary>
        /// Configures the effect with data from the weapon that fired it.
        /// </summary>
        void Setup(GameObject owner, IWeapon sourceWeapon, ShootingContext context);

        /// <summary>
        /// Called when the projectile is launched to start timers and other logic.
        /// </summary>
        void OnLaunch();

        /// <summary>
        /// Called every frame to handle time-based logic, such as fuse timers.
        /// </summary>
        void ProcessUpdate();

        /// <summary>
        /// Cleans up any resources or state when the projectile is despawned.
        /// </summary>
        void Cleanup();

        /// <summary>
        /// Gets a value indicating whether the projectile should use deferred despawning.
        /// </summary>
        bool IsDeferredDespawnEnabled { get; }

        /// <summary>
        /// Gets the number of network ticks to wait before despawning if deferred despawning is enabled.
        /// </summary>
        int DeferredDespawnTicks { get; }

        /// <summary>
        /// Event invoked when the projectile's effect is complete (e.g., after impact or timeout),
        /// signaling that it should be despawned.
        /// </summary>
        event Action<ModularProjectile> OnEffectComplete;
    }
}
