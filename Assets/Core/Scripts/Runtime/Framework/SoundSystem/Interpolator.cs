using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// A utility class for smoothly interpolating float values over time using various curve types.
    /// This class provides an easy-to-use interface for creating animated transitions between values,
    /// supporting different easing curves for more natural motion. Commonly used for animating UI elements,
    /// audio parameters, and other smooth value transitions in the gameplay systems.
    /// </summary>
    public class Interpolator
    {
        #region Enums

        /// <summary>
        /// Defines the available curve types for interpolation, each providing different easing characteristics.
        /// </summary>
        public enum CurveType
        {
            /// <summary>
            /// No interpolation applied; returns 0.
            /// </summary>
            None,
            /// <summary>
            /// Linear interpolation with constant speed.
            /// </summary>
            Linear,
            /// <summary>
            /// Starts slow and accelerates towards the end.
            /// </summary>
            SmoothDeparture,
            /// <summary>
            /// Starts fast and decelerates towards the end.
            /// </summary>
            SmoothArrival,
            /// <summary>
            /// Smooth acceleration at the start and deceleration at the end.
            /// </summary>
            SmoothStep
        }

        #endregion

        #region Fields & Properties

        /// <summary>
        /// Gets the target value that the interpolator is moving towards.
        /// </summary>
        public float TargetValue { get; private set; }

        // Internal state for interpolation calculations.
        private float m_StartTime;
        private float m_StartValue;
        private float m_TargetTime;
        private readonly CurveType m_Type;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new Interpolator with a default starting value of 0 and Linear curve type.
        /// </summary>
        public Interpolator()
        {
            m_Type = CurveType.Linear;
            SetValue(0);
        }

        /// <summary>
        /// Initializes a new Interpolator with the specified starting value and curve type.
        /// </summary>
        /// <param name="startValue">The initial value for the interpolator.</param>
        /// <param name="type">The curve type to use for interpolation.</param>
        public Interpolator(float startValue, CurveType type)
        {
            m_Type = type;
            SetValue(startValue);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Sets the interpolator to a specific value immediately, stopping any ongoing interpolation.
        /// </summary>
        /// <param name="value">The value to set immediately.</param>
        public void SetValue(float value)
        {
            m_StartValue = value;
            TargetValue = value;
            m_StartTime = 0;
            m_TargetTime = 0;
        }

        /// <summary>
        /// Starts interpolating from the current value to the specified target over the given duration.
        /// </summary>
        /// <param name="target">The target value to interpolate to.</param>
        /// <param name="time">The duration of the interpolation in seconds.</param>
        public void MoveTo(float target, float time)
        {
            m_StartValue = GetValue();
            TargetValue = target;
            m_StartTime = Time.realtimeSinceStartup;
            m_TargetTime = m_StartTime + time;
        }

        /// <summary>
        /// Gets the current interpolated value based on the elapsed time and curve type.
        /// </summary>
        /// <returns>The current interpolated value.</returns>
        public float GetValue()
        {
            var now = Time.realtimeSinceStartup;
            var timeToLive = m_TargetTime - now;
            if (timeToLive <= 0.0f)
                return TargetValue;

            var t = (now - m_StartTime) / (m_TargetTime - m_StartTime);
            t = GetNormalizedCurveValue(m_Type, t);
            return m_StartValue + (TargetValue - m_StartValue) * t;
        }

        /// <summary>
        /// Gets a normalized curve value (0-1) for the specified curve type at the given time parameter.
        /// This method can be used independently for curve calculations without creating an Interpolator instance.
        /// </summary>
        /// <param name="curveType">The type of curve to evaluate.</param>
        /// <param name="t">The normalized time parameter (0-1).</param>
        /// <returns>The curve-adjusted value between 0 and 1.</returns>
        public float GetNormalizedCurveValue(CurveType curveType, float t)
        {
            switch (curveType)
            {
                default:
                case CurveType.None:
                    return 0;
                case CurveType.Linear:
                    return t;
                case CurveType.SmoothArrival:
                    var s = 1.0f - t;
                    return s * s * s * s;
                case CurveType.SmoothDeparture:
                    return t * t * t * t;
                case CurveType.SmoothStep:
                    return t * t * (3.0f - 2.0f * t);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the interpolator is currently moving towards its target.
        /// </summary>
        /// <returns>True if currently interpolating, false if at the target value.</returns>
        public bool IsMoving()
        {
            return Time.realtimeSinceStartup < m_TargetTime;
        }

        /// <summary>
        /// Gets the direction of the current interpolation movement.
        /// </summary>
        /// <returns>1 if moving in positive direction, -1 if moving in negative direction, 0 if not moving.</returns>
        public float Direction()
        {
            return Mathf.Sign(TargetValue - m_StartValue);
        }

        /// <summary>
        /// Stops the current interpolation immediately, setting both start and target values to the current position.
        /// </summary>
        public void Stop()
        {
            m_StartValue = TargetValue = GetValue();
            m_TargetTime = 0;
            m_StartTime = 0;
        }

        #endregion
    }
}
