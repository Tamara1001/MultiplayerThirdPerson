using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Standard linear projectile movement in a straight line.
    /// Supports both continuous movement mode (updated each FixedUpdate) and physics impulse mode.
    /// Includes optional world boundary checking with distributed performance optimization.
    /// </summary>
    public class StraightLineMovement : NetworkBehaviour, IMovementBehavior
    {
        #region Fields & Properties

        [Header("Movement Settings")]
        [Tooltip("Velocity rate applied per second when using continuous movement mode.")]
        [SerializeField] private float velocityRate = 15f;

        [Tooltip("When enabled, projectile will bounce back when hitting world boundary.")]
        [SerializeField] private bool enableBoundary = true;

        [Tooltip("When true, continuously updates projectile position in FixedUpdate. When false, applies initial impulse and lets physics handle movement.")]
        [SerializeField] private bool useContinuousMovement = true;

        [Tooltip("Force applied when using physics impulse mode.")]
        [SerializeField] private float initialForce = 30f;

        private static GameObject s_WorldBoundary;
        private static float s_WorldBoundaryRadius = 1024.0f;
        private const float k_BoundaryCheckFrequency = 0.06667f;
        private const int k_BoundaryCheckDistributionRes = 10;
        private static int s_BoundaryOffsetCount = 1;
        private float m_NextBoundaryCheck;

        private Vector3 m_InitialVelocity;
        private Vector3 m_StartPosition;
        private Quaternion m_StartRotation;
        private Vector3 m_InitialDirection;

        private Rigidbody m_Rigidbody;
        private NetworkRigidbody m_NetworkRigidbody;
        private ModularProjectile m_Projectile;

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the movement behavior with the parent projectile reference.
        /// Caches component references and locates the world boundary object.
        /// </summary>
        /// <param name="projectile">The parent modular projectile.</param>
        public void Initialize(ModularProjectile projectile)
        {
            m_Projectile = projectile;

            // Locate and cache world boundary on first initialization
            if (s_WorldBoundary == null)
            {
                s_WorldBoundary = GameObject.Find("WorldBoundary");
                if (s_WorldBoundary != null)
                {
                    var sphereCollider = s_WorldBoundary.GetComponent<SphereCollider>();
                    s_WorldBoundaryRadius = sphereCollider.radius;
                }
            }

            m_Rigidbody = GetComponent<Rigidbody>();
            m_NetworkRigidbody = GetComponent<NetworkRigidbody>();
            SetNextBoundaryCheck(init: true);
        }

        /// <summary>
        /// Configures the initial movement parameters for the projectile.
        /// Sets starting position, direction, and rotation based on the provided values.
        /// </summary>
        /// <param name="position">Starting position of the projectile.</param>
        /// <param name="direction">Direction vector for projectile movement.</param>
        /// <param name="initialVelocity">Additional velocity to add to the base velocity rate.</param>
        public void SetupMovement(Vector3 position, Vector3 direction, Vector3 initialVelocity)
        {
            m_StartPosition = position;
            m_InitialVelocity = initialVelocity;
            m_InitialDirection = direction;

            if (direction != Vector3.zero)
            {
                m_StartRotation = Quaternion.LookRotation(direction);
            }
            else
            {
                m_StartRotation = Quaternion.identity;
                Debug.LogWarning($"StraightLineMovement.SetupMovement called with zero direction vector. Using Quaternion.identity.");
            }

            if (m_Rigidbody != null)
            {
                m_Rigidbody.position = position;
                m_Rigidbody.rotation = m_StartRotation;
            }
            else
            {
                transform.position = position;
                transform.rotation = m_StartRotation;
            }
        }

        /// <summary>
        /// Applies the initial state to the projectile.
        /// In physics impulse mode, applies the initial force to the rigidbody.
        /// In continuous mode, just sets the starting transform.
        /// </summary>
        public void ApplyInitialState()
        {
            if (m_Projectile.CanCommitToTransform && !m_Projectile.ignoreStartValues)
            {
                m_Projectile.SetState(m_StartPosition, m_StartRotation, null, false);

                // Combine configured force with initial velocity for total impulse
                if (!useContinuousMovement)
                {
                    float totalForceMagnitude = initialForce + m_InitialVelocity.magnitude;
                    Vector3 forceVector = m_InitialDirection * totalForceMagnitude;
                    m_Rigidbody.AddForce(forceVector, ForceMode.Impulse);
                    m_Rigidbody.angularVelocity = Vector3.zero;
                }
            }
        }

        /// <summary>
        /// Updates the projectile's position when using continuous movement mode.
        /// Called from FixedUpdate. Only executes if using continuous mode and has network authority.
        /// </summary>
        public void UpdateMovement()
        {
            if (!useContinuousMovement || !IsSpawned || !m_Projectile.CanCommitToTransform)
                return;

            // Calculate target position based on forward direction and combined velocity
            var target = transform.position + (transform.forward * (velocityRate + m_InitialVelocity.magnitude));
            m_NetworkRigidbody.MovePosition(Vector3.Lerp(transform.position, target, Time.fixedDeltaTime));
        }

        /// <summary>
        /// Checks if the projectile has exceeded the world boundary and redirects it back toward center.
        /// Uses distributed timing to avoid all projectiles checking on the same frame.
        /// </summary>
        public void CheckBoundary()
        {
            if (m_Rigidbody == null || (m_Rigidbody != null && m_Rigidbody.isKinematic))
            {
                return;
            }

            if (enableBoundary && m_NextBoundaryCheck < Time.realtimeSinceStartup)
            {
                var distance = Vector3.Distance(Vector3.zero, transform.position);
                if (distance >= s_WorldBoundaryRadius)
                {
                    // Redirect velocity toward world center while maintaining speed
                    var dir = Vector3.zero - transform.position;
                    var velocity = GetCurrentVelocity();
                    velocity = velocity.magnitude * dir;
                    SetVelocity(velocity);
                }
                else
                {
                    SetNextBoundaryCheck();
                }
            }
        }

        /// <summary>
        /// Enables or disables boundary checking for this projectile.
        /// </summary>
        /// <param name="boundaryEnabled">True to enable boundary checking, false to disable.</param>
        public void SetBoundaryEnabled(bool boundaryEnabled)
        {
            enableBoundary = boundaryEnabled;
        }

        /// <summary>
        /// Gets the current velocity of the projectile from its rigidbody.
        /// </summary>
        /// <returns>Current linear velocity, or Vector3.zero if no rigidbody exists.</returns>
        public Vector3 GetCurrentVelocity()
        {
            if (m_Rigidbody != null)
            {
                return m_Rigidbody.linearVelocity;
            }

            return Vector3.zero;
        }

        /// <summary>
        /// Sets the velocity of the projectile's rigidbody.
        /// </summary>
        /// <param name="velocity">The velocity vector to apply.</param>
        public void SetVelocity(Vector3 velocity)
        {
            if (m_Rigidbody != null)
            {
                m_Rigidbody.linearVelocity = velocity;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Schedules the next boundary check time.
        /// On initialization, distributes checks across frames to reduce performance spikes.
        /// </summary>
        /// <param name="timeOffset">Time delay until next check.</param>
        /// <param name="init">True if this is the initial setup, false for subsequent checks.</param>
        private void SetNextBoundaryCheck(float timeOffset = k_BoundaryCheckFrequency, bool init = false)
        {
            if (init)
            {
                // Distribute boundary checks across multiple frames using modulo distribution
                s_BoundaryOffsetCount++;
                m_NextBoundaryCheck = Time.realtimeSinceStartup +
                                      (timeOffset * (s_BoundaryOffsetCount % k_BoundaryCheckDistributionRes));
            }
            else
            {
                m_NextBoundaryCheck = Time.realtimeSinceStartup + timeOffset;
            }
        }

        #endregion

        #region Unity Methods

        private void FixedUpdate()
        {
            UpdateMovement();
        }

        #endregion
    }
}
