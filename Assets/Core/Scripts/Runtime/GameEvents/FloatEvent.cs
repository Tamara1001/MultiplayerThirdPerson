using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// A specialized <see cref="GameEvent{T}"/> that carries a floating-point (`float`) payload.
    /// This asset acts as a communication channel for broadcasting numerical values, such as a reload duration,
    /// a weapon's current spread angle, or any other continuous value. Using this event allows various systems
    /// to react to these changes without being tightly coupled to the source.
    /// </summary>
    [CreateAssetMenu(fileName = "FloatEvent", menuName = "Game Events/Float Event")]
    public class FloatEvent : GameEvent<float>
    {
        // This class is intentionally left empty.
        // It inherits all of its functionality from the generic GameEvent<T> base class.
    }
}
