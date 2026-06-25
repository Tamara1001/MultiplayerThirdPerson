using UnityEngine;
using Unity.Netcode;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Player addon that extends <see cref="CorePlayerManager"/> with shooter-specific functionality.
    /// Implements <see cref="IPlayerAddon"/> to integrate shooting mechanics, aiming, weapons,
    /// and shooter-specific animations into the core player system.
    /// Manages camera modes, aim toggling, and component lifecycle for shooter gameplay.
    /// </summary>
    public class ShooterAddon : NetworkBehaviour, IPlayerAddon
    {
        #region Fields & Properties

        [Header("Shooter Components")]
        [Tooltip("Reference to the weapon HUD for displaying ammo and weapon information.")]
        [SerializeField] private WeaponHUD weaponHUD;
        [Tooltip("Controls aiming mechanics and animation rigging.")]
        [SerializeField] private AimController aimController;
        [Tooltip("Manages shooter-specific animations.")]
        [SerializeField] private ShooterAnimator shooterAnimator;
        [Tooltip("Handles weapon switching and firing logic.")]
        [SerializeField] private WeaponController weaponController;
        [Tooltip("Processes hit detection and damage application.")]
        [SerializeField] private ShooterHitProcessor shooterHitProcessor;

        [Header("Camera Mode Names")]
        [Tooltip("Name of the third-person free look camera mode.")]
        [SerializeField] private string thirdPersonFreeLookCameraName = "FreeLook";
        [Tooltip("Name of the third-person aim camera mode.")]
        [SerializeField] private string thirdPersonAimCameraName = "Aim";

        [Header("Camera Sensitivity")]
        [Tooltip("Camera look sensitivity when in free look mode.")]
        public float freeLookSensitivity = 1.0f;
        [Tooltip("Camera look sensitivity when aiming (typically lower for precision).")]
        public float aimSensitivity = 0.5f;

        [Header("Input Events (Listening)")]
        [Tooltip("Event triggered when the player toggles aim mode.")]
        [SerializeField] private GameEvent onAimToggled;

        [Header("Gameplay Events (Broadcasting)")]
        [Tooltip("Event raised when aiming state changes. Contains the new aiming state.")]
        [SerializeField] private BoolEvent onAimingStateChanged;

        private CorePlayerManager m_PlayerManager;
        private bool m_IsAiming;

        private static readonly int k_AnimIDIsReloading = Animator.StringToHash("IsReloading");

        /// <summary>
        /// Gets the weapon HUD component.
        /// </summary>
        public WeaponHUD WeaponHUD => weaponHUD;

        /// <summary>
        /// Gets the aim controller component.
        /// </summary>
        public AimController AimController => aimController;

        /// <summary>
        /// Gets the shooter animator component.
        /// </summary>
        public ShooterAnimator ShooterAnimator => shooterAnimator;

        /// <summary>
        /// Gets the weapon controller component.
        /// </summary>
        public WeaponController WeaponController => weaponController;

        /// <summary>
        /// Gets the shooter hit processor component.
        /// </summary>
        public ShooterHitProcessor ShooterHitProcessor => shooterHitProcessor;

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the shooter addon with the core player manager.
        /// Automatically finds required components if not assigned in the inspector.
        /// </summary>
        /// <param name="playerManager">The core player manager this addon is attached to.</param>
        public void Initialize(CorePlayerManager playerManager)
        {
            m_PlayerManager = playerManager;

            // Find components if not assigned in the inspector
            if (weaponHUD == null) weaponHUD = GetComponent<WeaponHUD>();
            if (aimController == null) aimController = GetComponent<AimController>();
            if (shooterAnimator == null) shooterAnimator = GetComponent<ShooterAnimator>();
            if (weaponController == null) weaponController = GetComponent<WeaponController>();
            if (shooterHitProcessor == null) shooterHitProcessor = GetComponent<ShooterHitProcessor>();
        }

        /// <summary>
        /// Called when the player spawns.
        /// Registers input event listeners for the owning client.
        /// </summary>
        public void OnPlayerSpawn()
        {
            if (m_PlayerManager.IsOwner)
            {
                onAimToggled.RegisterListener(HandleAimToggled);
            }
        }

        /// <summary>
        /// Called when the player despawns.
        /// Unregisters input event listeners to prevent memory leaks.
        /// </summary>
        public void OnPlayerDespawn()
        {
            if (m_PlayerManager != null && m_PlayerManager.IsOwner)
            {
                onAimToggled.UnregisterListener(HandleAimToggled);
            }
        }

        /// <summary>
        /// Called when the player's life state changes.
        /// Handles shooter-specific state changes for elimination and respawn.
        /// </summary>
        /// <param name="previousState">The previous life state.</param>
        /// <param name="newState">The new life state.</param>
        public void OnLifeStateChanged(PlayerLifeState previousState, PlayerLifeState newState)
        {
            if (newState == PlayerLifeState.Eliminated)
            {
                ResetAimState();
                OnEliminated(true);
            }
            else if (previousState == PlayerLifeState.Eliminated && newState == PlayerLifeState.Respawned)
            {
                ResetAimState();
                OnEliminated(false);
            }
        }

        /// <summary>
        /// Enables or disables shooter components based on elimination state.
        /// </summary>
        /// <param name="isEliminated">True if the player is eliminated, false otherwise.</param>
        public void OnEliminated(bool isEliminated)
        {
            bool isActive = !isEliminated;

            if (weaponController != null)
            {
                weaponController.enabled = isActive;
                weaponController.SetCurrentWeaponActive(isActive);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Resets the aiming state to non-aiming mode.
        /// Switches camera back to free look and resets animation rigging.
        /// </summary>
        private void ResetAimState()
        {
            if (m_IsAiming)
            {
                m_IsAiming = false;
                if (onAimingStateChanged != null) onAimingStateChanged.Raise(false);

                if (m_PlayerManager != null && m_PlayerManager.CoreCamera != null)
                {
                    m_PlayerManager.CoreCamera.SwitchCameraMode(thirdPersonFreeLookCameraName);
                    m_PlayerManager.CoreCamera.SetLookSensitivity(freeLookSensitivity);

                    // Sync movement rotation mode with the new camera mode
                    if (m_PlayerManager.CoreMovement != null)
                    {
                        m_PlayerManager.CoreMovement.PlayerRotationMode = m_PlayerManager.CoreCamera.CurrentPlayerRotationMode;
                    }
                }
            }

            if (aimController != null)
            {
                aimController.ResetRiggingState();
            }
        }

        /// <summary>
        /// Handles the aim toggle input event.
        /// Prevents aiming while reloading and switches between aim and free look camera modes.
        /// Updates camera sensitivity and player rotation mode based on aiming state.
        /// </summary>
        private void HandleAimToggled()
        {
            // Prevent aim toggle during reload to avoid animation conflicts
            bool isWeaponReloading = weaponController != null &&
                weaponController.CurrentWeapon?.GetCurrentState() == WeaponState.Reloading;
            bool isAnimatorReloading = shooterAnimator != null &&
                shooterAnimator.Animator.GetBool(k_AnimIDIsReloading);

            if (isWeaponReloading || isAnimatorReloading)
            {
                return;
            }

            m_IsAiming = !m_IsAiming;
            if (onAimingStateChanged != null) onAimingStateChanged.Raise(m_IsAiming);

            if (m_PlayerManager.CoreCamera != null)
            {
                string modeName = m_IsAiming ? thirdPersonAimCameraName : thirdPersonFreeLookCameraName;
                m_PlayerManager.CoreCamera.SwitchCameraMode(modeName);

                float sensitivity = m_IsAiming ? aimSensitivity : freeLookSensitivity;
                m_PlayerManager.CoreCamera.SetLookSensitivity(sensitivity);
            }

            if (m_PlayerManager.CoreMovement != null && m_PlayerManager.CoreCamera != null)
            {
                m_PlayerManager.CoreMovement.PlayerRotationMode = m_PlayerManager.CoreCamera.CurrentPlayerRotationMode;
            }
        }

        #endregion
    }
}
