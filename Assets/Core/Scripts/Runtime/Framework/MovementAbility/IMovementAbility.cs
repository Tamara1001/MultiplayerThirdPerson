namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Defines the contract for all movement abilities that can be attached to the <see cref="CoreMovement"/> controller.
    /// Each ability represents a specific movement action (e.g., walking, jumping, dashing) and contributes to the
    /// final character movement by returning a <see cref="MovementModifier"/> each frame.
    /// </summary>
    public interface IMovementAbility
    {
        /// <summary>
        /// Gets the execution priority of this ability. Abilities are processed in descending order of priority.
        /// Higher numbers are processed first, allowing them to potentially override the effects of lower-priority abilities.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Gets the amount of stamina required to activate or use this ability. This is typically used for special actions
        /// like dashing or double jumping, and is checked by a stats handler before activation.
        /// </summary>
        float StaminaCost { get; }

        /// <summary>
        /// Called by the <see cref="CoreMovement"/> controller when the ability is first added.
        /// This is used to provide the ability with a reference to its controlling motor.
        /// </summary>
        /// <param name="movementController">The <see cref="CoreMovement"/> instance that this ability will be a part of.</param>
        void Initialize(CoreMovement movementController);

        /// <summary>
        /// The core logic of the ability, executed every frame by <see cref="CoreMovement"/>.
        /// This method should evaluate the current state (e.g., input, character state) and return a
        /// <see cref="MovementModifier"/> struct that describes the velocity or state changes this ability wants to apply.
        /// </summary>
        /// <returns>A <see cref="MovementModifier"/> containing the desired movement changes for this frame.</returns>
        MovementModifier Process();

        /// <summary>
        /// Attempts to activate a discrete, one-off ability (like a dash or special jump).
        /// This is typically called by a manager in response to a specific input event, rather than being part of the continuous `Process` loop.
        /// </summary>
        /// <returns>True if the ability was successfully activated, false otherwise (e.g., if on cooldown or conditions are not met).</returns>
        bool TryActivate();
    }
}
