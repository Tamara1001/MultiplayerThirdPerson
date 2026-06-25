using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// A specialized <see cref="GameEvent{T}"/> that carries a <see cref="Vector2"/> payload.
    /// This asset acts as a communication channel for broadcasting two-dimensional vector data, most commonly
    /// used for player input like movement from a joystick or look input from a mouse. By using this event,
    /// input-consuming components (like <see cref="CoreMovement"/>) can react to input without needing a direct
    /// reference to the input source (like <see cref="CoreInputHandler"/>), promoting a decoupled architecture.
    /// </summary>
    [CreateAssetMenu(fileName = "Vector2Event", menuName = "Game Events/Vector2 Event")]
    public class Vector2Event : GameEvent<Vector2>
    {
        // This class is intentionally left empty.
        // It inherits all of its functionality from the generic GameEvent<T> base class.
    }
}
