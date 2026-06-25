using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Payload containing notification data including the client ID and message.
    /// </summary>
    [System.Serializable]
    public struct NotificationPayload
    {
        public ulong clientId;
        public string message;
    }

    /// <summary>
    /// A specialized <see cref="GameEvent{T}"/> that carries a <see cref="NotificationPayload"/>.
    /// This event is used to broadcast notifications to the HUD system, allowing any system
    /// to send messages that will be displayed to players.
    /// </summary>
    [CreateAssetMenu(fileName = "NotificationEvent", menuName = "Game Events/Notification Event")]
    public class NotificationEvent : GameEvent<NotificationPayload>
    {
        // This class is intentionally left empty.
        // It inherits all of its functionality from the generic GameEvent<T> base class.
    }
}

