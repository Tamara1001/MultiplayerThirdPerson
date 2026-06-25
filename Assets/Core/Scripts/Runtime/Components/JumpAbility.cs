using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// An <see cref="IMovementAbility"/> that provides the character with the ability to jump.
    /// This ability checks for a jump request from the <see cref="CoreMovement"/> controller, and if the character
    /// is grounded, it applies an instantaneous upward velocity. It has a high priority to ensure the jump
    /// action can override other ground-based movements for a single frame.
    /// </summary>
    public class JumpAbility : MonoBehaviour, IMovementAbility
    {
        #region Fields & Properties

        /// <summary>
        /// Gets the priority of the Jump ability. It's set to 10 to ensure it is processed after the basic
        /// WalkAbility (Priority 0) but before more specialized or overriding abilities. This allows the jump
        /// to apply its vertical velocity cleanly.
        /// </summary>
        public int Priority => 10;

        /// <summary>
        /// Gets the stamina cost required to perform a jump.
        /// </summary>
        public float StaminaCost => 10f;

        [Header("Jump Settings")]
        [Tooltip("Time in seconds after landing before the player can jump again.")]
        [SerializeField] private float landingCooldown = 0.5f;

        // Reference to the CoreMovement controller that owns this ability
        private CoreMovement m_Motor;

        // Cooldown timer to prevent jump spamming and erratic behavior
        private float m_JumpCooldown;

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the ability by caching a reference to the CoreMovement controller.
        /// </summary>
        /// <param name="motor">The CoreMovement instance that will manage this ability.</param>
        public void Initialize(CoreMovement motor)
        {
            m_Motor = motor;
        }

        /// <summary>
        /// Processes the jump logic for the current frame.
        /// Checks for jump requests and applies upward velocity if all conditions are met.
        /// </summary>
        /// <returns>A <see cref="MovementModifier"/>. If a jump is performed, it returns a modifier that overrides
        /// gravity for that frame to ensure the full jump force is applied. Otherwise, it returns an empty modifier.</returns>
        public MovementModifier Process()
        {
            // Decrement the cooldown timer each frame
            if (m_JumpCooldown > 0)
            {
                m_JumpCooldown -= Time.deltaTime;
            }

            // Check if we can jump (grounded and passed landing cooldown)
            bool canJump = (m_Motor.IsGrounded || Time.time < m_Motor.TimeLastGrounded)
                           && m_Motor.TimeSinceLanded >= landingCooldown;

            // Check conditions for performing a jump:
            // 1. A jump has been requested via CoreMovement
            // 2. The character is grounded and landing cooldown has expired
            // 3. The jump cooldown has expired
            if (m_Motor.JumpRequested && canJump && m_JumpCooldown <= 0)
            {
                // Set the cooldown to prevent immediate re-jumping
                m_JumpCooldown = 0.5f;

                // Calculate the required upward velocity to reach the desired jump height
                // Using the kinematic equation: v^2 = u^2 + 2as, simplified to v = sqrt(-2gs)
                float jumpVelocity = Mathf.Sqrt(m_Motor.jumpHeight * -2f * m_Motor.gravity);

                // Apply the jump velocity
                m_Motor.SetVerticalVelocity(jumpVelocity);

                // Override gravity for this frame to prevent it from immediately reducing the applied jump force
                // This ensures the character leaves the ground with the full calculated velocity
                return new MovementModifier { OverrideGravity = true };
            }

            // No jump performed, return empty modifier
            return new MovementModifier();
        }

        /// <summary>
        /// Jump is not an activatable ability in the traditional sense; it's a reactive part of the
        /// continuous movement processing loop handled by <see cref="Process"/>.
        /// </summary>
        /// <returns>Always false.</returns>
        public bool TryActivate() => false;

        #endregion
    }
}
