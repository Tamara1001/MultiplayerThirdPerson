using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// A specialized <see cref="GameEvent{T}"/> that carries a <see cref="StatChangePayload"/>.
    /// This asset acts as a central communication channel for broadcasting updates to any gameplay stat (e.g., Health, Stamina).
    /// Components like UI elements can listen to this event to update their displays whenever a stat changes,
    /// without needing a direct reference to the <see cref="CoreStatsHandler"/> that manages the stat.
    /// This promotes a highly decoupled and scalable architecture.
    /// </summary>
    [CreateAssetMenu(fileName = "StatChangeEvent", menuName = "Game Events/Stat Change Event")]
    public class StatChangeEvent : GameEvent<StatChangePayload>
    {
        // This class is intentionally left empty.
        // It inherits all of its functionality from the generic GameEvent<T> base class.
    }

    /// <summary>
    /// A data structure that encapsulates all necessary information for a stat change event.
    /// It contains the stat's name, its new current value, and its maximum value, providing listeners
    /// with all the context they need to react appropriately (e.g., updating a health bar).
    /// </summary>
    [System.Serializable]
    public struct StatChangePayload
    {
        public ulong targetPlayerId;
        public ulong sourcePlayerId;
        public ModificationSource sourceType;
        public string statName;
        public int statID;
        public float currentValue;
        public float maxValue;
    }
}
