using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Arc-based projectile movement affected by gravity, ideal for grenades and lobbed projectiles
    /// </summary>
    public class ArcMovement : NetworkBehaviour, IMovementBehavior
    {
        #region Fields & Properties

        [Header("Arc Settings")]
        [Tooltip("Initial speed of the projectile")]
        [SerializeField] private float initialSpeed = 15f;
        [Tooltip("Height of the arc trajectory")]
        [SerializeField] private float arcHeight = 1.5f;
        [Tooltip("Multiplier for gravity strength")]
        [SerializeField] private float gravityMultiplier = 1.0f;
        [Tooltip("Enable boundary checking to keep projectile within world bounds")]
        [SerializeField] private bool enableBoundary = true;
        [Tooltip("When false, uses physics-driven trajectory instead of continuous movement")]
        [SerializeField] private bool useContinuousMovement = false;

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
        private Vector3 m_GravityVector;

        private Rigidbody m_Rigidbody;
        private NetworkRigidbody m_NetworkRigidbody;
        private ModularProjectile m_Projectile;

        #endregion

        #region Public Methods

        public void Initialize(ModularProjectile projectile)
        {
            m_Projectile = projectile;

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

            m_GravityVector = Physics.gravity * gravityMultiplier;
        }

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
                Debug.LogWarning($"ArcMovement.SetupMovement called with zero direction vector. Using Quaternion.identity.");
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

        public void ApplyInitialState()
        {
            if (m_Projectile.CanCommitToTransform && !m_Projectile.ignoreStartValues)
            {
                m_Projectile.SetState(m_StartPosition, m_StartRotation, null, false);

                if (!useContinuousMovement && m_Rigidbody != null)
                {
                    float totalSpeed = initialSpeed + m_InitialVelocity.magnitude;

                    Vector3 launchVelocity = m_InitialDirection * totalSpeed;
                    launchVelocity += Vector3.up * arcHeight;

                    m_Rigidbody.useGravity = true;
                    m_Rigidbody.AddForce(launchVelocity, ForceMode.Impulse);
                }
                else if (m_Rigidbody != null)
                {
                    m_Rigidbody.useGravity = false;
                }
            }
        }

        public void UpdateMovement()
        {
            if (!useContinuousMovement || !IsSpawned || !m_Projectile.CanCommitToTransform)
            {
                return;
            }

            if (m_Rigidbody != null)
            {
                Vector3 currentVelocity = GetCurrentVelocity();
                currentVelocity += m_GravityVector * Time.fixedDeltaTime;
                SetVelocity(currentVelocity);

                if (currentVelocity.magnitude > 0.1f)
                {
                    m_Rigidbody.rotation = Quaternion.LookRotation(currentVelocity.normalized);
                }
            }
        }

        public void CheckBoundary()
        {
            if (m_Rigidbody == null || (m_Rigidbody != null && m_Rigidbody.isKinematic))
            {
                return;
            }

            if (enableBoundary && m_NextBoundaryCheck < Time.realtimeSinceStartup)
            {
                var distance = Vector3.Distance(Vector3.zero, transform.position);
                if (distance >= s_WorldBoundaryRadius && OnBoundaryReached())
                {
                    // Reflect velocity to bounce projectile back toward world center
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

        public void SetBoundaryEnabled(bool boundaryEnabled)
        {
            enableBoundary = boundaryEnabled;
        }

        public Vector3 GetCurrentVelocity()
        {
            if (m_Rigidbody != null)
            {
                return m_Rigidbody.linearVelocity;
            }

            return Vector3.zero;
        }

        public void SetVelocity(Vector3 velocity)
        {
            if (m_Rigidbody != null)
            {
                m_Rigidbody.linearVelocity = velocity;
            }
        }

        #endregion

        #region Private Methods

        private bool OnBoundaryReached()
        {
            return true;
        }

        // Distributes boundary checks across frames to avoid performance spikes when many projectiles exist
        private void SetNextBoundaryCheck(float timeOffset = k_BoundaryCheckFrequency, bool init = false)
        {
            if (init)
            {
                // Stagger initial checks using modulo to distribute across frames
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
