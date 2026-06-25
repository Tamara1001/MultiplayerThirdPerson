using UnityEngine;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// A ScriptableObject that serves as a container for a collection of <see cref="StatDefinition"/> assets.
    /// This configuration asset defines the complete stat profile for a character or entity, such as a player or an NPC.
    /// It is used by the <see cref="CoreStatsHandler"/> to initialize all runtime stats and to identify the primary
    /// stat (like Health) that determines the entity's "alive" state.
    /// </summary>
    [CreateAssetMenu(fileName = "NewStatsConfig", menuName = "Stats/Stats Configuration")]
    public class StatsConfig : ScriptableObject
    {
        #region Fields

        /// <summary>
        /// A list of all the <see cref="StatDefinition"/> assets that this character will possess.
        /// Each entry in this list will be used to create a <see cref="RuntimeStat"/> instance.
        /// </summary>
        [Tooltip("A list of all stat definitions for a character or entity.")]
        public List<StatDefinition> stats;

        /// <summary>
        /// The name of the stat from the list above that is considered the primary stat for determining life/death.
        /// When the value of this stat reaches zero, the <see cref="CoreStatsHandler"/> will consider the entity to be dead.
        /// This must match the 'statName' of one of the StatDefinitions in the 'stats' list.
        /// </summary>
        [Tooltip("The name of the stat that determines if the entity is alive. If it reaches zero, the entity is considered dead.")]
        public string primaryStatName = "Health";

        /// <summary>
        /// Gets the hash of the primary stat name for efficient comparisons.
        /// </summary>
        public int PrimaryStatHash => Animator.StringToHash(primaryStatName);

        #endregion
    }
}
