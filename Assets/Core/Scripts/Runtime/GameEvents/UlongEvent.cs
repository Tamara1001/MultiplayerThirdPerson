using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// A specialized GameEvent that carries a ulong payload, typically used for sending
    /// a NetworkObjectId or ClientId.
    /// </summary>
    [CreateAssetMenu(fileName = "UlongEvent", menuName = "Game Events/Ulong Event")]
    public class UlongEvent : GameEvent<ulong>
    {
        // This class is intentionally left empty.
        // It inherits all of its functionality from the generic GameEvent<T> base class.
    }
}
