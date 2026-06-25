using UnityEngine;
using Unity.Netcode;
using System.Collections;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Platformer
{
    /// <summary>
    /// An interaction effect that tracks the order in which players cross a finish line.
    /// Each player that interacts with this effect receives a finish position (1st, 2nd, 3rd, etc.)
    /// and a notification is sent to all players announcing their placement.
    /// </summary>
    public class FinishLineEffect : NetworkBehaviour, IInteractionEffect
    {
        #region Fields & Properties

        [Header("Effect Settings")]
        [Tooltip("The priority of this effect in the interaction chain. Higher values are executed first.")]
        [SerializeField] private int priority = 0;

        [Header("Event Settings")]
        [Tooltip("The notification event to raise when a player crosses the finish line.")]
        [SerializeField] private NotificationEvent onPlayerFinished;

        /// <summary>
        /// Gets the priority of this effect in the interaction chain. Higher values are executed first.
        /// </summary>
        public int Priority => priority;

        // NetworkVariable to track the next finish position (1st, 2nd, 3rd, etc.)
        // Server has authority, everyone can read the current position
        private readonly NetworkVariable<int> m_FinishPosition = new NetworkVariable<int>(
            1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        #endregion

        #region Public Methods

        /// <summary>
        /// Applies the finish line effect when a player interacts with it.
        /// Requests the player's finish position from the server authority.
        /// </summary>
        /// <param name="interactor">The GameObject representing the player crossing the finish line.</param>
        /// <param name="interactable">The GameObject representing the finish line itself.</param>
        /// <returns>An enumerator for coroutine execution.</returns>
        public IEnumerator ApplyEffect(GameObject interactor, GameObject interactable)
        {
            if (!IsSpawned) yield break;

            ulong clientId = GetClientId(interactor);

            // Request position assignment from server authority
            RequestFinishPositionRpc(clientId);

            // Yield to allow RPC to process
            yield return null;
        }

        /// <summary>
        /// Cancels the effect. No cleanup is needed for this effect.
        /// </summary>
        /// <param name="interactor">The GameObject that was interacting with the finish line.</param>
        public void CancelEffect(GameObject interactor)
        {
            // No cleanup needed for this effect
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// RPC called on the server authority to assign a finish position to the player.
        /// The server atomically assigns the current position and increments the counter.
        /// </summary>
        /// <param name="clientId">The client ID of the player crossing the finish line.</param>
        [Rpc(SendTo.Authority)]
        private void RequestFinishPositionRpc(ulong clientId)
        {
            // Authority assigns current position and increments for next player
            int position = m_FinishPosition.Value;
            m_FinishPosition.Value++;

            string playerName = GetPlayerName(clientId);
            string positionText = GetPositionText(position);
            string message = $"{playerName} finished {positionText}!";

            // Broadcast finish notification to all clients
            ShowFinishNotificationRpc(clientId, message);
        }

        /// <summary>
        /// RPC called on all clients to display the finish notification.
        /// </summary>
        /// <param name="clientId">The client ID of the player who finished.</param>
        /// <param name="message">The formatted finish message to display.</param>
        [Rpc(SendTo.Everyone)]
        private void ShowFinishNotificationRpc(ulong clientId, string message)
        {
            if (onPlayerFinished != null)
            {
                onPlayerFinished.Raise(new NotificationPayload
                {
                    clientId = clientId,
                    message = message
                });
            }
        }

        /// <summary>
        /// Gets the client ID from the interactor <see cref="GameObject"/> by extracting it from the <see cref="NetworkObject"/> component.
        /// </summary>
        /// <param name="interactor">The GameObject to extract the client ID from.</param>
        /// <returns>The owner client ID, or 0 if no NetworkObject component is found.</returns>
        private ulong GetClientId(GameObject interactor)
        {
            if (interactor.TryGetComponent<NetworkObject>(out var netObj))
            {
                return netObj.OwnerClientId;
            }
            return 0;
        }

        /// <summary>
        /// Gets the player name from <see cref="CorePlayerState"/> component.
        /// Falls back to a default name format if the player name is unavailable.
        /// </summary>
        /// <param name="clientId">The client ID of the player.</param>
        /// <returns>The player's name, or "Player-{clientId}" if the name cannot be retrieved.</returns>
        private string GetPlayerName(ulong clientId)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null)
            {
                var playerObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
                if (playerObject != null && playerObject.TryGetComponent<CorePlayerState>(out var playerState))
                {
                    string playerName = playerState.PlayerName;
                    if (!string.IsNullOrEmpty(playerName))
                    {
                        return playerName;
                    }
                }
            }
            return $"Player-{clientId}";
        }

        /// <summary>
        /// Converts a numeric position to its ordinal text representation (1st, 2nd, 3rd, etc.).
        /// </summary>
        /// <param name="position">The numeric position to convert.</param>
        /// <returns>The ordinal text representation of the position.</returns>
        private string GetPositionText(int position)
        {
            if (position <= 0) return "0th";

            // Handle special cases where "th" is used (11th, 12th, 13th, 111th, 112th, 113th, etc.)
            int lastTwoDigits = position % 100;
            if (lastTwoDigits >= 11 && lastTwoDigits <= 13)
            {
                return $"{position}th";
            }

            // Handle regular cases based on last digit
            int lastDigit = position % 10;
            return lastDigit switch
            {
                1 => $"{position}st",
                2 => $"{position}nd",
                3 => $"{position}rd",
                _ => $"{position}th"
            };
        }

        #endregion
    }
}

