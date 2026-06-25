using UnityEngine;
using Unity.Cinemachine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// A Cinemachine extension that adds dynamic camera feel effects for platformer movement.
    /// Features include speed-based FOV adjustments, landing impacts, and jump arc camera responses.
    /// Subscribes to <see cref="GameEvent"/> for jump arc start/end notifications and reads movement
    /// data from <see cref="CoreMovement"/> to create responsive camera behavior.
    /// </summary>
    [SaveDuringPlay]
    public class MovementCameraFeelExtension : CinemachineExtension
    {
        #region Fields & Properties

        [Header("Movement Reference")]
        [Tooltip("The CoreMovement component to read movement data from. Auto-detected if null.")]
        [SerializeField] private CoreMovement movementComponent;

        [Header("Speed-Based FOV")]
        [Tooltip("Enable/disable speed-based FOV adjustment")]
        [SerializeField] private bool enableSpeedFOV = true;
        [Tooltip("Base FOV when stationary")]
        [SerializeField] private float baseFOV = 60f;
        [Tooltip("Maximum FOV increase at high speed")]
        [SerializeField] private float maxFOVIncrease = 15f;
        [Tooltip("Speed threshold for maximum FOV")]
        [SerializeField] private float maxFOVSpeedThreshold = 8f;
        [Tooltip("Curve for FOV increase based on normalized speed [0-1]")]
        [SerializeField] private AnimationCurve fovCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("FOV interpolation speed")]
        [SerializeField] private float fovSmoothSpeed = 5f;

        [Header("Landing Impact")]
        [Tooltip("Enable/disable landing impact effect")]
        [SerializeField] private bool enableLandingImpact = true;
        [Tooltip("Maximum downward camera offset on landing")]
        [SerializeField] private float landingOffsetAmount = 0.15f;
        [Tooltip("Landing impact recovery speed")]
        [SerializeField] private float landingRecoverySpeed = 8f;
        [Tooltip("Minimum vertical velocity to trigger landing impact")]
        [SerializeField] private float landingVelocityThreshold = -3f;

        [Header("Jump Arc Response")]
        [Tooltip("Game event for jump arc start (charge-up phase)")]
        [SerializeField] private GameEvent jumpArcStartedEvent;
        [Tooltip("Game event for jump arc end (release/landing phase)")]
        [SerializeField] private GameEvent jumpArcEndedEvent;
        [Tooltip("FOV decrease during charge-up")]
        [SerializeField] private float chargeUpFOVDecrease = 8f;
        [Tooltip("Camera vertical offset during charge-up (negative = down)")]
        [SerializeField] private float chargeUpVerticalOffset = -0.2f;
        [Tooltip("Camera forward offset during charge-up (negative = back)")]
        [SerializeField] private float chargeUpForwardOffset = -0.3f;
        [Tooltip("Charge-up transition speed")]
        [SerializeField] private float chargeUpSpeed = 3f;
        [Tooltip("FOV burst increase on jump release")]
        [SerializeField] private float releaseFOVBurst = 12f;
        [Tooltip("Camera vertical kick on release")]
        [SerializeField] private float releaseVerticalKick = 0.4f;
        [Tooltip("Release effect decay speed")]
        [SerializeField] private float releaseDecaySpeed = 6f;

        private bool m_WasGrounded;
        private float m_OriginalFOV;
        private float m_CurrentFOV;
        private float m_TargetFOV;
        private bool m_FOVInitialized;
        private float m_LandingOffset;
        private bool m_IsLanding;
        private float m_JumpTime;
        private bool m_IsChargingJump;
        private float m_ChargeUpProgress;
        private float m_ReleaseKickOffset;
        private float m_ReleaseFOVBoost;
        private float m_ReleaseTransitionProgress;
        private CoreMovement m_Movement;

        #endregion

        #region Unity Methods

        protected override void Awake()
        {
            base.Awake();

            m_FOVInitialized = false;
            m_LandingOffset = 0f;
            m_ChargeUpProgress = 0f;
            m_ReleaseKickOffset = 0f;
            m_ReleaseFOVBoost = 0f;
            m_ReleaseTransitionProgress = 0f;
        }

        protected override void OnEnable()
        {
            if (jumpArcStartedEvent != null)
            {
                jumpArcStartedEvent.RegisterListener(OnJumpArcStarted);
            }

            if (jumpArcEndedEvent != null)
            {
                jumpArcEndedEvent.RegisterListener(OnJumpArcEnded);
            }
        }

        private void OnDisable()
        {
            if (jumpArcStartedEvent != null)
            {
                jumpArcStartedEvent.UnregisterListener(OnJumpArcStarted);
            }

            if (jumpArcEndedEvent != null)
            {
                jumpArcEndedEvent.UnregisterListener(OnJumpArcEnded);
            }
        }

        private void Start()
        {
            TryInitializeMovementReference();
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Cinemachine callback invoked during the camera pipeline processing.
        /// Applies FOV effects during the Aim stage and position effects during the Body stage.
        /// </summary>
        /// <param name="vcam">The virtual camera being processed.</param>
        /// <param name="stage">The current pipeline stage.</param>
        /// <param name="state">The camera state to modify.</param>
        /// <param name="deltaTime">Time elapsed since last frame.</param>
        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            if (m_Movement == null)
            {
                TryInitializeMovementReference();
            }

            if (m_Movement == null)
            {
                return;
            }

            if (stage == CinemachineCore.Stage.Body)
            {
                UpdateMovementState();
                UpdateJumpArcEffects(deltaTime);
                ApplyLandingImpact(ref state, deltaTime);
                ApplyJumpArcPositionEffects(ref state, deltaTime);

                m_WasGrounded = m_Movement.IsGrounded;
            }
            else if (stage == CinemachineCore.Stage.Aim)
            {
                ApplyFOVEffects(ref state, deltaTime);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Attempts to find and cache the <see cref="CoreMovement"/> component reference.
        /// Auto-detects from the virtual camera's LookAt target if not manually assigned.
        /// </summary>
        private void TryInitializeMovementReference()
        {
            if (m_Movement != null)
            {
                return;
            }

            if (movementComponent == null)
            {
                var vcam = GetComponent<CinemachineCamera>();
                if (vcam != null && vcam.LookAt != null)
                {
                    movementComponent = vcam.LookAt.GetComponent<CoreMovement>();
                }

                if (movementComponent == null)
                {
                    movementComponent = vcam.LookAt.GetComponentInParent<CoreMovement>();
                }
            }

            if (movementComponent == null)
            {
                return;
            }

            m_Movement = movementComponent;
        }

        /// <summary>
        /// Updates movement-related state tracking including landing detection.
        /// Detects transitions from airborne to grounded and triggers landing effects.
        /// </summary>
        private void UpdateMovementState()
        {
            bool isCurrentlyGrounded = m_Movement.IsGrounded;
            float currentVerticalVelocity = m_Movement.VerticalVelocity;

            // Trigger landing when transitioning from airborne to grounded with sufficient downward velocity
            if (enableLandingImpact && !m_WasGrounded && isCurrentlyGrounded && currentVerticalVelocity < landingVelocityThreshold)
            {
                m_IsLanding = true;
                m_LandingOffset = -landingOffsetAmount;
            }

            // End landing state when offset has nearly recovered to zero
            if (m_IsLanding && isCurrentlyGrounded && m_LandingOffset >= -0.01f)
            {
                m_IsLanding = false;
            }
        }

        /// <summary>
        /// Applies dynamic field of view adjustments based on movement speed and jump arc state.
        /// Combines speed-based FOV increase with charge-up decrease and release burst for layered effects.
        /// </summary>
        /// <param name="state">The camera state to modify.</param>
        /// <param name="deltaTime">Time elapsed since last frame.</param>
        private void ApplyFOVEffects(ref CameraState state, float deltaTime)
        {
            if (!m_FOVInitialized)
            {
                m_OriginalFOV = state.Lens.FieldOfView > 0f ? state.Lens.FieldOfView : baseFOV;
                m_CurrentFOV = m_OriginalFOV;
                m_TargetFOV = m_OriginalFOV;
                m_FOVInitialized = true;
            }

            // Calculate base FOV from movement speed using curve evaluation
            float targetFovFromSpeed = m_OriginalFOV;
            if (enableSpeedFOV && m_Movement != null)
            {
                float speed = m_Movement.CurrentSpeed;
                float normalizedSpeed = Mathf.Clamp01(speed / Mathf.Max(1f, maxFOVSpeedThreshold));
                float fovIncrease = fovCurve.Evaluate(normalizedSpeed) * maxFOVIncrease;
                targetFovFromSpeed += fovIncrease;
            }

            // Layer jump arc FOV adjustments on top of speed-based FOV
            float fovAdjustment = 0f;

            // Charge-up narrows FOV to focus anticipation
            if (m_ChargeUpProgress > 0f)
            {
                fovAdjustment -= chargeUpFOVDecrease * m_ChargeUpProgress;
            }

            // Release widens FOV for dramatic burst effect
            if (m_ReleaseFOVBoost > 0f)
            {
                fovAdjustment += m_ReleaseFOVBoost;
            }

            // Combine all FOV components and smoothly interpolate
            m_TargetFOV = targetFovFromSpeed + fovAdjustment;
            m_CurrentFOV = Mathf.Lerp(m_CurrentFOV, m_TargetFOV, deltaTime * fovSmoothSpeed);

            state.Lens.FieldOfView = m_CurrentFOV;
        }

        /// <summary>
        /// Applies downward camera offset on landing and smoothly recovers to neutral position.
        /// Creates a camera shake effect that emphasizes impact when player lands from height.
        /// </summary>
        /// <param name="state">The camera state to modify.</param>
        /// <param name="deltaTime">Time elapsed since last frame.</param>
        private void ApplyLandingImpact(ref CameraState state, float deltaTime)
        {
            if (!enableLandingImpact || m_Movement == null)
            {
                return;
            }

            if (m_IsLanding || m_LandingOffset < 0f)
            {
                m_LandingOffset = Mathf.Lerp(m_LandingOffset, 0f, deltaTime * landingRecoverySpeed);

                Quaternion cameraRotation = state.GetCorrectedOrientation();
                Vector3 offset = cameraRotation * new Vector3(0f, m_LandingOffset, 0f);
                state.PositionCorrection += offset;
            }
        }

        /// <summary>
        /// Event handler called when jump arc charge-up begins.
        /// Triggers camera pull-back and FOV decrease to anticipate the jump.
        /// </summary>
        private void OnJumpArcStarted()
        {
            m_IsChargingJump = true;
        }

        /// <summary>
        /// Event handler called when jump is released.
        /// Triggers camera kick upward and FOV burst to emphasize the release moment.
        /// </summary>
        private void OnJumpArcEnded()
        {
            m_IsChargingJump = false;
            m_ReleaseKickOffset = releaseVerticalKick;
            m_ReleaseFOVBoost = releaseFOVBurst;
            m_ReleaseTransitionProgress = 1f;
        }

        /// <summary>
        /// Updates jump arc effect intensities over time, managing charge-up and release transitions.
        /// Handles smooth decay of release effects and synchronized charge-up recovery.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last frame.</param>
        private void UpdateJumpArcEffects(float deltaTime)
        {
            if (m_IsChargingJump)
            {
                m_ChargeUpProgress = Mathf.Lerp(m_ChargeUpProgress, 1f, deltaTime * chargeUpSpeed);
            }
            else
            {
                // Sync charge-up decay with release effects during transition for smooth blending
                if (m_ReleaseTransitionProgress > 0f)
                {
                    m_ChargeUpProgress = Mathf.Lerp(m_ChargeUpProgress, 0f, deltaTime * releaseDecaySpeed);
                }
                else
                {
                    m_ChargeUpProgress = Mathf.Lerp(m_ChargeUpProgress, 0f, deltaTime * chargeUpSpeed);
                }
            }

            // Smoothly decay all release effects over time
            if (m_ReleaseKickOffset > 0f)
            {
                m_ReleaseKickOffset = Mathf.Lerp(m_ReleaseKickOffset, 0f, deltaTime * releaseDecaySpeed);
            }

            if (m_ReleaseFOVBoost > 0f)
            {
                m_ReleaseFOVBoost = Mathf.Lerp(m_ReleaseFOVBoost, 0f, deltaTime * releaseDecaySpeed);
            }

            if (m_ReleaseTransitionProgress > 0f)
            {
                m_ReleaseTransitionProgress = Mathf.Lerp(m_ReleaseTransitionProgress, 0f, deltaTime * releaseDecaySpeed);
            }
        }

        /// <summary>
        /// Applies camera position offsets during jump arc charge-up and release phases.
        /// Moves camera down/back during charge and kicks upward on release for dramatic effect.
        /// </summary>
        /// <param name="state">The camera state to modify.</param>
        /// <param name="deltaTime">Time elapsed since last frame.</param>
        private void ApplyJumpArcPositionEffects(ref CameraState state, float deltaTime)
        {
            if (m_ChargeUpProgress <= 0f && m_ReleaseKickOffset <= 0f)
            {
                return;
            }

            Quaternion cameraRotation = state.GetCorrectedOrientation();
            Vector3 offset = Vector3.zero;

            // Pull camera down and back during charge-up for anticipation
            if (m_ChargeUpProgress > 0f)
            {
                Vector3 chargeOffset = new Vector3(0f, chargeUpVerticalOffset, chargeUpForwardOffset);
                offset += chargeOffset * m_ChargeUpProgress;
            }

            // Kick camera upward on release for emphasis
            if (m_ReleaseKickOffset > 0f)
            {
                offset += new Vector3(0f, m_ReleaseKickOffset, 0f);
            }

            // Transform from camera-local space to world space and apply
            Vector3 worldOffset = cameraRotation * offset;
            state.PositionCorrection += worldOffset;
        }

        #endregion
    }
}

