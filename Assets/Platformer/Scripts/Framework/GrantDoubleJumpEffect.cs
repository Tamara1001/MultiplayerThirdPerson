using UnityEngine;
using Unity.Netcode;
using System.Collections;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Platformer
{
    /// <summary>
    /// An interaction effect that grants the <see cref="DoubleJumpAbility"/> to an interactor.
    /// This effect dynamically adds the DoubleJumpAbility component to the interacting GameObject if it doesn't
    /// already have it, effectively unlocking the ability for the player.
    /// </summary>
    public class GrantDoubleJumpEffect : NetworkBehaviour, IInteractionEffect
    {
        #region Fields & Properties

        [Header("Effect Settings")]
        [Tooltip("The priority of this effect in the interaction chain. Higher values are executed first.")]
        [SerializeField] private int priority = 0;

        [Header("Notification")]
        [Tooltip("The notification event to raise when double jump is granted. Assign the NotificationEvent ScriptableObject asset.")]
        [SerializeField] private NotificationEvent notificationEvent;

        /// <summary>
        /// Gets the priority of this effect in the interaction chain. Higher values are executed first.
        /// </summary>
        public int Priority => priority;

        #endregion

        #region Public Methods

        /// <summary>
        /// Applies the double jump grant effect when a player interacts with it.
        /// Checks if the interactor has <see cref="CoreMovement"/> and doesn't already have the ability,
        /// then dynamically adds the <see cref="DoubleJumpAbility"/> component and broadcasts a notification.
        /// </summary>
        /// <param name="interactor">The GameObject representing the player receiving the ability.</param>
        /// <param name="interactable">The GameObject representing the ability granter itself.</param>
        /// <returns>An enumerator for coroutine execution.</returns>
        public IEnumerator ApplyEffect(GameObject interactor, GameObject interactable)
        {
            if (interactor.TryGetComponent<CoreMovement>(out var coreMovement))
            {
                // Check if the player already has the double jump ability
                if (!coreMovement.HasAbility<DoubleJumpAbility>())
                {
                    // Dynamically add the DoubleJumpAbility component
                    var newAbility = interactor.AddComponent<DoubleJumpAbility>();
                    coreMovement.AddAbility(newAbility);
                    Debug.Log("Granted Double Jump ability.", interactor);

                    // Broadcast notification to all players
                    if (notificationEvent != null)
                    {
                        var playerManager = interactor.GetComponent<CorePlayerManager>();
                        ulong clientId = playerManager != null ? playerManager.OwnerClientId : 0;

                        SendNotificationRpc(clientId, "Player");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"Interactor '{interactor.name}' does not have a CoreMovement component. Cannot grant Double Jump ability.", interactor);
            }

            yield return null;
        }

        /// <summary>
        /// Cancels the effect. No cleanup is needed for this effect.
        /// </summary>
        /// <param name="interactor">The GameObject that was interacting with the ability granter.</param>
        public void CancelEffect(GameObject interactor)
        {
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// RPC called on all clients to display the ability unlock notification.
        /// </summary>
        /// <param name="clientId">The client ID of the player who unlocked the ability.</param>
        /// <param name="playerName">The name of the player who unlocked the ability.</param>
        [Rpc(SendTo.Everyone)]
        private void SendNotificationRpc(ulong clientId, string playerName)
        {
            if (notificationEvent != null)
            {
                notificationEvent.Raise(new NotificationPayload
                {
                    clientId = clientId,
                    message = $"{playerName} unlocked Double Jump!"
                });
            }
        }

        #endregion
    }
}
