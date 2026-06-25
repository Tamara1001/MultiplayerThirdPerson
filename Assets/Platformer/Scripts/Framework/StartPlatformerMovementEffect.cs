using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Platformer
{
    /// <summary>
    /// An interaction effect that triggers the movement of one or more platforms controlled by a
    /// <see cref="AutomatedNetworkTransform"/>. This can be used to create buttons or switches
    /// that activate moving platforms, bridges, or other dynamic level elements.
    /// </summary>
    public class StartPlatformerMovementEffect : MonoBehaviour, IInteractionEffect
    {
        #region Fields & Properties

        [Tooltip("The priority of this effect in the interaction chain. Higher values are executed first.")]
        [SerializeField] private int priority = 0;
        [Tooltip("List of platforms to activate when this effect is triggered. If empty, attempts to activate the interactable object itself.")]
        [SerializeField] private List<AutomatedNetworkTransform> platformsToStart;

        public int Priority => priority;

        #endregion

        #region Public Methods

        /// <summary>
        /// Applies the platform movement effect by starting movement on assigned platforms or the interactable itself.
        /// </summary>
        /// <param name="interactor">The GameObject that triggered the interaction.</param>
        /// <param name="interactable">The GameObject being interacted with.</param>
        /// <returns>Coroutine that completes after triggering platform movement.</returns>
        public IEnumerator ApplyEffect(GameObject interactor, GameObject interactable)
        {
            if (platformsToStart != null && platformsToStart.Count > 0)
            {
                foreach (var platform in platformsToStart)
                {
                    if (platform != null)
                    {
                        platform.StartMovement();
                    }
                    else
                    {
                        Debug.LogWarning($"StartPlatformerMovementEffect: An item in the 'Platforms To Start' list is null on interactable '{interactable.name}'.", interactable);
                    }
                }
            }
            else
            {
                if (interactable.TryGetComponent<AutomatedNetworkTransform>(out var selfPlatform))
                {
                    selfPlatform.StartMovement();
                }
                else
                {
                    Debug.LogWarning($"StartPlatformerMovementEffect: The 'Platforms To Start' list is empty, and the interactable object '{interactable.name}' does not have a PlatformerNetworkTransform component.", interactable);
                }
            }
            yield return null;
        }

        /// <summary>
        /// Cancels the effect. This effect has no cancellation behavior.
        /// </summary>
        /// <param name="interactor">The GameObject that triggered the interaction.</param>
        public void CancelEffect(GameObject interactor) { }

        #endregion
    }
}
