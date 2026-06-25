using UnityEngine;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Implements a burst firing mechanism that fires a fixed number of shots in rapid succession.
    /// When triggered, fires a burst of shots with a delay between each shot, followed by a cooldown period.
    /// Supports optional continuous bursting when the trigger is held down.
    /// </summary>
    public class BurstMechanism : MonoBehaviour, IFiringMechanism
    {
        #region Fields & Properties

        [Header("Burst Settings")]
        [Tooltip("Number of shots fired in each burst.")]
        [SerializeField] private int shotsPerBurst = 3;
        [Tooltip("Time interval between shots within a burst in seconds.")]
        [SerializeField] private float timeBetweenShots = 0.05f;
        [Tooltip("Cooldown time after completing a burst before the next burst can start.")]
        [SerializeField] private float cooldownAfterBurst = 0.5f;
        [Tooltip("If true, holding trigger will continue bursting. If false, trigger must be released and pressed again for each burst.")]
        [SerializeField] private bool allowContinuousBursts = false;

        private int m_CurrentBurstCount;
        private float m_NextShotTime;
        private float m_BurstEndTime;
        private bool m_InBurst;
        private bool m_TriggerHeld;
        private bool m_WaitingForRelease;

        /// <summary>
        /// Gets a value indicating whether this mechanism can start a new burst.
        /// Returns false if currently in a burst, in cooldown, or waiting for trigger release.
        /// </summary>
        public bool CanStartFiring => !m_InBurst && Time.time >= m_BurstEndTime && !m_WaitingForRelease;

        /// <summary>
        /// Gets a value indicating whether this mechanism is currently firing a burst.
        /// </summary>
        public bool IsFiring => m_InBurst;

        /// <summary>
        /// Gets the cooldown time between bursts in seconds.
        /// </summary>
        public float FireRate => cooldownAfterBurst;

        /// <summary>
        /// Gets the number of shots fired in each burst.
        /// </summary>
        public int ShotsPerBurst => shotsPerBurst;

        #endregion

        #region Events

        /// <summary>
        /// Event invoked when the weapon should fire a shot.
        /// This is triggered for each shot within the burst sequence.
        /// </summary>
        public event System.Action OnShouldFire;

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts a new burst sequence if conditions allow.
        /// Tracks trigger state for continuous burst behavior.
        /// </summary>
        public void StartFiring()
        {
            if (CanStartFiring)
            {
                m_InBurst = true;
                m_CurrentBurstCount = 0;
                FireBurstShot();
            }
            m_TriggerHeld = true;
        }

        /// <summary>
        /// Stops firing and releases the trigger state.
        /// Clears the waiting for release flag if continuous bursts are disabled.
        /// </summary>
        public void StopFiring()
        {
            m_TriggerHeld = false;
            if (!allowContinuousBursts)
            {
                m_WaitingForRelease = false;
            }
        }

        /// <summary>
        /// Updates the firing mechanism to process burst shots and handle continuous bursting.
        /// Should be called every frame while the weapon is active.
        /// </summary>
        /// <param name="deltaTime">The time elapsed since the last frame (currently unused).</param>
        public void UpdateFiring(float deltaTime)
        {
            if (m_InBurst && Time.time >= m_NextShotTime)
            {
                FireBurstShot();
            }

            // Automatically start next burst if continuous bursts enabled and trigger still held
            if (!m_InBurst && m_TriggerHeld && allowContinuousBursts && CanStartFiring)
            {
                StartFiring();
            }
        }

        /// <summary>
        /// Resets the firing mechanism to its initial state.
        /// </summary>
        public void Reset()
        {
            m_InBurst = false;
            m_CurrentBurstCount = 0;
            m_TriggerHeld = false;
            m_WaitingForRelease = false;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Fires a single shot within the burst sequence.
        /// Manages burst completion, cooldown timing, and scheduling the next shot.
        /// </summary>
        private void FireBurstShot()
        {
            OnShouldFire?.Invoke();
            m_CurrentBurstCount++;

            if (m_CurrentBurstCount >= shotsPerBurst)
            {
                // Burst complete, enter cooldown
                m_InBurst = false;
                m_BurstEndTime = Time.time + cooldownAfterBurst;

                // Require trigger release before next burst if continuous bursts disabled
                if (!allowContinuousBursts && m_TriggerHeld)
                {
                    m_WaitingForRelease = true;
                }
            }
            else
            {
                // Schedule next shot in the burst
                m_NextShotTime = Time.time + timeBetweenShots;
            }
        }

        #endregion
    }
}
