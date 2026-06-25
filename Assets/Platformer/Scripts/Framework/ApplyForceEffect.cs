using UnityEngine;
using System.Collections;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Platformer
{
    /// <summary>
    /// An interaction effect that applies an instantaneous force (impulse) to the interacting object.
    /// This is ideal for creating jump pads, knockback traps, or other physics-based interactions.
    /// It intelligently applies force to either a <see cref="CoreMovement"/> component for characters
    /// or a standard <see cref="Rigidbody"/> for other physics objects.
    /// </summary>
    public class ApplyForceEffect : MonoBehaviour, IInteractionEffect
    {
        #region Fields & Properties

        [Tooltip("The priority of this effect in the interaction chain. Higher values are executed first.")]
        [SerializeField] private int priority = 0;

        [Tooltip("The base force vector to apply to the interactor. Best used with ForceMode.Impulse for an instantaneous push.")]
        [SerializeField] private Vector3 impulseVector = new Vector3(0, 15, 0);

        [Tooltip("The force mode to use when applying the impulse. Impulse is recommended for instantaneous forces.")]
        [SerializeField] private ForceMode forceMode = ForceMode.Impulse;

        [Header("Force Scaling")]
        [Tooltip("When enabled, the force will increase after each interaction.")]
        [SerializeField] private bool increaseForceAfterInteraction = false;

        [Tooltip("The multiplier to apply to the force after each interaction. For example, 1.1 increases force by 10% each time.")]
        [SerializeField] private float forceMultiplier = 1.1f;

        [Tooltip("The maximum multiplier that can be applied to the original force. Use 0 for no limit.")]
        [SerializeField] private float maxForceMultiplier = 3.0f;

        [Tooltip("When enabled, the force will reset to its original value after this many seconds of no interactions.")]
        [SerializeField] private bool resetForceOverTime = false;

        [Tooltip("Time in seconds after which the force resets to its original value.")]
        [SerializeField] private float resetTime = 10.0f;

        // Tracks the current force multiplier applied to the base impulse vector
        private float m_CurrentForceMultiplier = 1.0f;

        // Tracks the last time an interaction occurred for force reset timing
        private float m_LastInteractionTime;

        public int Priority => priority;

        #endregion

        #region Unity Methods

        private void Update()
        {
            // Reset force multiplier if enough time has passed without interactions
            if (resetForceOverTime && increaseForceAfterInteraction &&
                Time.time - m_LastInteractionTime > resetTime &&
                m_CurrentForceMultiplier > 1.0f)
            {
                ResetForce();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Applies the configured force to the interacting object.
        /// For <see cref="CoreMovement"/> components, vertical force is set directly while horizontal force is applied externally.
        /// For <see cref="Rigidbody"/> components, the full force vector is applied using the configured force mode.
        /// </summary>
        /// <param name="interactor">The GameObject that initiated the interaction (receives the force).</param>
        /// <param name="interactable">The GameObject being interacted with (this effect's owner).</param>
        /// <returns>Coroutine enumerator for effect execution.</returns>
        public IEnumerator ApplyEffect(GameObject interactor, GameObject interactable)
        {
            // Scale the base impulse by the current multiplier
            Vector3 currentImpulse = impulseVector * m_CurrentForceMultiplier;

            // Handle CoreMovement components (character controllers)
            if (interactor.TryGetComponent<CoreMovement>(out var coreMovement))
            {
                // Set vertical velocity directly for immediate upward force
                coreMovement.SetVerticalVelocity(currentImpulse.y);

                // Apply horizontal force separately if present
                if (currentImpulse.x != 0 || currentImpulse.z != 0)
                {
                    Vector3 horizontalForce = new Vector3(currentImpulse.x, 0, currentImpulse.z);
                    coreMovement.ApplyExternalForce(horizontalForce, forceMode);
                }
            }
            // Handle standard Rigidbody components
            else if (interactor.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.AddForce(currentImpulse, forceMode);
            }
            else
            {
                Debug.LogWarning(
                    $"Interactor '{interactor.name}' has neither a CoreMovement nor a Rigidbody component. Cannot apply impulse.",
                    interactor);
            }

            // Increase force for next interaction if scaling is enabled
            if (increaseForceAfterInteraction)
            {
                IncreaseForce();
            }

            m_LastInteractionTime = Time.time;
            yield return null;
        }

        /// <summary>
        /// Cancels the effect. This effect has no ongoing state to cancel.
        /// </summary>
        /// <param name="interactor">The GameObject that initiated the interaction.</param>
        public void CancelEffect(GameObject interactor) { }

        /// <summary>
        /// Resets the force multiplier back to 1.0 (original force).
        /// </summary>
        public void ResetForce()
        {
            m_CurrentForceMultiplier = 1.0f;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Increases the force multiplier for the next interaction.
        /// The multiplier is clamped to the configured maximum if set.
        /// </summary>
        private void IncreaseForce()
        {
            m_CurrentForceMultiplier *= forceMultiplier;

            // Clamp to maximum multiplier if configured
            if (maxForceMultiplier > 0 && m_CurrentForceMultiplier > maxForceMultiplier)
            {
                m_CurrentForceMultiplier = maxForceMultiplier;
            }
        }

        #endregion
    }
}
