using UnityEngine;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Manages weapon state transitions and timing for firing, reloading, and cooldown cycles.
    /// Works in conjunction with <see cref="AmmoHandler"/> and <see cref="IFiringMechanism"/>
    /// to coordinate weapon behavior based on the current <see cref="WeaponState"/>.
    /// Uses a timer-based state machine to handle automatic state transitions.
    /// </summary>
    public class WeaponStateManager : MonoBehaviour
    {
        #region Fields & Properties

        [Tooltip("The current state of the weapon.")]
        [SerializeField] private WeaponState currentState = WeaponState.ReadyToFire;

        private float m_StateTimer;
        private AmmoHandler m_AmmoHandler;
        private IFiringMechanism m_FiringMechanism;

        /// <summary>
        /// Gets the current state of the weapon.
        /// </summary>
        public WeaponState CurrentState => currentState;

        #endregion

        #region Unity Methods

        private void Update()
        {
            if (m_StateTimer > 0)
            {
                m_StateTimer -= Time.deltaTime;
                if (m_StateTimer <= 0)
                {
                    m_StateTimer = 0f;
                    HandleStateTimerEnd();
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the state manager with required dependencies.
        /// This should be called by the <see cref="ModularWeapon"/> during weapon setup.
        /// </summary>
        /// <param name="ammoHandler">The ammo handler for checking ammunition and reloading.</param>
        /// <param name="firingMechanism">The firing mechanism for fire rate and reset operations.</param>
        public void Initialize(AmmoHandler ammoHandler, IFiringMechanism firingMechanism)
        {
            m_AmmoHandler = ammoHandler;
            m_FiringMechanism = firingMechanism;
        }

        /// <summary>
        /// Transitions the weapon to a new state with an optional duration timer.
        /// When the duration expires, the state machine will automatically handle the next transition.
        /// </summary>
        /// <param name="newState">The state to transition to.</param>
        /// <param name="duration">Optional duration in seconds before automatic state transition. Defaults to 0.</param>
        public void TransitionToState(WeaponState newState, float duration = 0f)
        {
            currentState = newState;
            m_StateTimer = duration;
        }

        /// <summary>
        /// Checks whether the weapon can currently fire.
        /// </summary>
        /// <returns>True if the weapon is in ReadyToFire state and has ammunition, false otherwise.</returns>
        public bool CanFire()
        {
            return currentState == WeaponState.ReadyToFire && m_AmmoHandler.HasAmmo();
        }

        /// <summary>
        /// Checks whether the weapon can currently reload.
        /// Reload is allowed in ReadyToFire, Empty, and Cooldown states.
        /// </summary>
        /// <returns>True if the weapon can reload, false otherwise.</returns>
        public bool CanReload()
        {
            return currentState == WeaponState.ReadyToFire ||
                   currentState == WeaponState.Empty ||
                   currentState == WeaponState.Cooldown;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Handles automatic state transitions when the state timer reaches zero.
        /// Manages the firing cycle, cooldown period, and reload completion.
        /// </summary>
        private void HandleStateTimerEnd()
        {
            switch (currentState)
            {
                case WeaponState.Firing:
                    TransitionToState(WeaponState.Cooldown, m_FiringMechanism?.FireRate ?? 0.1f);
                    break;

                case WeaponState.Cooldown:
                    if (!m_AmmoHandler.HasAmmo())
                    {
                        // Attempt auto-reload when out of ammo
                        if (m_AmmoHandler.TryReload())
                        {
                            // TryReload already transitions to Reloading state
                        }
                        else
                        {
                            TransitionToState(WeaponState.Empty);
                        }
                    }
                    else
                    {
                        TransitionToState(WeaponState.ReadyToFire);
                    }
                    break;

                case WeaponState.Reloading:
                    m_AmmoHandler.Reload();
                    TransitionToState(WeaponState.ReadyToFire);
                    m_FiringMechanism?.Reset();
                    break;
            }
        }

        #endregion
    }
}
