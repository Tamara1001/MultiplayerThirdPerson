using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// A specialized <see cref="GameEvent{T}"/> that carries a <see cref="StatDepletedPayload"/>.
    /// This asset acts as a communication channel for broadcasting when a specific stat reaches zero,
    /// allowing systems to react to stat depletion events (like triggering elimination, status effects, etc.)
    /// without needing direct references to the <see cref="CoreStatsHandler"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "StatDepletedEvent", menuName = "Game Events/Stat Depleted Event")]
    public class StatDepletedEvent : GameEvent<StatDepletedPayload>
    {
        // This class is intentionally left empty.
        // It inherits all of its functionality from the generic GameEvent<T> base class.
    }

    /// <summary>
    /// A data structure that encapsulates all necessary information for a stat depleted event.
    /// </summary>
    [System.Serializable]
    public struct StatDepletedPayload
    {
        public ulong playerId;
        public string statName;
        public int statID;
    }
}
