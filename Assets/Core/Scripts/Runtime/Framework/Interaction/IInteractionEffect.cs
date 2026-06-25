using UnityEngine;
using System.Collections;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Defines a single, reusable action that can occur as part of an interaction.
    /// </summary>
    public interface IInteractionEffect
    {
        /// <summary>
        /// Gets the priority of this effect. Effects with higher priority values are executed first.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Applies the effect. This can be a coroutine for time-based effects.
        /// The ModularInteractable will wait for this coroutine to complete before processing the next effect.
        /// </summary>
        /// <param name="interactor">The GameObject performing the interaction (e.g., the player).</param>
        /// <param name="interactable">The GameObject being interacted with.</param>
        /// <returns>An IEnumerator to be run as a coroutine. Return null or `yield return null;` for instantaneous effects.</returns>
        IEnumerator ApplyEffect(GameObject interactor, GameObject interactable);

        /// <summary>
        /// Called when the effect is being stopped before it completes naturally.
        /// Use this to clean up any persistent state (e.g., reverting a visual change).
        /// </summary>
        /// <param name="interactor">The GameObject that was performing the interaction.</param>
        void CancelEffect(GameObject interactor);
    }
}
