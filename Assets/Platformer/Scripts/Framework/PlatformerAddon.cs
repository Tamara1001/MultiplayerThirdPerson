using UnityEngine;
using Unity.Netcode;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Platformer
{
    /// <summary>
    /// Manages platformer-specific player functionality including jump input handling and stamina consumption.
    /// Integrates with <see cref="CorePlayerManager"/> to provide platformer movement abilities and
    /// handles the lifecycle of platformer components during spawn, despawn, and life state changes.
    /// </summary>
    public class PlatformerAddon : NetworkBehaviour, IPlayerAddon
    {
        #region Fields & Properties

        [Header("Platformer Components")]
        [Tooltip("The locomotion ability component that handles platformer movement parameters.")]
        [SerializeField] private PlatformerLocomotionAbility locomotionAbility;

        [Header("Input Events")]
        [Tooltip("Game event triggered when the jump button is pressed.")]
        [SerializeField] private GameEvent onJumpPressed;

        private CorePlayerManager m_PlayerManager;

        /// <summary>
        /// Gets the platformer locomotion ability component.
        /// </summary>
        public PlatformerLocomotionAbility LocomotionAbility => locomotionAbility;

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the platformer addon with a reference to the player manager.
        /// </summary>
        /// <param name="playerManager">The core player manager that owns this addon.</param>
        public void Initialize(CorePlayerManager playerManager)
        {
            m_PlayerManager = playerManager;

            if (locomotionAbility == null)
            {
                locomotionAbility = GetComponentInChildren<PlatformerLocomotionAbility>();
            }
        }

        /// <summary>
        /// Called when the player spawns. Registers input event listeners for the owner.
        /// </summary>
        public void OnPlayerSpawn()
        {
            if (m_PlayerManager.IsOwner)
            {
                onJumpPressed?.RegisterListener(HandleJump);
            }
        }

        /// <summary>
        /// Called when the player despawns. Unregisters input event listeners for the owner.
        /// </summary>
        public void OnPlayerDespawn()
        {
            if (m_PlayerManager != null && m_PlayerManager.IsOwner)
            {
                onJumpPressed?.UnregisterListener(HandleJump);
            }
        }

        /// <summary>
        /// Called when the player's life state changes. Enables or disables locomotion based on the new state.
        /// </summary>
        /// <param name="previousState">The previous life state.</param>
        /// <param name="newState">The new life state.</param>
        public void OnLifeStateChanged(PlayerLifeState previousState, PlayerLifeState newState)
        {
            if (newState == PlayerLifeState.Eliminated)
            {
                if (locomotionAbility != null)
                {
                    locomotionAbility.enabled = false;
                }
            }
            else if (previousState == PlayerLifeState.Eliminated && newState == PlayerLifeState.Respawned)
            {
                if (locomotionAbility != null)
                {
                    locomotionAbility.enabled = true;
                }
            }
        }

        #endregion

        #region Unity Methods

        private void Update()
        {
            if (!IsOwner) return;

            HandleSprintingStamina();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Handles jump input and consumes stamina if the player is grounded and has sufficient stamina.
        /// </summary>
        private void HandleJump()
        {
            if (m_PlayerManager?.CoreMovement == null || m_PlayerManager?.CoreStats == null) return;
            if (locomotionAbility == null) return;

            var movement = m_PlayerManager.CoreMovement;
            if (!movement.IsGrounded) return;

            float jumpStaminaCost = locomotionAbility.JumpStaminaCost;
            if (m_PlayerManager.CoreStats.TryConsumeStat(StatKeys.Stamina, jumpStaminaCost, OwnerClientId))
            {
                movement.PerformJump();
            }
        }

        /// <summary>
        /// Continuously drains stamina while sprinting. Disables sprint if stamina is depleted.
        /// </summary>
        private void HandleSprintingStamina()
        {
            if (m_PlayerManager?.CoreMovement == null || m_PlayerManager?.CoreStats == null) return;
            if (locomotionAbility == null) return;

            var movement = m_PlayerManager.CoreMovement;
            if (!movement.IsSprinting || movement.CurrentSpeed <= 0.1f || !movement.IsGrounded) return;

            float staminaCost = locomotionAbility.StaminaCost * Time.deltaTime;
            if (!m_PlayerManager.CoreStats.TryConsumeStat(StatKeys.Stamina, staminaCost, OwnerClientId))
            {
                movement.SetSprintState(false);
            }
        }

        #endregion
    }
}

