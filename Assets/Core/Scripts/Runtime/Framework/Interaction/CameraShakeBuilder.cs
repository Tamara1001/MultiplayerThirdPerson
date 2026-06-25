using UnityEngine;
using Unity.Cinemachine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// A fluent builder for creating camera shake effects using Cinemachine Impulse Source.
    /// </summary>
    public class CameraShakeBuilder
    {
        #region Fields & Properties

        private readonly CinemachineImpulseSource m_ImpulseSource;

        private CinemachineImpulseDefinition.ImpulseShapes m_ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
        private CinemachineImpulseDefinition.ImpulseTypes m_ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
        private float m_ImpulseDuration = 0.2f;
        private Vector3 m_Velocity = Vector3.one;
        private Vector3 m_Position = Vector3.zero;
        private bool m_UsePosition = false;

        #endregion

        #region Public Methods

        /// <summary>
        /// Constructor for camera shake builder
        /// </summary>
        public CameraShakeBuilder(CinemachineImpulseSource impulseSource)
        {
            m_ImpulseSource = impulseSource;
        }

        /// <summary>
        /// Sets the duration of the impulse in seconds
        /// </summary>
        public CameraShakeBuilder WithDuration(float duration)
        {
            m_ImpulseDuration = duration;
            return this;
        }

        /// <summary>
        /// Sets the velocity/intensity of the shake
        /// </summary>
        public CameraShakeBuilder WithVelocity(Vector3 velocity)
        {
            m_Velocity = velocity;
            return this;
        }

        /// <summary>
        /// Sets uniform velocity/intensity for all axes
        /// </summary>
        public CameraShakeBuilder WithVelocity(float uniformVelocity)
        {
            m_Velocity = Vector3.one * uniformVelocity;
            return this;
        }

        /// <summary>
        /// Sets the world position where the impulse originates (for distance-based attenuation)
        /// </summary>
        public CameraShakeBuilder AtPosition(Vector3 position)
        {
            m_Position = position;
            m_UsePosition = true;
            return this;
        }

        /// <summary>
        /// Sets the impulse definition parameters (shape, type, and duration) in one call
        /// </summary>
        public CameraShakeBuilder WithImpulseDefinition(CinemachineImpulseDefinition.ImpulseShapes shape, CinemachineImpulseDefinition.ImpulseTypes type, float duration)
        {
            m_ImpulseShape = shape;
            m_ImpulseType = type;
            m_ImpulseDuration = duration;
            return this;
        }

        /// <summary>
        /// Finalizes the configuration and triggers the camera shake impulse
        /// </summary>
        public void Execute()
        {
            // Validate impulse source before attempting to execute
            if (m_ImpulseSource == null)
            {
                Debug.LogError("CameraShakeBuilder: ImpulseSource is null! Cannot execute shake.");
                return;
            }

            // Apply all configured parameters to the impulse definition
            m_ImpulseSource.ImpulseDefinition.ImpulseShape = m_ImpulseShape;
            m_ImpulseSource.ImpulseDefinition.ImpulseType = m_ImpulseType;
            m_ImpulseSource.ImpulseDefinition.ImpulseDuration = m_ImpulseDuration;

            // Generate impulse at specific position for distance-based attenuation, or globally if no position is set
            if (m_UsePosition)
            {
                m_ImpulseSource.GenerateImpulseAtPositionWithVelocity(m_Position, m_Velocity);
            }
            else
            {
                m_ImpulseSource.GenerateImpulseWithVelocity(m_Velocity);
            }
        }

        #endregion
    }
}
