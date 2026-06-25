using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// A specialized <see cref="GameEvent{T}"/> that carries a <see cref="GameObject"/> payload.
    /// This asset serves as a communication channel for broadcasting references to specific GameObjects,
    /// such as the player object that died, a newly spawned item, or a target that was just selected.
    /// It enables a decoupled architecture, allowing different systems to react to events concerning GameObjects
    /// without needing direct references to the event source.
    /// </summary>
    [CreateAssetMenu(fileName = "GameObjectEvent", menuName = "Game Events/GameObject Event")]
    public class GameObjectEvent : GameEvent<GameObject>
    {
        // This class is intentionally left empty.
        // It inherits all of its functionality from the generic GameEvent<T> base class.
    }
}
