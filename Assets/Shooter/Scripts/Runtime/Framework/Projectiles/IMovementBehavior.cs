using UnityEngine;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Interface for defining different projectile movement patterns
    /// </summary>
    public interface IMovementBehavior
    {
        /// <summary>
        /// Initialize the movement behavior with the projectile and other required components
        /// </summary>
        /// <param name="projectile">The SimpleProjectile this behavior belongs to</param>
        void Initialize(ModularProjectile projectile);

        /// <summary>
        /// Configure the initial movement parameters
        /// </summary>
        /// <param name="position">Initial position</param>
        /// <param name="direction">Initial direction</param>
        /// <param name="initialVelocity">Initial velocity vector (can be zero)</param>
        void SetupMovement(Vector3 position, Vector3 direction, Vector3 initialVelocity);

        /// <summary>
        /// Apply the initial movement state (called after network spawn)
        /// </summary>
        void ApplyInitialState();

        /// <summary>
        /// Update movement logic - called every physics update
        /// </summary>
        void UpdateMovement();

        /// <summary>
        /// Check if projectile has gone beyond boundary limits
        /// </summary>
        void CheckBoundary();

        /// <summary>
        /// Enable or disable boundary checking
        /// </summary>
        /// <param name="enableBoundary">Whether boundary checking is enabled</param>
        void SetBoundaryEnabled(bool enableBoundary);

        /// <summary>
        /// Get the current velocity of the projectile
        /// </summary>
        /// <returns>Current velocity vector</returns>
        Vector3 GetCurrentVelocity();

        /// <summary>
        /// Set the velocity of the projectile
        /// </summary>
        /// <param name="velocity">The new velocity vector</param>
        void SetVelocity(Vector3 velocity);
    }
}
