using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// A struct that represents the desired movement changes from a single <see cref="IMovementAbility"/>.
    /// The <see cref="CoreMovement"/> controller processes abilities in priority order, accumulating their
    /// modifiers to calculate the final character movement for a frame.
    /// </summary>
    public struct MovementModifier
    {
        /// <summary>
        /// The horizontal (X and Z) velocity that the ability wants to apply.
        /// This is additive; the final horizontal velocity is the sum of ArealVelocity from all processed abilities.
        /// The Y component of this vector is typically ignored, as vertical velocity is handled separately.
        /// </summary>
        public Vector3 ArealVelocity;

        /// <summary>
        /// If true, the standard gravity calculation in <see cref="CoreMovement"/> will be skipped for this frame.
        /// This is a boolean flag; if any ability sets this to true, gravity is overridden.
        /// Useful for abilities like jumping, where gravity should not act against the initial upward force.
        /// </summary>
        public bool OverrideGravity;

        /// <summary>
        /// (Not currently used in the base Core kit, but available for extension)
        /// A nullable float that, if set, requests a change to the CharacterController's height.
        /// This can be used for abilities like crouching or sliding. The system would need to be
        /// designed to handle which ability's height request takes precedence.
        /// </summary>
        public float? ControllerHeight;
    }
}
