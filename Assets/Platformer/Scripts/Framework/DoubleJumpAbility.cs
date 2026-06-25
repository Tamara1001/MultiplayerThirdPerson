using UnityEngine;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Platformer
{
    /// <summary>
    /// An <see cref="IMovementAbility"/> that grants the character the ability to perform a second jump while in the air.
    /// This ability listens for a jump request when the character is airborne and has not yet performed a double jump.
    /// It resets automatically when the character lands on the ground.
    /// </summary>
    public class DoubleJumpAbility : MonoBehaviour, IMovementAbility
    {
        #region Fields & Properties

        [Tooltip("The height of the double jump, measured from the point the jump is initiated.")]
        [SerializeField] private float doubleJumpHeight = 2.0f;

        // Reference to the CoreMovement component for accessing movement state and control
        private CoreMovement m_Motor;

        // Tracks whether the player has already used their double jump while airborne
        private bool m_HasDoubleJumped;

        public int Priority => 15;

        public float StaminaCost => 10f;

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the ability with a reference to the <see cref="CoreMovement"/> component.
        /// Subscribes to the grounded state change event to reset the double jump when landing.
        /// </summary>
        /// <param name="motor">The CoreMovement component controlling the character.</param>
        public void Initialize(CoreMovement motor)
        {
            m_Motor = motor;
            m_Motor.OnGroundedStateChanged += OnGroundedStateChanged;
        }

        /// <summary>
        /// Processes the double jump ability each frame.
        /// Executes a double jump if the player requests a jump while airborne and hasn't already double jumped.
        /// </summary>
        /// <returns>A MovementModifier with gravity override enabled when performing a double jump, or an empty modifier otherwise.</returns>
        public MovementModifier Process()
        {
            if (m_Motor.JumpRequested && !m_Motor.IsGrounded && !m_HasDoubleJumped)
            {
                m_HasDoubleJumped = true;

                // Calculate required upward velocity using physics formula: v = sqrt(h * -2 * g)
                float jumpVelocity = Mathf.Sqrt(doubleJumpHeight * -2f * m_Motor.gravity);
                m_Motor.SetVerticalVelocity(jumpVelocity);

                // Override gravity for one frame to prevent immediate gravity application
                return new MovementModifier { OverrideGravity = true };
            }

            return new MovementModifier();
        }

        /// <summary>
        /// This ability does not support manual activation and always returns false.
        /// The double jump is automatically triggered during Process() when conditions are met.
        /// </summary>
        /// <returns>Always returns false.</returns>
        public bool TryActivate() => false;

        #endregion

        #region Unity Methods

        private void OnDestroy()
        {
            if (m_Motor != null)
            {
                m_Motor.OnGroundedStateChanged -= OnGroundedStateChanged;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Callback invoked when the character's grounded state changes.
        /// Resets the double jump flag when the character lands on the ground.
        /// </summary>
        /// <param name="isGrounded">True if the character is now grounded, false if airborne.</param>
        private void OnGroundedStateChanged(bool isGrounded)
        {
            if (isGrounded)
            {
                m_HasDoubleJumped = false;
            }
        }

        #endregion
    }
}
