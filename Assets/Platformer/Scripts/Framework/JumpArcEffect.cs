using UnityEngine;
using Unity.Cinemachine;
using System.Collections;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Platformer
{
    /// <summary>
    /// An interaction effect that forces a player to jump in an arc from a start position to an end position.
    /// This effect takes control of the player's movement while still allowing horizontal input from PlatformerMovementAbility.
    /// When the player reaches the destination, it triggers the "JumpLandHeavy" animator trigger.
    /// </summary>
    public class JumpArcEffect : MonoBehaviour, IInteractionEffect
    {
        #region Fields & Properties
        [Header("Effect Settings")]
        [Tooltip("The priority of this effect in the interaction chain. Higher values are executed first.")]
        [SerializeField] private int priority = 100;

        [Header("Arc Configuration")]
        [Tooltip("Optional: The starting position for the jump. If not set, uses the interactor's current position.")]
        [SerializeField] private Transform startPosition;

        [Tooltip("The target position where the player should land.")]
        [SerializeField] private Transform endPosition;

        [Tooltip("The height of the arc peak above the start position.")]
        [SerializeField] private float arcHeight = 5f;

        [Tooltip("Duration of the arc jump in seconds.")]
        [SerializeField] private float jumpDuration = 2f;

        [Tooltip("Curve that controls the arc trajectory (0 = start, 1 = end).")]
        [SerializeField] private AnimationCurve arcCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Movement Control")]
        [Tooltip("How much horizontal input from the player is allowed during the arc (0 = no control, 1 = full control).")]
        [SerializeField, Range(0f, 1f)] private float horizontalInputInfluence = 0.3f;

        [Tooltip("Maximum horizontal speed the player can achieve with input during the arc.")]
        [SerializeField] private float maxHorizontalInputSpeed = 8f;

        [Header("Landing Settings")]
        [Tooltip("Distance from the target at which the landing is considered complete.")]
        [SerializeField] private float landingThreshold = 0.5f;

        [Tooltip("If true, disables player movement input during the arc.")]
        [SerializeField] private bool disableMovementInput = false;

        [Header("Animation Timing")]
        [Tooltip("Time to wait after triggering JumpStartHeavy before executing the jump.")]
        [SerializeField] private float jumpStartDelay = 0.5f;

        [Header("Camera Shake - Jump Start")]
        [Tooltip("Enable camera shake when the jump starts.")]
        [SerializeField] private bool enableJumpStartShake = true;
        [Tooltip("Velocity/intensity of the camera shake at jump start.")]
        [SerializeField] private Vector3 jumpStartShakeVelocity = new Vector3(0.05f, 0.3f, 0.05f);
        [Tooltip("Duration of the camera shake impulse at jump start.")]
        [SerializeField] private float jumpStartShakeDuration = 0.15f;

        [Header("Camera Shake - Landing")]
        [Tooltip("Enable camera shake when landing.")]
        [SerializeField] private bool enableLandingShake = false;
        [Tooltip("Velocity/intensity of the camera shake on landing.")]
        [SerializeField] private Vector3 landingShakeVelocity = new Vector3(0.1f, 0.2f, 0.1f);
        [Tooltip("Duration of the camera shake impulse on landing.")]
        [SerializeField] private float landingShakeDuration = 0.2f;

        [Header("Landing Animation Control")]
        [Tooltip("If true, triggers the JumpLandHeavy animation on landing.")]
        [SerializeField] private bool triggerLandingAnimation = true;
        [Tooltip("If true, disables movement input during the landing animation.")]
        [SerializeField] private bool disableMovementDuringLanding = true;
        [Tooltip("Duration of the landing animation (time to wait before re-enabling movement).")]
        [SerializeField] private float landingAnimationDuration = 0.5f;
        [Tooltip("Additional delay after landing animation before movement is restored.")]
        [SerializeField] private float delayBeforeMovementRestore = 0f;

        [Header("Sound Effects")]
        [SerializeField] private SoundDef jumpStartSound;
        [SerializeField] private SoundDef jumpPreDelaySound;
        [SerializeField] private SoundDef jumpLandSound;

        [Header("Visual Effects")]
        [SerializeField] private GameObject jumpStartEffectPrefab;
        [SerializeField] private GameObject jumpLandEffectPrefab;

        [Header("Game Events")]
        [Tooltip("Event raised when the jump arc starts.")]
        [SerializeField] private GameEvent jumpArcStarted;
        [Tooltip("Event raised when the jump arc ends.")]
        [SerializeField] private GameEvent jumpArcEnded;

        private CoreMovement m_CoreMovement;
        private PlatformerLocomotionAbility m_PlatformerMovement;
        private CorePlayerManager m_PlayerManager;
        private Animator m_PlayerAnimator;
        private Vector3 m_StartPosition;
        private Vector3 m_EndPosition;
        private float m_ArcStartTime;
        private bool m_IsArcActive;
        private System.Func<Vector3, Vector3> m_OriginalMoveOverride;

        private readonly int m_AnimIDJumpStartHeavy = Animator.StringToHash("JumpStartHeavy");
        private readonly int m_AnimIDJumpStartHeavyComplete = Animator.StringToHash("JumpStartHeavyComplete");
        private readonly int m_AnimIDJumpLandHeavy = Animator.StringToHash("JumpLandHeavy");

        public int Priority => priority;
        #endregion

        #region IInteractionEffect Implementation
        public IEnumerator ApplyEffect(GameObject interactor, GameObject interactable)
        {
            if (!ValidateComponents(interactor))
            {
                Debug.LogWarning($"JumpArcEffect: Required components not found on interactor '{interactor.name}'");
                yield break;
            }

            if (endPosition == null)
            {
                Debug.LogWarning($"JumpArcEffect: End position not set on '{gameObject.name}'");
                yield break;
            }

            yield return StartCoroutine(ExecuteArcJump(interactor));
        }

        public void CancelEffect(GameObject interactor)
        {
            if (m_IsArcActive)
            {
                CancelArcJumpImmediate(interactor);
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Validates that all required components are present on the interactor.
        /// </summary>
        /// <param name="interactor">The GameObject to validate.</param>
        /// <returns>True if all required components are found, false otherwise.</returns>
        private bool ValidateComponents(GameObject interactor)
        {
            m_CoreMovement = interactor.GetComponent<CoreMovement>();
            m_PlatformerMovement = interactor.GetComponent<PlatformerLocomotionAbility>();
            m_PlayerManager = interactor.GetComponent<CorePlayerManager>();
            m_PlayerAnimator = interactor.GetComponentInChildren<Animator>();

            return m_CoreMovement != null && m_PlatformerMovement != null && m_PlayerAnimator != null;
        }

        /// <summary>
        /// Executes the complete arc jump sequence, including wind-up animation, arc movement, and landing.
        /// </summary>
        /// <param name="interactor">The player GameObject performing the jump.</param>
        private IEnumerator ExecuteArcJump(GameObject interactor)
        {
            m_StartPosition = startPosition != null ? startPosition.position : interactor.transform.position;
            m_EndPosition = endPosition.position;

            if (disableMovementInput && m_PlayerManager != null)
            {
                m_PlayerManager.SetMovementInputEnabled(false);
            }

            jumpArcStarted?.Raise();

            if (jumpStartEffectPrefab != null)
            {
                CoreDirector.CreatePrefabEffect(jumpStartEffectPrefab)
                    .WithPosition(interactor.transform.position)
                    .WithName("StartEffect")
                    .WithDuration(2f)
                    .Create();
            }

            if (enableJumpStartShake)
            {
                CoreDirector.RequestCameraShake()
                    .WithVelocity(jumpStartShakeVelocity)
                    .AtPosition(interactor.transform.position)
                    .WithImpulseDefinition(CinemachineImpulseDefinition.ImpulseShapes.Bump, CinemachineImpulseDefinition.ImpulseTypes.Dissipating, jumpStartShakeDuration)
                    .Execute();
            }

            if (m_PlayerAnimator != null)
            {
                m_PlayerAnimator.SetTrigger(m_AnimIDJumpStartHeavy);
            }

            if (jumpStartDelay > 0f)
            {
                if (jumpPreDelaySound != null)
                {
                    CoreDirector.RequestAudio(jumpPreDelaySound)
                        .AttachedTo(interactor.transform)
                        .Play();
                }
                yield return new WaitForSeconds(jumpStartDelay);
            }

            if (m_PlayerAnimator != null)
            {
                m_PlayerAnimator.SetTrigger(m_AnimIDJumpStartHeavyComplete);
            }

            if (jumpStartSound != null)
            {
                CoreDirector.RequestAudio(jumpStartSound)
                    .AttachedTo(interactor.transform)
                    .Play();
            }

            // Event raised at launch moment to trigger camera release effects
            jumpArcEnded?.Raise();

            m_ArcStartTime = Time.time;
            m_IsArcActive = true;

            Vector3 directionToEnd = (m_EndPosition - m_StartPosition);
            // Keep rotation on horizontal plane
            directionToEnd.y = 0f;
            if (directionToEnd.magnitude > 0.1f)
            {
                float targetAngle = Mathf.Atan2(directionToEnd.x, directionToEnd.z) * Mathf.Rad2Deg;
                Transform targetTransform = m_CoreMovement.RotationTransform != null ? m_CoreMovement.RotationTransform : interactor.transform;
                targetTransform.rotation = Quaternion.Euler(0f, targetAngle, 0f);
            }

            m_OriginalMoveOverride = m_CoreMovement.FinalMoveCalculationOverride;
            m_CoreMovement.FinalMoveCalculationOverride = CalculateArcMovement;
            m_CoreMovement.SetVerticalVelocity(0f);

            float elapsed = 0f;
            while (elapsed < jumpDuration)
            {
                elapsed = Time.time - m_ArcStartTime;

                float distanceToTarget = Vector3.Distance(interactor.transform.position, m_EndPosition);
                if (distanceToTarget <= landingThreshold)
                {
                    break;
                }

                yield return null;
            }

            m_CoreMovement.SetPosition(m_EndPosition, false);

            yield return StartCoroutine(EndArcJump(interactor));
        }

        /// <summary>
        /// Calculates the movement vector for the arc trajectory with optional player input influence.
        /// </summary>
        /// <param name="originalPlayerMovement">The original movement input from the player.</param>
        /// <returns>The calculated movement vector to follow the arc path.</returns>
        private Vector3 CalculateArcMovement(Vector3 originalPlayerMovement)
        {
            if (!m_IsArcActive)
            {
                return originalPlayerMovement;
            }

            float elapsed = Time.time - m_ArcStartTime;
            float normalizedTime = Mathf.Clamp01(elapsed / jumpDuration);

            Vector3 arcPosition = CalculateArcPosition(normalizedTime);

            Vector3 horizontalInput = Vector3.zero;
            if (m_PlatformerMovement != null && horizontalInputInfluence > 0f)
            {
                Vector3 originalHorizontal = new Vector3(originalPlayerMovement.x, 0, originalPlayerMovement.z);

                if (originalHorizontal.magnitude > maxHorizontalInputSpeed)
                {
                    originalHorizontal = originalHorizontal.normalized * maxHorizontalInputSpeed;
                }

                horizontalInput = originalHorizontal * horizontalInputInfluence;
            }

            Vector3 currentPosition = m_CoreMovement.transform.position;
            Vector3 targetPosition = arcPosition + (horizontalInput * Time.deltaTime);
            Vector3 arcMovement = (targetPosition - currentPosition) / Time.deltaTime;

            float distanceToEnd = Vector3.Distance(currentPosition, m_EndPosition);
            if (distanceToEnd <= landingThreshold)
            {
                arcMovement = (m_EndPosition - currentPosition) / Time.deltaTime;
            }

            return arcMovement * Time.deltaTime;
        }

        /// <summary>
        /// Calculates a point on the arc trajectory using parabolic interpolation.
        /// </summary>
        /// <param name="normalizedTime">Time value from 0 to 1 representing progress along the arc.</param>
        /// <returns>The position on the arc at the given time.</returns>
        private Vector3 CalculateArcPosition(float normalizedTime)
        {
            float curveValue = arcCurve.Evaluate(normalizedTime);
            Vector3 linearPosition = Vector3.Lerp(m_StartPosition, m_EndPosition, curveValue);

            // Parabola formula peaks at 0.5 for smooth arc trajectory
            float heightMultiplier = 4f * normalizedTime * (1f - normalizedTime);
            Vector3 arcOffset = Vector3.up * (arcHeight * heightMultiplier);

            return linearPosition + arcOffset;
        }

        /// <summary>
        /// Immediately cancels the arc jump and restores player control.
        /// </summary>
        /// <param name="interactor">The player GameObject to restore control to.</param>
        private void CancelArcJumpImmediate(GameObject interactor)
        {
            if (!m_IsArcActive) return;

            m_IsArcActive = false;

            if (m_CoreMovement != null)
            {
                m_CoreMovement.FinalMoveCalculationOverride = m_OriginalMoveOverride;
                m_CoreMovement.SetVerticalVelocity(-2f);
            }

            if (m_PlayerManager != null)
            {
                m_PlayerManager.SetMovementInputEnabled(true);
            }

            m_CoreMovement = null;
            m_PlatformerMovement = null;
            m_PlayerManager = null;
            m_PlayerAnimator = null;
            m_OriginalMoveOverride = null;
        }

        /// <summary>
        /// Handles the landing sequence including animation, effects, and movement restoration.
        /// </summary>
        /// <param name="interactor">The player GameObject performing the landing.</param>
        private IEnumerator EndArcJump(GameObject interactor)
        {
            if (!m_IsArcActive) yield break;

            m_IsArcActive = false;

            if (m_CoreMovement != null)
            {
                m_CoreMovement.FinalMoveCalculationOverride = m_OriginalMoveOverride;
                m_CoreMovement.SetVerticalVelocity(-2f);
            }

            bool shouldRestoreMovement = false;
            if (disableMovementDuringLanding && m_PlayerManager != null)
            {
                m_PlayerManager.SetMovementInputEnabled(false);
                shouldRestoreMovement = true;
            }
            else if (disableMovementInput && m_PlayerManager != null)
            {
                m_PlayerManager.SetMovementInputEnabled(true);
            }

            if (triggerLandingAnimation && m_PlayerAnimator != null)
            {
                m_PlayerAnimator.ResetTrigger(m_AnimIDJumpLandHeavy);
                m_PlayerAnimator.SetTrigger(m_AnimIDJumpLandHeavy);
            }

            if (triggerLandingAnimation && landingAnimationDuration > 0f)
            {
                yield return new WaitForSeconds(landingAnimationDuration / 2);

                if (jumpLandEffectPrefab != null)
                {
                    CoreDirector.CreatePrefabEffect(jumpLandEffectPrefab)
                        .WithPosition(interactor.transform.position)
                        .WithName("LandEffect")
                        .WithDuration(2f)
                        .Create();
                }

                if (enableLandingShake)
                {
                    CoreDirector.RequestCameraShake()
                        .WithVelocity(landingShakeVelocity)
                        .AtPosition(interactor.transform.position)
                        .WithImpulseDefinition(CinemachineImpulseDefinition.ImpulseShapes.Rumble, CinemachineImpulseDefinition.ImpulseTypes.Dissipating, landingShakeDuration)
                        .Execute();
                }

                if (jumpLandSound != null)
                {
                    CoreDirector.RequestAudio(jumpLandSound)
                        .WithPosition(interactor.transform.position)
                        .Play();
                }

                yield return new WaitForSeconds(landingAnimationDuration / 2);
            }

            if (delayBeforeMovementRestore > 0f)
            {
                yield return new WaitForSeconds(delayBeforeMovementRestore);
            }

            if (shouldRestoreMovement && m_PlayerManager != null)
            {
                m_PlayerManager.SetMovementInputEnabled(true);
            }

            m_CoreMovement = null;
            m_PlatformerMovement = null;
            m_PlayerManager = null;
            m_PlayerAnimator = null;
            m_OriginalMoveOverride = null;
        }

        private void OnDrawGizmosSelected()
        {
            if (endPosition == null) return;

            Vector3 start = startPosition != null ? startPosition.position : transform.position;
            Vector3 end = endPosition.position;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(start, 0.2f);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(end, 0.2f);

            Gizmos.color = Color.cyan;
            Vector3 lastPos = start;

            for (int i = 1; i <= 20; i++)
            {
                float t = i / 20f;
                Vector3 linearPos = Vector3.Lerp(start, end, arcCurve.Evaluate(t));
                float heightMultiplier = 4f * t * (1f - t);
                Vector3 arcPos = linearPos + Vector3.up * (arcHeight * heightMultiplier);

                Gizmos.DrawLine(lastPos, arcPos);
                lastPos = arcPos;
            }

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(end, landingThreshold);
        }
        #endregion
    }
}
