using UnityEngine;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Manages weapon spread mechanics, including spread increase per shot and gradual recovery over time.
    /// Spread affects weapon accuracy by adding randomness to projectile direction.
    /// The spread increases with each shot and recovers to minimum after a delay period.
    /// </summary>
    public class SpreadHandler : MonoBehaviour
    {
        #region Fields & Properties

        [Header("Broadcasting on")]
        [Tooltip("Event raised when the weapon spread angle changes. Contains the current spread angle.")]
        [SerializeField] private FloatEvent onWeaponSpreadChanged;

        private float m_MinSpreadAngle;
        private float m_MaxSpreadAngle;
        private float m_TimeSinceLastShot;
        private float m_SpreadRecoveryRate;
        private float m_CurrentSpreadAngle;
        private float m_SpreadRecoveryDelay;
        private float m_SpreadIncreasePerShot;

        /// <summary>
        /// Gets the current spread angle in degrees.
        /// This value affects the accuracy of projectiles fired from the weapon.
        /// </summary>
        public float CurrentSpreadAngle => m_CurrentSpreadAngle;

        #endregion

        #region Unity Methods

        private void Update()
        {
            m_TimeSinceLastShot += Time.deltaTime;

            // Only recover spread after the delay period has elapsed
            if (m_TimeSinceLastShot >= m_SpreadRecoveryDelay && m_CurrentSpreadAngle > m_MinSpreadAngle)
            {
                m_CurrentSpreadAngle -= m_SpreadRecoveryRate * Time.deltaTime;
                // Clamp to minimum spread to prevent overshooting
                m_CurrentSpreadAngle = Mathf.Max(m_CurrentSpreadAngle, m_MinSpreadAngle);
                onWeaponSpreadChanged?.Raise(m_CurrentSpreadAngle);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the spread handler with weapon-specific spread configuration.
        /// This should be called by the weapon during setup.
        /// </summary>
        /// <param name="minSpread">The minimum spread angle in degrees (best accuracy).</param>
        /// <param name="maxSpread">The maximum spread angle in degrees (worst accuracy).</param>
        /// <param name="spreadIncrease">The amount of spread added per shot in degrees.</param>
        /// <param name="recoveryRate">The rate at which spread recovers per second in degrees.</param>
        /// <param name="recoveryDelay">The delay in seconds before spread starts recovering after shooting.</param>
        public void Initialize(float minSpread, float maxSpread, float spreadIncrease, float recoveryRate, float recoveryDelay)
        {
            m_MinSpreadAngle = minSpread;
            m_MaxSpreadAngle = maxSpread;
            m_SpreadIncreasePerShot = spreadIncrease;
            m_SpreadRecoveryRate = recoveryRate;
            m_SpreadRecoveryDelay = recoveryDelay;

            m_CurrentSpreadAngle = m_MinSpreadAngle;
            m_TimeSinceLastShot = m_SpreadRecoveryDelay;
        }

        /// <summary>
        /// Increases the weapon spread by the configured amount per shot.
        /// This should be called each time the weapon fires.
        /// Resets the recovery delay timer to prevent immediate recovery.
        /// </summary>
        public void IncreaseSpread()
        {
            m_CurrentSpreadAngle += m_SpreadIncreasePerShot;
            // Clamp to maximum spread to prevent exceeding weapon limits
            m_CurrentSpreadAngle = Mathf.Min(m_CurrentSpreadAngle, m_MaxSpreadAngle);
            m_TimeSinceLastShot = 0f;
            onWeaponSpreadChanged?.Raise(m_CurrentSpreadAngle);
        }

        /// <summary>
        /// Immediately resets the spread to minimum accuracy.
        /// This is typically called when the weapon is unequipped or holstered.
        /// </summary>
        public void Reset()
        {
            m_CurrentSpreadAngle = m_MinSpreadAngle;
            m_TimeSinceLastShot = m_SpreadRecoveryDelay;
            onWeaponSpreadChanged?.Raise(m_CurrentSpreadAngle);
        }

        #endregion
    }
}
