using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// A specialized <see cref="GameEvent{T}"/> that carries a <see cref="FloatPair"/> payload.
    /// This asset provides a communication channel for broadcasting two related float values together,
    /// such as the current and maximum values of a progress bar, or a min/max range. This promotes
    /// a decoupled architecture by allowing systems to communicate related numerical data without direct dependencies.
    /// </summary>
    [CreateAssetMenu(fileName = "FloatPairEvent", menuName = "Game Events/FloatPair Event")]
    public class FloatPairEvent : GameEvent<FloatPair>
    {
        // This class is intentionally left empty.
        // It inherits all of its functionality from the generic GameEvent<T> base class.
    }

    /// <summary>
    /// A simple data structure used to group two floating-point values together.
    /// This struct is marked as serializable so it can be easily used and configured in the Unity Inspector
    /// when exposed in other components.
    /// </summary>
    [System.Serializable]
    public struct FloatPair
    {
        #region Fields

        /// <summary>
        /// The first float value in the pair.
        /// </summary>
        public float value1;

        /// <summary>
        /// The second float value in the pair.
        /// </summary>
        public float value2;

        #endregion
    }
}
