using UnityEngine;
using Unity.Netcode;
using Blocks.Gameplay.Core;
using UnityEngine.InputSystem;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Handles shooter-specific player input using Unity's Input System and broadcasts actions via GameEvents.
    /// This component handles inputs for firing, aiming, reloading, and weapon switching.
    /// </summary>
    public class ShooterInputHandler : NetworkBehaviour
    {
        #region Fields

        [Header("Shooter Events")]
        [Tooltip("Raised when the aim button is pressed.")]
        [SerializeField] private Core.GameEvent onSecondaryActionPressed;
        [Tooltip("Raised when the aim button is released.")]
        [SerializeField] private Core.GameEvent onSecondaryActionReleased;
        [Tooltip("Raised when the reload button is pressed.")]
        [SerializeField] private Core.GameEvent onReloadPressed;
        [Tooltip("Raised when the next weapon button is pressed.")]
        [SerializeField] private Core.GameEvent onNextWeaponPressed;
        [Tooltip("Raised when the previous weapon button is pressed.")]
        [SerializeField] private Core.GameEvent onPreviousWeaponPressed;

        private GameplayInputSystem_Actions m_InputActions;

        #endregion

        #region Unity Lifecycle & Network Callbacks

        private void Awake()
        {
            m_InputActions = new GameplayInputSystem_Actions();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                RegisterInputActions();
                m_InputActions.Player.Enable();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                m_InputActions.Player.Disable();
                UnregisterInputActions();
            }
        }

        private void OnEnable()
        {
            if (IsOwner && m_InputActions != null)
            {
                m_InputActions.Player.Enable();
            }
        }

        private void OnDisable()
        {
            if (IsOwner && m_InputActions != null)
            {
                m_InputActions.Player.Disable();
            }
        }

        #endregion

        #region Input Registration

        private void RegisterInputActions()
        {
            m_InputActions.Player.SecondaryAction.started += HandleAimPressed;
            m_InputActions.Player.SecondaryAction.canceled += HandleAimReleased;

            m_InputActions.Player.Reload.performed += HandleReload;

            m_InputActions.Player.Next.performed += HandleNextWeapon;
            m_InputActions.Player.Previous.performed += HandlePreviousWeapon;
        }

        private void UnregisterInputActions()
        {
            m_InputActions.Player.SecondaryAction.started -= HandleAimPressed;
            m_InputActions.Player.SecondaryAction.canceled -= HandleAimReleased;

            m_InputActions.Player.Reload.performed -= HandleReload;

            m_InputActions.Player.Next.performed -= HandleNextWeapon;
            m_InputActions.Player.Previous.performed -= HandlePreviousWeapon;
        }

        #endregion

        #region Input Handlers

        private void HandleAimPressed(InputAction.CallbackContext context) => onSecondaryActionPressed?.Raise();
        private void HandleAimReleased(InputAction.CallbackContext context) => onSecondaryActionReleased?.Raise();
        private void HandleReload(InputAction.CallbackContext context) => onReloadPressed?.Raise();
        private void HandleNextWeapon(InputAction.CallbackContext context) => onNextWeaponPressed?.Raise();
        private void HandlePreviousWeapon(InputAction.CallbackContext context) => onPreviousWeaponPressed?.Raise();

        #endregion
    }
}

