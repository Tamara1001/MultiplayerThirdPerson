using UnityEngine;
using Unity.Netcode;
using Unity.Cinemachine;
using System.Collections;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// An interaction effect that triggers a camera shake when an interaction occurs.
    /// Supports synchronized shaking across all clients via RPC or local-only execution.
    /// </summary>
    public class CameraShakeEffect : NetworkBehaviour, IInteractionEffect
    {
        #region Fields & Properties

        [Header("Effect Settings")]
        [Tooltip("The priority of this effect in the interaction chain. Higher values are executed first.")]
        [SerializeField] private int priority = 0;

        [Header("Camera Shake Configuration")]
        [Tooltip("Duration of the camera shake impulse in seconds.")]
        [SerializeField] private float duration = 0.2f;

        [Tooltip("Velocity/intensity of the shake. Can be set per-axis or as uniform value.")]
        [SerializeField] private Vector3 velocity = Vector3.one;

        [Tooltip("If true, uses uniform velocity for all axes (uses velocity.x as the uniform value).")]
        [SerializeField] private bool useUniformVelocity = false;

        [Tooltip("If true, the shake will originate from the interactable's position (for distance-based attenuation).")]
        [SerializeField] private bool usePosition = false;

        [Header("Impulse Definition")]
        [Tooltip("The shape of the impulse (Bump, Recoil, Explosion, etc.).")]
        [SerializeField] private CinemachineImpulseDefinition.ImpulseShapes impulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;

        [Tooltip("The type of impulse (Uniform, Propagating, etc.).")]
        [SerializeField] private CinemachineImpulseDefinition.ImpulseTypes impulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;

        [Header("Network Settings")]
        [Tooltip("If true, sends the camera shake to all clients via RPC for synchronized effects. If false, only plays locally.")]
        [SerializeField] private bool syncToAllClients = true;

        public int Priority => priority;

        #endregion

        #region Public Methods

        /// <summary>
        /// Applies a camera shake effect when an interaction occurs.
        /// Can be synchronized across all network clients or executed locally based on configuration.
        /// </summary>
        /// <param name="interactor">The GameObject that initiated the interaction.</param>
        /// <param name="interactable">The GameObject being interacted with (used for shake origin position if configured).</param>
        /// <returns>Coroutine enumerator for effect execution.</returns>
        public IEnumerator ApplyEffect(GameObject interactor, GameObject interactable)
        {
            // Determine shake origin position: use interactable's world position or zero (screen-space)
            Vector3 position = usePosition ? interactable.transform.position : Vector3.zero;

            if (syncToAllClients)
            {
                // Synchronize shake across all network clients for consistent multiplayer experience
                TriggerCameraShakeRpc(position);
            }
            else
            {
                // Execute shake locally only
                ExecuteCameraShake(position);
            }

            yield return null;
        }

        /// <summary>
        /// Cancels the effect. Camera shake is fire-and-forget, so no cleanup is needed.
        /// </summary>
        /// <param name="interactor">The GameObject that initiated the interaction.</param>
        public void CancelEffect(GameObject interactor)
        {
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// RPC that triggers camera shake on all connected clients for synchronized effects.
        /// </summary>
        /// <param name="position">The world position where the shake originates, or Vector3.zero for screen-space shake.</param>
        [Rpc(SendTo.Everyone)]
        private void TriggerCameraShakeRpc(Vector3 position)
        {
            ExecuteCameraShake(position);
        }

        /// <summary>
        /// Executes the camera shake using CoreDirector's CameraShakeBuilder.
        /// Configures shake parameters using the builder pattern and triggers the impulse.
        /// </summary>
        /// <param name="position">The world position where the shake originates, or Vector3.zero for screen-space shake.</param>
        private void ExecuteCameraShake(Vector3 position)
        {
            var shakeBuilder = CoreDirector.RequestCameraShake();

            shakeBuilder.WithDuration(duration);

            // Apply velocity as uniform (single value) or per-axis (Vector3)
            if (useUniformVelocity)
            {
                shakeBuilder.WithVelocity(velocity.x);
            }
            else
            {
                shakeBuilder.WithVelocity(velocity);
            }

            shakeBuilder.WithImpulseDefinition(impulseShape, impulseType, duration);

            // Set shake origin for spatial attenuation if configured
            if (usePosition)
            {
                shakeBuilder.AtPosition(position);
            }

            shakeBuilder.Execute();
        }

        #endregion
    }
}

