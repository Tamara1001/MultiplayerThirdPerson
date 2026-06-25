using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// An <see cref="IMovementAbility"/> that handles the character's fundamental ground-based locomotion,
    /// including walking and sprinting. It processes player input to determine the target speed and direction
    /// of movement relative to the character, camera, or world. This ability has the lowest priority,
    /// as it forms the base layer of movement that other abilities can modify or override.
    /// </summary>
    public class WalkAbility : MonoBehaviour, IMovementAbility
    {
        #region Fields & Properties

        private CoreMovement m_Motor;
        private float m_CurrentSpeed;
        private Vector3 m_CurrentVelocity;
        private bool m_WasSprinting;

        /// <summary>
        /// Gets the priority of the Walk ability. It is set to 0, the lowest priority, because it represents
        /// the most basic form of movement. All other abilities (like jumping or dashing) will be processed
        /// after this one, allowing them to modify or build upon the base walking velocity.
        /// </summary>
        public int Priority => 0;

        /// <summary>
        /// Gets the stamina cost per second while sprinting.
        /// </summary>
        public float StaminaCost => 3f;

        [Header("Air Control")]
        [Tooltip("How much influence input has in the air. 0 = no control, 1 = full control.")]
        [Range(0f, 1f)]
        [SerializeField] private float airControl = 0.5f;

        [Tooltip("How fast the character can turn in the air.")]
        [SerializeField] private float airRotationSpeed = 5f;

        [Tooltip("If true, the character keeps their momentum when jumping, especially from a sprint.")]
        [SerializeField] private bool conserveMomentum = true;

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
        /// Processes the walking and sprinting logic for the current frame.
        /// This method calculates the appropriate movement speed based on input and sprint state,
        /// smoothly interpolates to that speed, and determines the final movement direction.
        /// </summary>
        /// <returns>A <see cref="MovementModifier"/> containing the calculated horizontal velocity for this frame.</returns>
        public MovementModifier Process()
        {
            var modifier = new MovementModifier();

            // Determine if we should be using sprint speed
            // If we are grounded, we check the current sprint state
            // If we are in the air and conserving momentum, we use the state from when we left the ground
            bool useSprintSpeed = m_Motor.IsGrounded ? m_Motor.IsSprinting : (conserveMomentum && m_WasSprinting);
            
            // Determine the target speed based on whether the player is sprinting
            float targetSpeed = useSprintSpeed ? m_Motor.sprintSpeed : m_Motor.moveSpeed;

            // If grounded, update our sprint state memory
            if (m_Motor.IsGrounded)
            {
                m_WasSprinting = m_Motor.IsSprinting;
            }

            if (m_Motor.MoveInput == Vector2.zero)
            {
                targetSpeed = 0.0f;
            }

            // Smoothly interpolate the current speed towards the target speed
            float speedOffset = 0.1f;
            
            // Adjust acceleration/deceleration based on grounded state
            float acceleration = m_Motor.speedChangeRate;
            if (!m_Motor.IsGrounded)
            {
                // Reduce control authority in air based on airControl setting
                acceleration *= airControl;
            }

            if (m_CurrentSpeed < targetSpeed - speedOffset || m_CurrentSpeed > targetSpeed + speedOffset)
            {
                // Lerp the speed, scaling by InputMagnitude to handle analog stick input correctly
                m_CurrentSpeed = Mathf.Lerp(m_CurrentSpeed, targetSpeed * m_Motor.InputMagnitude,
                    Time.deltaTime * acceleration);

                // Round to three decimal places
                m_CurrentSpeed = Mathf.Round(m_CurrentSpeed * 1000f) / 1000f;
            }
            else
            {
                // Snap to target speed only if we have input or are grounded
                // This allows momentum to carry us a bit if we release input in air
                if (m_Motor.MoveInput != Vector2.zero || m_Motor.IsGrounded)
                {
                    m_CurrentSpeed = targetSpeed;
                }
            }

            // Convert 2D input to 3D direction
            Vector3 inputDirection = new Vector3(m_Motor.MoveInput.x, 0.0f, m_Motor.MoveInput.y).normalized;
            Vector3 targetDirection = Vector3.forward; // Default to forward if no input

            // Calculate the final movement direction
            if (inputDirection.sqrMagnitude > 0)
            {
                switch (m_Motor.directionMode)
                {
                    case CoreMovement.MovementDirectionMode.CharacterRelative:
                        targetDirection = m_Motor.transform.rotation * inputDirection;
                        break;
                    case CoreMovement.MovementDirectionMode.CameraRelative:
                        targetDirection = Quaternion.Euler(0.0f, m_Motor.TargetRotationY, 0.0f) * inputDirection;
                        break;
                    case CoreMovement.MovementDirectionMode.World:
                    default:
                        targetDirection = inputDirection;
                        break;
                }
                
                // Store last valid input direction for situations where we might want to continue in that direction
                // (Though m_CurrentVelocity helps with that too)
            }
            else if (m_CurrentVelocity.sqrMagnitude > 0.1f)
            {
                 // If no input, direction is our current velocity direction
                 targetDirection = m_CurrentVelocity.normalized;
            }

            // Calculate target velocity
            Vector3 targetVelocity = targetDirection * m_CurrentSpeed;

            // Apply smoothing to the velocity vector itself for better air control feel
            // If grounded, we track perfectly (handled by m_CurrentSpeed lerp above efficiently enough, but let's be consistent)
            // Actually, separating speed and direction logic as before is fine, but for air control we want to steer the vector.
            
            if (m_Motor.IsGrounded)
            {
                // On ground, instant direction changes are usually preferred for responsiveness
                // The speed lerp handles the acceleration
                 m_CurrentVelocity = targetDirection.normalized * m_CurrentSpeed;
            }
            else
            {
                // In air, we interpolate velocity vector to allow steering
                if (m_Motor.MoveInput != Vector2.zero)
                {
                     // Steer towards target direction
                     // We use airRotationSpeed to interpolate the direction
                     m_CurrentVelocity = Vector3.Lerp(m_CurrentVelocity, targetDirection.normalized * m_CurrentSpeed, Time.deltaTime * airRotationSpeed);
                }
                else
                {
                    // No input in air - preserve momentum but let drag/gravity do its thing (handled by CoreMovement via speed updates here)
                    // We just update the speed part of the velocity
                    if (m_CurrentVelocity.sqrMagnitude > 0)
                    {
                        m_CurrentVelocity = m_CurrentVelocity.normalized * m_CurrentSpeed;
                    }
                }
            }

            // Apply the calculated speed to the movement direction to get the final velocity
            modifier.ArealVelocity = m_CurrentVelocity;

            return modifier;
        }

        /// <summary>
        /// Attempts to activate the ability. Walking is a continuous state that processes every frame,
        /// not a discrete, activatable ability like jumping or dashing.
        /// </summary>
        /// <returns>Always returns false as this ability cannot be manually activated.</returns>
        public bool TryActivate() => false;

        #endregion
    }
}
