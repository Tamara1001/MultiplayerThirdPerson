using UnityEngine;
using Unity.Netcode;
using System.Collections;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Platformer
{
    /// <summary>
    /// An interaction effect that modifies a specific stat on the interactor's <see cref="CoreStatsHandler"/>.
    /// This is a versatile effect used for creating health packs, coin pickups, damage traps, or any other
    /// interaction that results in a numerical change to a player's stats.
    /// </summary>
    public class ModifyStatEffect : MonoBehaviour, IInteractionEffect
    {
        #region Fields & Properties

        [Header("Effect Settings")]
        [Tooltip("The priority of this effect in the interaction chain. Higher values are executed first.")]
        [SerializeField] private int priority = 0;
        [Tooltip("The exact name of the stat to modify (e.g., 'Health', 'Stamina'). This must match a name in the StatsConfig.")]
        [SerializeField] private string statNameToModify = "Health";
        [Tooltip("The amount to add (positive for healing/restoring) or subtract (negative for damage) from the stat.")]
        [SerializeField] private float modificationAmount = 25f;

        /// <summary>
        /// Gets the priority of this effect in the interaction chain. Higher values are executed first.
        /// </summary>
        public int Priority => priority;

        // Cache the stat hash to avoid repeated string hashing
        private int m_StatHash;
        private bool m_HasCachedHash;

        #endregion

        #region Public Methods

        /// <summary>
        /// Applies the stat modification effect to the interactor.
        /// Caches the stat hash on first use for performance optimization.
        /// </summary>
        /// <param name="interactor">The GameObject initiating the interaction (typically the player).</param>
        /// <param name="interactable">The GameObject being interacted with (the object containing this effect).</param>
        /// <returns>A coroutine that completes after one frame.</returns>
        public IEnumerator ApplyEffect(GameObject interactor, GameObject interactable)
        {
            if (interactor.TryGetComponent<CoreStatsHandler>(out var statsHandler))
            {
                // Cache the stat hash on first use to avoid repeated string hashing operations
                if (!m_HasCachedHash)
                {
                    m_StatHash = Animator.StringToHash(statNameToModify);
                    m_HasCachedHash = true;
                }

                ulong sourceId = 0;
                if (interactable.TryGetComponent<NetworkObject>(out var netObj))
                {
                    sourceId = netObj.OwnerClientId;
                }

                ModificationSource source = modificationAmount > 0 ? ModificationSource.Healing : ModificationSource.Environmental;
                statsHandler.ModifyStat(m_StatHash, modificationAmount, sourceId, source);
            }
            else
            {
                Debug.LogWarning($"Interactor '{interactor.name}' does not have a CoreStatsHandler component. Cannot apply ModifyStatEffect.", interactor);
            }

            yield return null;
        }

        /// <summary>
        /// Cancels the effect. This implementation has no cancellation logic as stat modifications are instantaneous.
        /// </summary>
        /// <param name="interactor">The GameObject that was interacting.</param>
        public void CancelEffect(GameObject interactor) { }

        #endregion
    }
}
