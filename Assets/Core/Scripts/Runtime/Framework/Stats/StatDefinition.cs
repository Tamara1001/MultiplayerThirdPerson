using System;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// A ScriptableObject that defines the template for a single gameplay stat, such as Health, Stamina, or Mana.
    /// This asset contains the base configuration for a stat, including its name, value bounds, and regeneration properties.
    /// These definitions are collected within a <see cref="StatsConfig"/> asset, which is then used by the
    /// <see cref="CoreStatsHandler"/> component to initialize and manage a character's runtime stats.
    /// </summary>
    [CreateAssetMenu(fileName = "NewStatDefinition", menuName = "Stats/Stat Definition")]
    public class StatDefinition : ScriptableObject
    {
        #region Fields & Properties

        /// <summary>
        /// The unique display name of the stat (e.g., "Health", "Stamina"). This name is used as an identifier
        /// in other components and configurations.
        /// </summary>
        [Header("Identity")]
        [Tooltip("The name of the stat, e.g., 'Health', 'Stamina'.")]
        public string statName;

        /// <summary>
        /// The minimum value this stat can reach. This is enforced at runtime and cannot be exceeded downward.
        /// </summary>
        [Header("Value Bounds")]
        [Tooltip("The minimum value this stat can have.")]
        public float minValue = 0f;

        /// <summary>
        /// The absolute maximum value this stat can reach. This defines the upper bound for the stat's value.
        /// Note that runtime modifiers and buffs may temporarily adjust the effective maximum, but this value
        /// serves as the base maximum capacity for the stat.
        /// </summary>
        [Tooltip("The maximum value this stat can have.")]
        public float maxValue = 100f;

        /// <summary>
        /// The value this stat starts with when initialized. This should typically be between <see cref="minValue"/>
        /// and <see cref="maxValue"/>. If set outside these bounds, it will be clamped at runtime.
        /// </summary>
        [Tooltip("The value this stat starts with when initialized.")]
        public float startingValue = 100f;

        /// <summary>
        /// The amount the stat regenerates per second. A value of 0 means the stat does not regenerate automatically.
        /// Positive values cause the stat to increase over time, while negative values cause gradual depletion.
        /// Regeneration only occurs after the <see cref="regenDelay"/> has elapsed since the last stat reduction.
        /// </summary>
        [Header("Regeneration")]
        [Tooltip("The rate at which this stat regenerates per second. 0 = no regen.")]
        public float regenRate = 0f;

        /// <summary>
        /// The delay in seconds after the stat is reduced before regeneration begins.
        /// This prevents regeneration from starting immediately after taking damage or consuming stamina,
        /// creating a more tactical gameplay experience. Set to 0 for instant regeneration after any change.
        /// </summary>
        [Tooltip("Delay in seconds before regeneration starts after the stat is reduced.")]
        public float regenDelay = 3f;

        /// <summary>
        /// Flags that control which events this stat should broadcast when it changes or reaches certain states.
        /// Use this to optimize performance by only broadcasting events that are actively being listened to.
        /// </summary>
        [Header("Events")]
        [Tooltip("Which events this stat should broadcast.")]
        public StatEventFlags eventFlags = StatEventFlags.OnChanged | StatEventFlags.OnDepleted;

        #endregion
    }

    /// <summary>
    /// Flags to control which events a stat broadcasts during gameplay.
    /// Multiple flags can be combined using bitwise OR operations to enable multiple event types.
    /// </summary>
    [Flags]
    public enum StatEventFlags
    {
        /// <summary>
        /// No events will be broadcast by this stat. Use this for performance-critical stats that don't need event callbacks.
        /// </summary>
        None = 0,

        /// <summary>
        /// Broadcast an event whenever the stat's value changes. This includes increases, decreases, and regeneration updates.
        /// </summary>
        OnChanged = 1 << 0,

        /// <summary>
        /// Broadcast an event when the stat reaches its minimum value (typically zero). Useful for triggering death,
        /// stamina depletion effects, or other state changes when a stat is fully consumed.
        /// </summary>
        OnDepleted = 1 << 1
    }
}
