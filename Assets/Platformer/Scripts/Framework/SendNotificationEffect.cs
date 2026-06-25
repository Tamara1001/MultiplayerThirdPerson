using UnityEngine;
using System.Collections;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// An interaction effect that sends a notification message to the HUD system.
    /// This allows interactables to display custom messages to players when triggered.
    /// The notification will be visible to all players through their HUDs.
    /// </summary>
    public class SendNotificationEffect : MonoBehaviour, IInteractionEffect
    {
        #region Fields & Properties

        [Header("Notification Configuration")]
        [Tooltip("The notification event to raise. Assign the NotificationEvent ScriptableObject asset.")]
        [SerializeField] private NotificationEvent notificationEvent;

        [Tooltip("The message to display in the notification. This will appear in the HUD's notification system.")]
        [SerializeField] [TextArea(2, 4)] private string notificationMessage = "Interaction triggered!";

        [Tooltip("If true, the interactor's name will be prepended to the message (e.g., 'PlayerName: Message').")]
        [SerializeField] private bool includePlayerName = false;

        [Header("Effect Settings")]
        [Tooltip("The priority of this effect in the interaction chain. Higher values are executed first.")]
        [SerializeField] private int priority = 0;

        public int Priority => priority;

        #endregion

        #region Public Methods

        public IEnumerator ApplyEffect(GameObject interactor, GameObject interactable)
        {
            // Validate that the notification event is assigned
            if (notificationEvent == null)
            {
                Debug.LogWarning($"SendNotificationEffect on '{gameObject.name}' has no NotificationEvent assigned.", this);
                yield break;
            }

            if (string.IsNullOrEmpty(notificationMessage))
            {
                Debug.LogWarning($"SendNotificationEffect on '{gameObject.name}' has an empty notification message.", this);
                yield break;
            }

            string finalMessage = notificationMessage;

            // Prepend player name if requested
            if (includePlayerName)
            {
                string playerName = "Player";
                if (interactor != null)
                {
                    var playerState = interactor.GetComponent<CorePlayerState>();
                    if (playerState != null)
                    {
                        playerName = playerState.PlayerName;
                    }
                }
                finalMessage = $"{playerName}: {notificationMessage}";
            }

            // Raise the notification event with the configured message
            // clientId is set to 0 as it's not used by CoreHUD's notification system
            notificationEvent.Raise(new NotificationPayload
            {
                clientId = 0,
                message = finalMessage
            });

            yield return null;
        }

        public void CancelEffect(GameObject interactor)
        {
        }

        #endregion
    }
}

