using UnityEngine;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Implements an automatic firing mechanism that fires continuously while the trigger is held.
    /// The mechanism supports configurable fire rate and an optional initial delay before the first shot.
    /// </summary>
    public class AutomaticMechanism : MonoBehaviour, IFiringMechanism
    {
        #region Fields & Properties

        [Header("Automatic Settings")]
        [Tooltip("Time interval between consecutive shots in seconds.")]
        [SerializeField] private float fireRate = 0.1f;
        [Tooltip("Delay before starting automatic fire (0 = instant).")]
        [SerializeField] private float initialDelay = 0f;

        private float m_NextFireTime;
        private bool m_IsFiring;
        private bool m_HasStartedFiring;

        /// <summary>
        /// Gets a value indicating whether this mechanism can start firing.
        /// Automatic mechanism can always start firing.
        /// </summary>
        public bool CanStartFiring => true;

        /// <summary>
        /// Gets a value indicating whether this mechanism is currently firing.
        /// </summary>
        public bool IsFiring => m_IsFiring;

        /// <summary>
        /// Gets the time interval between consecutive shots in seconds.
        /// </summary>
        public float FireRate => fireRate;

        #endregion

        #region Events

        /// <summary>
        /// Event invoked when the weapon should fire a shot.
        /// This is triggered automatically based on the fire rate while the mechanism is active.
        /// </summary>
        public event System.Action OnShouldFire;

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the automatic firing sequence.
        /// If this is the first time firing since the last stop, applies the initial delay.
        /// </summary>
        public void StartFiring()
        {
            m_IsFiring = true;
            if (!m_HasStartedFiring)
            {
                // Apply initial delay only on the first shot
                m_NextFireTime = Time.time + initialDelay;
                m_HasStartedFiring = true;
            }
        }

        /// <summary>
        /// Stops the automatic firing sequence and resets the firing state.
        /// </summary>
        public void StopFiring()
        {
            m_IsFiring = false;
            m_HasStartedFiring = false;
        }

        /// <summary>
        /// Updates the firing mechanism and triggers shots based on the fire rate.
        /// Should be called every frame while the weapon is active.
        /// </summary>
        /// <param name="deltaTime">The time elapsed since the last frame (currently unused).</param>
        public void UpdateFiring(float deltaTime)
        {
            if (m_IsFiring && Time.time >= m_NextFireTime)
            {
                OnShouldFire?.Invoke();
                m_NextFireTime = Time.time + fireRate;
            }
        }

        /// <summary>
        /// Resets the firing mechanism to its initial state.
        /// </summary>
        public void Reset()
        {
            m_IsFiring = false;
            m_HasStartedFiring = false;
            m_NextFireTime = 0f;
        }

        #endregion
    }
}
