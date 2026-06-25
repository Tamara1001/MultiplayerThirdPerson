using System;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Handles player movement, including walking, sprinting, jumping, and gravity.
    /// This component is built upon a CharacterController and uses a system of <see cref="IMovementAbility"/>
    /// to modularly handle different movement actions like walking, jumping and dashing.
    /// It is responsible for processing inputs, applying movement and rotation, and syncing state over the network.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CoreMovement : NetworkTransform
    {
        #region Enums

        /// <summary>
        /// Defines how the player's rotation is coupled with the camera's rotation.
        /// </summary>
        public enum CouplingMode
        {
            /// <summary>
            /// Player rotation is always coupled with the camera's horizontal rotation.
            /// </summary>
            Coupled,
            /// <summary>
            /// Player rotation is coupled with the camera only when there is movement input.
            /// </summary>
            CoupledWhenMoving,
            /// <summary>
            /// Player rotation is independent of the camera; the player faces the direction of movement.
            /// </summary>
            Decoupled
        }

        /// <summary>
        /// Defines the directional space for movement input.
        /// </summary>
        public enum MovementDirectionMode
        {
            /// <summary>
            /// Movement is relative to the world axes (e.g., top-down).
            /// </summary>
            World,
            /// <summary>
            /// Movement is relative to the character's forward direction.
            /// </summary>
            CharacterRelative,
            /// <summary>
            /// Movement is relative to the camera's forward direction.
            /// </summary>
            CameraRelative
        }

        #endregion

        #region Fields & Properties

        [Header("Movement Settings")]
        [Tooltip("Determines the directional context for movement input.")]
        public MovementDirectionMode directionMode = MovementDirectionMode.CameraRelative;
        [Tooltip("Base movement speed.")]
        public float moveSpeed = 4.0f;
        [Tooltip("Movement speed when sprinting.")]
        public float sprintSpeed = 6.0f;
        [Tooltip("How quickly the character rotates to the target direction. Lower values are faster.")]
        public float rotationSmoothTime = 0.1f;
        [Tooltip("The rate of acceleration and deceleration.")]
        public float speedChangeRate = 10.0f;
        [Tooltip("How quickly external forces decay. Higher values mean faster decay.")]
        public float forceDecayRate = 5f;

        [Header("Gravity and Jump Settings")]
        [Tooltip("The force of gravity applied to the character.")]
        public float gravity = -15.0f;
        [Tooltip("The height the character can jump.")]
        public float jumpHeight = 1.2f;

        [Header("Ground Check")]
        [Tooltip("Vertical offset for the ground check sphere.")]
        [SerializeField] private float groundedOffset = -0.14f;
        [Tooltip("Radius of the ground check sphere.")]
        [SerializeField] private float groundedRadius = 0.28f;
        [Tooltip("Layers considered as ground.")]
        public LayerMask groundLayers;

        [Header("Slope Handling")]
        [Tooltip("Extra downward force applied when on slopes to prevent bouncing.")]
        [SerializeField] private float slopeForce = 5f;
        [Tooltip("Multiplier for raycast length when detecting slopes.")]
        [SerializeField] private float slopeForceRayLength = 1.5f;

        [Header("Movement Control")]
        [Tooltip("Enables or disables the application of movement from abilities and gravity. When disabled, the character will not move.")]
        [SerializeField] private bool isMovementEnabled = true;
        [Tooltip("Optional transform to apply rotation to. If null, rotation is applied to this transform. Useful for root motion animations.")]
        [SerializeField] private Transform rotationTransform;

        /// <summary>
        /// Allows external scripts to override the character's rotation logic.
        /// If set, this function will be used to determine the target rotation.
        /// </summary>
        public Func<Quaternion> RotationOverride { get; set; }

        /// <summary>
        /// Allows an external script to completely override the final movement vector calculation.
        /// The input is the movement vector (including delta time) calculated by CoreMovement's abilities and gravity.
        /// The output should be the final vector to pass to CharacterController.Move().
        /// If set, this function is responsible for ALL final motion calculations, including platform inertia.
        /// </summary>
        public Func<Vector3, Vector3> FinalMoveCalculationOverride { get; set; }

        /// <summary>
        /// Gets a value indicating whether the character is currently on the ground.
        /// </summary>
        public bool IsGrounded { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the character is currently on a slope.
        /// </summary>
        public bool IsOnSlope { get; private set; }

        /// <summary>
        /// Gets the time when the character was last on the ground. Useful for implementing jump grace periods (coyote time).
        /// </summary>
        public float TimeLastGrounded => m_TimeLastGrounded;

        /// <summary>
        /// Gets the time elapsed since the character last landed on the ground.
        /// </summary>
        public float TimeSinceLanded => m_TimeSinceLanded;

        /// <summary>
        /// Gets the character's current vertical velocity.
        /// </summary>
        public float VerticalVelocity => m_VerticalVelocity;

        /// <summary>
        /// Gets the character's current horizontal speed.
        /// </summary>
        public float CurrentSpeed => new Vector3(m_ArealVelocity.x, 0.0f, m_ArealVelocity.z).magnitude;

        /// <summary>
        /// Gets the magnitude of the movement input (0 for no input, 1 for full input).
        /// </summary>
        public float InputMagnitude => MoveInput.magnitude;

        /// <summary>
        /// Gets the raw movement input vector.
        /// </summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the character is currently sprinting.
        /// </summary>
        public bool IsSprinting { get; private set; }

        /// <summary>
        /// Gets the target rotation on the Y-axis, usually driven by the camera.
        /// </summary>
        public float TargetRotationY { get; private set; }

        /// <summary>
        /// Gets a value indicating whether a jump has been requested this frame.
        /// </summary>
        public bool JumpRequested { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether movement is enabled. If false, the CharacterController.Move method will not be called.
        /// </summary>
        public bool IsMovementEnabled { get => isMovementEnabled; set => isMovementEnabled = value; }

        /// <summary>
        /// True if a variable-intensity action was released this frame.
        /// Reset automatically at the end of each frame in LateUpdate.
        /// </summary>
        public bool VariableActionReleasedThisFrame { get; private set; }

        /// <summary>
        /// Gets or sets the player's rotation coupling mode.
        /// </summary>
        public CouplingMode PlayerRotationMode { get; set; } = CouplingMode.Decoupled;

        /// <summary>
        /// Gets the last non-zero horizontal movement direction.
        /// </summary>
        public Vector3 LastMoveDirection { get; private set; }

        /// <summary>
        /// Gets the rotation transform.
        /// </summary>
        public Transform RotationTransform => rotationTransform;

        private CharacterController m_CharacterController;
        private List<IMovementAbility> m_Abilities = new List<IMovementAbility>();
        private float m_VerticalVelocity;
        private float m_RotationVelocity;
        private Vector3 m_ArealVelocity;
        private readonly float m_TerminalVelocity = 53.0f;
        private Vector3 m_ExternalForce;
        private float m_TimeLastGrounded;
        private float m_TimeSinceLanded;
        private float m_InitialGravity;

        #endregion

        #region Events

        /// <summary>
        /// Event invoked when the character lands on the ground.
        /// The Vector3 payload contains the vertical velocity at the moment of landing.
        /// </summary>
        public event Action<Vector3> OnLanded;

        /// <summary>
        /// Event invoked when the character's grounded state changes.
        /// The bool payload is true if grounded, false otherwise.
        /// </summary>
        public event Action<bool> OnGroundedStateChanged;

        #endregion

        #region Unity Methods

        protected override void Awake()
        {
            base.Awake();
            m_CharacterController = GetComponent<CharacterController>();
            m_InitialGravity = gravity;

            // Discover and initialize all movement abilities
            m_Abilities = new List<IMovementAbility>(GetComponents<IMovementAbility>());

            foreach (var ability in m_Abilities)
            {
                ability.Initialize(this);
            }

            // Sort abilities by priority to ensure correct processing order
            m_Abilities.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        private void Update()
        {
            if (!IsOwner) return;

            // Update landing timer when grounded
            if (IsGrounded)
            {
                m_TimeSinceLanded += Time.deltaTime;
            }

            if (!isMovementEnabled)
            {
                // If movement is disabled, we still want to apply gravity and ground checks to prevent floating.
                GroundedCheck();
                if (m_VerticalVelocity < m_TerminalVelocity)
                {
                    m_VerticalVelocity += gravity * Time.deltaTime;
                }
                if (m_CharacterController.enabled)
                {
                    Vector3 verticalMove = new Vector3(0, m_VerticalVelocity, 0) * Time.deltaTime;
                    if (!float.IsNaN(verticalMove.x) && !float.IsNaN(verticalMove.y) && !float.IsNaN(verticalMove.z))
                    {
                        m_CharacterController.Move(verticalMove);
                    }
                }
                return;
            }

            GroundedCheck();
            ProcessAbilities();
        }

        private void LateUpdate()
        {
            if (!IsOwner) return;

            // Reset jump request at the end of the frame
            JumpRequested = false;
            VariableActionReleasedThisFrame = false;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the movement input for the character.
        /// </summary>
        /// <param name="direction">The 2D movement input vector.</param>
        public void SetMoveInput(Vector2 direction) => MoveInput = direction;

        /// <summary>
        /// Sets the sprinting state of the character.
        /// </summary>
        /// <param name="isSprinting">True to enable sprinting, false otherwise.</param>
        public void SetSprintState(bool isSprinting) => IsSprinting = isSprinting;

        /// <summary>
        /// Sets the target rotation for the character, typically from the camera's horizontal angle.
        /// </summary>
        /// <param name="yAngle">The target Y-axis rotation angle.</param>
        public void SetTargetRotation(float yAngle) => TargetRotationY = yAngle;

        /// <summary>
        /// Flags that a jump should be performed on the next ability process.
        /// </summary>
        public void PerformJump()
        {
            JumpRequested = true;
        }

        /// <summary>
        /// Directly sets the character's vertical velocity.
        /// </summary>
        /// <param name="newVerticalVelocity">The new vertical velocity value.</param>
        public void SetVerticalVelocity(float newVerticalVelocity)
        {
            m_VerticalVelocity = newVerticalVelocity;
        }

        /// <summary>
        /// Applies an external, instantaneous force to the character. The force will decay over time based on forceDecayRate.
        /// This should only be called on the owner's instance.
        /// </summary>
        /// <param name="force">The force vector to apply.</param>
        /// <param name="forceMode">The type of force. Only Impulse is currently supported for additive force.</param>
        public void ApplyExternalForce(Vector3 force, ForceMode forceMode)
        {
            if (!IsOwner) return;

            if (forceMode == ForceMode.Impulse)
            {
                m_ExternalForce += force;
            }
            else // Other modes like ForceMode.Force can be handled here if needed.
            {
                m_ExternalForce += force * Time.deltaTime;
            }
        }

        /// <summary>
        /// Sets the character's position, disabling and re-enabling the CharacterController to avoid issues.
        /// </summary>
        /// <param name="position">The target position.</param>
        /// <param name="teleport">If true, uses NetworkTransform's Teleport method for network synchronization.</param>
        public void SetPosition(Vector3 position, bool teleport = true)
        {
            if (m_CharacterController != null && m_CharacterController.enabled)
            {
                m_CharacterController.enabled = false;
                transform.position = position;
                m_CharacterController.enabled = true;
            }
            else
            {
                transform.position = position;
            }

            if (CanCommitToTransform && teleport)
            {
                Teleport(position, transform.rotation, transform.localScale);
            }
        }

        /// <summary>
        /// Adds a new movement ability to the character at runtime.
        /// The ability is initialized and the processing order is updated based on priority.
        /// </summary>
        /// <param name="newAbility">The instance of the ability script (which must be an IMovementAbility and MonoBehaviour) to add.</param>
        public void AddAbility(IMovementAbility newAbility)
        {
            if (newAbility == null || m_Abilities.Contains(newAbility))
            {
                return;
            }

            m_Abilities.Add(newAbility);
            newAbility.Initialize(this);
            // Re-sort the list to maintain priority order
            m_Abilities.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        /// <summary>
        /// Checks if the character currently has a specific movement ability.
        /// </summary>
        /// <typeparam name="T">The type of the ability to check for.</typeparam>
        /// <returns>True if an ability of the specified type is active, false otherwise.</returns>
        public bool HasAbility<T>() where T : class, IMovementAbility
        {
            for (int i = 0; i < m_Abilities.Count; i++)
            {
                if (m_Abilities[i] is T)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Removes a movement ability from the character at runtime and destroys its component.
        /// </summary>
        /// <typeparam name="T">The type of the ability to remove.</typeparam>
        /// <returns>True if the ability was found and removed, false otherwise.</returns>
        public bool RemoveAbility<T>() where T : class, IMovementAbility
        {
            IMovementAbility abilityToRemove = null;
            for (int i = 0; i < m_Abilities.Count; i++)
            {
                if (m_Abilities[i] is T)
                {
                    abilityToRemove = m_Abilities[i];
                    break;
                }
            }

            if (abilityToRemove != null)
            {
                m_Abilities.Remove(abilityToRemove);
                // The ability is a MonoBehaviour component attached to this GameObject.
                // Destroy the component to trigger its OnDestroy for cleanup.
                if (abilityToRemove is MonoBehaviour component)
                {
                    Destroy(component);
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to activate a specific movement ability by type.
        /// </summary>
        /// <typeparam name="T">The type of the movement ability to activate.</typeparam>
        /// <returns>True if the ability was found and successfully activated, false otherwise.</returns>
        public bool TryActivateAbility<T>() where T : class, IMovementAbility
        {
            foreach (var ability in m_Abilities)
            {
                if (ability is T typedAbility)
                {
                    return typedAbility.TryActivate();
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the stamina cost of a specific movement ability.
        /// </summary>
        /// <typeparam name="T">The type of the movement ability.</typeparam>
        /// <returns>The stamina cost of the ability, or 0 if not found.</returns>
        public float GetAbilityStaminaCost<T>() where T : class, IMovementAbility
        {
            foreach (var ability in m_Abilities)
            {
                if (ability is T typedAbility)
                {
                    return typedAbility.StaminaCost;
                }
            }
            return 0f;
        }

        /// <summary>
        /// Sets the character's parent to a new NetworkObject, handling network synchronization.
        /// Pass null to remove the current parent.
        /// </summary>
        /// <param name="newParent">The NetworkObject to parent to, or null to clear the parent.</param>
        public void SetParent(NetworkObject newParent)
        {
            // Only the owner can change parenting.
            // CanCommitToTransform is the correct check as it's true on the authority.
            if (!CanCommitToTransform)
            {
                return;
            }

            if (newParent != null)
            {
                Debug.Log("Setting parent to: " + newParent.name, this);
                NetworkObject.TrySetParent(newParent);
                isMovementEnabled = false;
            }
            else
            {
                NetworkObject.TryRemoveParent();
            }
        }

        /// <summary>
        /// Sets the movement direction mode. Should only be called by the owner.
        /// </summary>
        public void SetDirectionMode(MovementDirectionMode mode)
        {
            if (!IsOwner) return;
            directionMode = mode;
        }

        /// <summary>
        /// Called when a variable-intensity action (like jump, charge attack, etc.) is released.
        /// Used by abilities that modify their effect based on hold duration.
        /// </summary>
        public void OnVariableActionReleased()
        {
            VariableActionReleasedThisFrame = true;
        }

        /// <summary>
        /// Sets the transform to apply rotation to. Useful for root motion animations where rotation should be applied to the animator's transform.
        /// </summary>
        /// <param name="targetTransform">The transform to apply rotation to. Pass null to use this transform.</param>
        public void SetRotationTransform(Transform targetTransform)
        {
            rotationTransform = targetTransform;
        }

        /// <summary>
        /// Resets all movement forces (vertical velocity, external forces, areal velocity) and restores gravity to its initial value.
        /// Useful when respawning a player to ensure clean state.
        /// </summary>
        public void ResetMovementForces()
        {
            m_VerticalVelocity = 0f;
            m_ExternalForce = Vector3.zero;
            m_ArealVelocity = Vector3.zero;
            gravity = m_InitialGravity;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Processes all movement abilities and applies their resulting modifiers to the CharacterController.
        /// </summary>
        private void ProcessAbilities()
        {
            var finalModifier = new MovementModifier();

            // Accumulate movement modifications from all abilities
            foreach (var ability in m_Abilities)
            {
                var modifier = ability.Process();
                finalModifier.ArealVelocity += modifier.ArealVelocity;
                finalModifier.OverrideGravity |= modifier.OverrideGravity;
            }

            if (m_ExternalForce.magnitude > 0.01f)
            {
                finalModifier.ArealVelocity += m_ExternalForce;
                m_ExternalForce = Vector3.Lerp(m_ExternalForce, Vector3.zero, forceDecayRate * Time.deltaTime);
            }
            else
            {
                m_ExternalForce = Vector3.zero;
            }

            m_ArealVelocity = finalModifier.ArealVelocity;

            // Store the last known movement direction
            var horizontalVelocity = new Vector3(-m_ArealVelocity.x, 0, -m_ArealVelocity.z);
            if (horizontalVelocity.sqrMagnitude > 0.01f)
            {
                LastMoveDirection = horizontalVelocity.normalized;
            }

            // Apply gravity if not overridden by an ability
            if (IsGrounded && !finalModifier.OverrideGravity)
            {
                // Apply a small downward force to keep the character grounded
                if (m_VerticalVelocity < 0.0f)
                {
                    m_VerticalVelocity = -2f;
                }
            }

            if (!finalModifier.OverrideGravity)
            {
                // Accelerate due to gravity, respecting terminal velocity
                if (m_VerticalVelocity < m_TerminalVelocity)
                {
                    m_VerticalVelocity += gravity * Time.deltaTime;
                }
            }

            // Apply rotation based on coupling mode and movement
            ApplyRotation(finalModifier.ArealVelocity);

            // Combine horizontal and vertical movement and apply it
            Vector3 movement = finalModifier.ArealVelocity;
            movement.y = m_VerticalVelocity;

            // Apply extra downward force on slopes when moving
            IsOnSlope = OnSlope();
            if (IsOnSlope && (MoveInput.x != 0 || MoveInput.y != 0))
            {
                movement += Vector3.down * slopeForce;
            }

            Vector3 finalMovementVector = movement * Time.deltaTime;

            // If an override is set, use it to determine the final movement.
            // Otherwise, use the vector calculated by this component.
            if (FinalMoveCalculationOverride != null)
            {
                finalMovementVector = FinalMoveCalculationOverride(finalMovementVector);
            }

            if (m_CharacterController != null && m_CharacterController.enabled)
            {
                // Prevent NaN values from breaking transform
                if (!float.IsNaN(finalMovementVector.x) && !float.IsNaN(finalMovementVector.y) && !float.IsNaN(finalMovementVector.z))
                {
                    m_CharacterController.Move(finalMovementVector);
                }
            }
        }

        /// <summary>
        /// Calculates and applies the character's rotation for the current frame.
        /// </summary>
        /// <param name="horizontalVelocity">The final horizontal velocity from abilities.</param>
        private void ApplyRotation(Vector3 horizontalVelocity)
        {
            Transform targetTransform = rotationTransform != null ?
                rotationTransform : transform;

            if (RotationOverride != null)
            {
                targetTransform.rotation = RotationOverride();
                return;
            }

            bool isCoupled = PlayerRotationMode == CouplingMode.Coupled ||
                (PlayerRotationMode == CouplingMode.CoupledWhenMoving && MoveInput != Vector2.zero);

            if (isCoupled)
            {
                // Rotate towards the camera's target direction
                float rotation = Mathf.SmoothDampAngle(targetTransform.eulerAngles.y, TargetRotationY, ref m_RotationVelocity, rotationSmoothTime);
                targetTransform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }
            else if (horizontalVelocity.magnitude > 0.1f)
            {
                // Rotate towards the direction of movement
                float targetAngle = Mathf.Atan2(horizontalVelocity.x, horizontalVelocity.z) * Mathf.Rad2Deg;
                float rotation = Mathf.SmoothDampAngle(targetTransform.eulerAngles.y, targetAngle, ref m_RotationVelocity, rotationSmoothTime);
                targetTransform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }
        }

        /// <summary>
        /// Checks if the character is on the ground and invokes related events.
        /// </summary>
        private void GroundedCheck()
        {
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y + groundedOffset, transform.position.z);
            bool wasGrounded = IsGrounded;
            IsGrounded = Physics.CheckSphere(spherePosition, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);

            if (IsGrounded)
            {
                m_TimeLastGrounded = Time.time;
            }

            if (IsGrounded != wasGrounded)
            {
                OnGroundedStateChanged?.Invoke(IsGrounded);

                // If we just landed after falling
                if (IsGrounded && m_VerticalVelocity < -2.0f)
                {
                    // Reset the landing timer
                    m_TimeSinceLanded = 0f;
                    OnLanded?.Invoke(new Vector3(0, m_VerticalVelocity, 0));
                }
            }
        }

        /// <summary>
        /// Checks if the character is currently on a slope by raycasting downward.
        /// </summary>
        /// <returns>True if on a slope, false otherwise.</returns>
        private bool OnSlope()
        {
            if (JumpRequested || m_VerticalVelocity > 0)
                return false;

            RaycastHit hit;
            Vector3 rayStart = transform.position;
            float rayLength = m_CharacterController.height / 2 * slopeForceRayLength;

            if (Physics.Raycast(rayStart, Vector3.down, out hit, rayLength, groundLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.normal != Vector3.up)
                    return true;
            }
            return false;
        }

        #endregion
    }
}
