using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// A specialized <see cref="GameEvent{T}"/> that carries a boolean (`bool`) payload.
    /// This asset acts as a communication channel for broadcasting true/false state changes throughout the application,
    /// such as toggling sprint status or aiming mode. By using this event, components can signal state changes
    /// without needing direct references to the components that will react to them, promoting a decoupled architecture.
    /// </summary>
    [CreateAssetMenu(fileName = "BoolEvent", menuName = "Game Events/Bool Event")]
    public class BoolEvent : GameEvent<bool>
    {
        // This class is intentionally left empty.
        // It inherits all of its functionality from the generic GameEvent<T> base class.
    }
}
