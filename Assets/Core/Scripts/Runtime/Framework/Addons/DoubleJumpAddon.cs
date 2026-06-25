using UnityEngine;
using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// An addon that grants the player the ability to perform a double jump.
    /// Listens for jump input and applies vertical velocity if the player is airborne and hasn't already double jumped.
    /// </summary>
    public class DoubleJumpAddon : NetworkBehaviour, IPlayerAddon
    {
        #region Fields & Properties

        [Header("Settings")]
        [Tooltip("The height of the double jump.")]
        [SerializeField] private float doubleJumpHeight = 2.0f;
        [Tooltip("Optional stamina cost for the double jump.")]
        [SerializeField] private float staminaCost = 0f;

        [Header("Input Events")]
        [Tooltip("Event raised when the jump button is pressed.")]
        [SerializeField] private GameEvent onJumpPressed;

        private CorePlayerManager m_PlayerManager;
        private CoreMovement m_CoreMovement;
        private CoreStatsHandler m_CoreStats;
        private bool m_HasDoubleJumped;
        private bool m_IsActive = true;

        #endregion

        #region IPlayerAddon Implementation

        public void Initialize(CorePlayerManager playerManager)
        {
            m_PlayerManager = playerManager;
            m_CoreMovement = playerManager.CoreMovement;
            m_CoreStats = playerManager.CoreStats;
        }

        public void OnPlayerSpawn()
        {
            if (IsOwner)
            {
                RegisterEventListeners();
            }
        }

        public void OnPlayerDespawn()
        {
            if (IsOwner)
            {
                UnregisterEventListeners();
            }
        }

        public void OnLifeStateChanged(PlayerLifeState previousState, PlayerLifeState newState)
        {
            m_IsActive = newState == PlayerLifeState.InitialSpawn || newState == PlayerLifeState.Respawned;

            if (newState == PlayerLifeState.Respawned || newState == PlayerLifeState.InitialSpawn)
            {
                m_HasDoubleJumped = false;
            }
        }

        #endregion

        #region Private Methods

        private void RegisterEventListeners()
        {
            if (onJumpPressed != null)
            {
                onJumpPressed.RegisterListener(HandleJump);
            }

            if (m_CoreMovement != null)
            {
                m_CoreMovement.OnGroundedStateChanged += HandleGroundedStateChanged;
            }
        }

        private void UnregisterEventListeners()
        {
            if (onJumpPressed != null)
            {
                onJumpPressed.UnregisterListener(HandleJump);
            }

            if (m_CoreMovement != null)
            {
                m_CoreMovement.OnGroundedStateChanged -= HandleGroundedStateChanged;
            }
        }

        private void HandleJump()
        {
            if (!m_IsActive || !IsOwner) return;

            if (m_CoreMovement == null) return;

            // Only allow double jump if airborne and haven't jumped yet
            if (!m_CoreMovement.IsGrounded && !m_HasDoubleJumped)
            {
                AttemptDoubleJump();
            }
        }

        private void AttemptDoubleJump()
        {
            // Check stamina if needed
            if (staminaCost > 0f && m_CoreStats != null)
            {
                if (!m_CoreStats.TryConsumeStat(StatKeys.Stamina, staminaCost, OwnerClientId))
                {
                    return;
                }
            }

            PerformDoubleJump();
        }

        private void PerformDoubleJump()
        {
            m_HasDoubleJumped = true;

            // Calculate jump velocity: v = sqrt(2 * g * h)
            // Note: Gravity is negative, so we multiply by -2
            float jumpVelocity = Mathf.Sqrt(doubleJumpHeight * -2f * m_CoreMovement.gravity);

            // Apply velocity directly
            m_CoreMovement.SetVerticalVelocity(jumpVelocity);

            // Reset any downward force/gravity accumulation if needed, though SetVerticalVelocity handles the main part
        }

        private void HandleGroundedStateChanged(bool isGrounded)
        {
            if (isGrounded)
            {
                m_HasDoubleJumped = false;
            }
        }

        #endregion
    }
}
