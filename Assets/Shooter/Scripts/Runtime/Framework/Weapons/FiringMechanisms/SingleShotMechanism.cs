using System;
using UnityEngine;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Implements a single-shot firing mechanism that fires one shot per trigger press.
    /// Supports configurable fire rate cooldown and optional trigger release requirement.
    /// Unlike automatic or burst mechanisms, this fires instantly when triggered rather than over time.
    /// </summary>
    public class SingleShotMechanism : MonoBehaviour, IFiringMechanism
    {
        #region Fields & Properties

        [Header("Single Shot Settings")]
        [Tooltip("Minimum time interval between shots in seconds.")]
        [SerializeField] private float fireRate = 0.5f;
        [Tooltip("If true, must release and press trigger again to fire. If false, can hold trigger to fire repeatedly (respecting fire rate).")]
        [SerializeField] private bool requireTriggerRelease = true;

        private float m_NextFireTime;
        private bool m_TriggerHeld;
        private bool m_HasFired;

        /// <summary>
        /// Gets a value indicating whether this mechanism can start firing.
        /// Returns false if on cooldown or if trigger release is required but trigger is still held.
        /// </summary>
        public bool CanStartFiring => Time.time >= m_NextFireTime && (!requireTriggerRelease || !m_TriggerHeld);

        /// <summary>
        /// Gets a value indicating whether this mechanism is currently firing.
        /// Always returns false for single-shot as firing is instantaneous rather than continuous.
        /// </summary>
        public bool IsFiring => false;

        /// <summary>
        /// Gets the minimum time interval between shots in seconds.
        /// </summary>
        public float FireRate => fireRate;

        #endregion

        #region Events

        /// <summary>
        /// Event invoked when the weapon should fire a shot.
        /// For single-shot, this is triggered immediately when <see cref="StartFiring"/> is called.
        /// </summary>
        public event Action OnShouldFire;

        #endregion

        #region Public Methods

        /// <summary>
        /// Fires a single shot if conditions allow and tracks trigger state.
        /// The shot fires immediately if cooldown has elapsed and the mechanism hasn't already fired this trigger press.
        /// </summary>
        public void StartFiring()
        {
            if (CanStartFiring && !m_HasFired)
            {
                OnShouldFire?.Invoke();
                m_NextFireTime = Time.time + fireRate;
                m_HasFired = true;
            }
            m_TriggerHeld = true;
        }

        /// <summary>
        /// Releases the trigger state, allowing the next shot to be fired.
        /// </summary>
        public void StopFiring()
        {
            m_TriggerHeld = false;
            m_HasFired = false;
        }

        /// <summary>
        /// Updates the firing mechanism logic.
        /// Single-shot mechanism does not require per-frame updates as firing is instantaneous.
        /// </summary>
        /// <param name="deltaTime">The time elapsed since the last frame (unused).</param>
        public void UpdateFiring(float deltaTime)
        {
        }

        /// <summary>
        /// Resets the firing mechanism to its initial state.
        /// </summary>
        public void Reset()
        {
            m_TriggerHeld = false;
            m_HasFired = false;
        }

        #endregion
    }
}
