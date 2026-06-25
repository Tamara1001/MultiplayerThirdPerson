using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// A specialized <see cref="GameEvent{T}"/> that carries an <see cref="IntPair"/> payload.
    /// This asset acts as a communication channel for broadcasting two related integer values together,
    /// such as a player's current and maximum ammo, or a score update. Using this event promotes
    /// a decoupled architecture by allowing systems to communicate related numerical data without direct dependencies.
    /// </summary>
    [CreateAssetMenu(fileName = "IntPairEvent", menuName = "Game Events/IntPair Event")]
    public class IntPairEvent : GameEvent<IntPair>
    {
        // This class is intentionally left empty.
        // It inherits all of its functionality from the generic GameEvent<T> base class.
    }

    /// <summary>
    /// A simple data structure used to group two integer values together.
    /// This struct is marked as serializable so it can be easily used and configured in the Unity Inspector
    /// when exposed in other components.
    /// </summary>
    [System.Serializable]
    public struct IntPair
    {
        #region Fields

        /// <summary>
        /// The first integer value in the pair.
        /// </summary>
        public int value1;

        /// <summary>
        /// The second integer value in the pair.
        /// </summary>
        public int value2;

        #endregion
    }
}
