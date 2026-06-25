using System;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Defines the contract for weapon firing mechanisms.
    /// Implementations control how weapons fire (e.g., automatic, burst, semi-automatic).
    /// </summary>
    public interface IFiringMechanism
    {
        #region Properties

        /// <summary>
        /// Gets a value indicating whether the mechanism can start firing.
        /// Returns false if the mechanism is on cooldown, reloading, or in another state that prevents firing.
        /// </summary>
        bool CanStartFiring { get; }

        /// <summary>
        /// Gets a value indicating whether the mechanism is currently in the process of firing.
        /// </summary>
        bool IsFiring { get; }

        /// <summary>
        /// Gets the fire rate of the mechanism in seconds.
        /// This represents the time between shots or bursts depending on the implementation.
        /// </summary>
        float FireRate { get; }

        #endregion

        #region Events

        /// <summary>
        /// Event invoked when the weapon should fire a shot.
        /// Implementations trigger this event based on their specific firing logic.
        /// </summary>
        event Action OnShouldFire;

        #endregion

        #region Methods

        /// <summary>
        /// Starts the firing sequence.
        /// Called when the trigger is pressed or firing is initiated.
        /// </summary>
        void StartFiring();

        /// <summary>
        /// Stops the firing sequence.
        /// Called when the trigger is released or firing should cease.
        /// </summary>
        void StopFiring();

        /// <summary>
        /// Updates the firing mechanism logic.
        /// Should be called every frame to process firing timing and trigger shots.
        /// </summary>
        /// <param name="deltaTime">The time elapsed since the last frame.</param>
        void UpdateFiring(float deltaTime);

        /// <summary>
        /// Resets the firing mechanism to its initial state.
        /// Called when weapon state changes (e.g., after reload, weapon swap, or interruption).
        /// </summary>
        void Reset();

        #endregion
    }
}
