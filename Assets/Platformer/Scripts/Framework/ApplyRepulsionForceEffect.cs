using UnityEngine;
using System.Collections;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Platformer
{
    /// <summary>
    /// An interaction effect that applies a repulsive force to the interactor, pushing them directly away
    /// from the direction they are facing. This is useful for creating bounce pads or traps that knock the player back.
    /// The force is applied via the <see cref="CoreMovement.ApplyExternalForce"/> method to ensure compatibility
    /// with the CharacterController-based movement system.
    /// </summary>
    public class ApplyRepulsionForceEffect : MonoBehaviour, IInteractionEffect
    {
        #region Fields & Properties

        [Header("Effect Settings")]
        [Tooltip("The priority of this effect in the interaction chain. Higher values are executed first.")]
        [SerializeField] private int priority = 0;

        [Tooltip("The magnitude of the initial force impulse to apply to the interactor.")]
        [SerializeField] private float forceMagnitude = 10f;

        [Tooltip("The type of force to apply. Impulse provides a sudden, instantaneous push.")]
        [SerializeField] private ForceMode forceMode = ForceMode.Impulse;

        public int Priority => priority;

        #endregion

        #region Public Methods

        /// <summary>
        /// Applies a repulsive force that pushes the interactor backward (opposite to their facing direction).
        /// The force direction is calculated as the negative of the character's forward vector, creating a knockback effect.
        /// </summary>
        /// <param name="interactor">The GameObject that initiated the interaction (receives the repulsion force).</param>
        /// <param name="interactable">The GameObject being interacted with (this effect's owner).</param>
        /// <returns>Coroutine enumerator for effect execution.</returns>
        public IEnumerator ApplyEffect(GameObject interactor, GameObject interactable)
        {
            if (interactor.TryGetComponent<CoreMovement>(out var coreMovement))
            {
                // Calculate repulsion direction as opposite to the character's facing direction
                Vector3 forceDirection = -coreMovement.RotationTransform.forward;

                // Scale direction by magnitude to get final force vector
                Vector3 forceVector = forceDirection * forceMagnitude;

                coreMovement.ApplyExternalForce(forceVector, forceMode);
            }
            else
            {
                Debug.LogWarning($"Interactor '{interactor.name}' does not have a CoreMovement component. Cannot apply ApplyRepulsionForceEffect.", interactor);
            }

            yield return null;
        }

        /// <summary>
        /// Cancels the effect. This effect has no ongoing state to cancel.
        /// </summary>
        /// <param name="interactor">The GameObject that initiated the interaction.</param>
        public void CancelEffect(GameObject interactor) { }

        #endregion
    }
}
