using UnityEngine;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Platformer
{
    /// <summary>
    /// An <see cref="IMovementAbility"/> that enables a CharacterController-based player to move along with dynamic platforms.
    /// Unlike other abilities that return a <see cref="MovementModifier"/> from `Process`, this ability hooks into the
    /// <see cref="CoreMovement.FinalMoveCalculationOverride"/> delegate. This allows it to modify the final movement vector
    /// just before it's applied, adding the platform's positional delta to the player's movement to create a smooth "sticking" effect.
    /// </summary>
    public class PlatformerMovingPlatformAbility : MonoBehaviour, IMovementAbility
    {
        #region Fields & Properties

        public int Priority => 5;
        public float StaminaCost => 0f;

        private bool m_IsOnPlatform;
        private Vector3 m_LastPlatformPosition;
        private Transform m_CurrentPlatform;
        private CoreMovement m_Motor;
        private CharacterController m_Controller;

        #endregion

        #region Unity Methods

        private void OnDestroy()
        {
            if (m_Motor != null)
            {
                if (m_Motor.FinalMoveCalculationOverride == CalculateFinalMovementWithPlatform)
                {
                    m_Motor.FinalMoveCalculationOverride = null;
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the ability with the specified <see cref="CoreMovement"/> motor and sets up the movement override.
        /// </summary>
        /// <param name="motor">The motor that owns this ability.</param>
        public void Initialize(CoreMovement motor)
        {
            m_Motor = motor;
            m_Controller = motor.GetComponent<CharacterController>();
            m_Motor.FinalMoveCalculationOverride = CalculateFinalMovementWithPlatform;
        }

        /// <summary>
        /// Processes the ability each frame. Returns an empty modifier as platform logic is handled in the movement override.
        /// </summary>
        /// <returns>An empty <see cref="MovementModifier"/>.</returns>
        public MovementModifier Process()
        {
            return new MovementModifier();
        }

        /// <summary>
        /// Attempts to activate the ability.
        /// </summary>
        /// <returns>Always returns false as this is a passive ability.</returns>
        public bool TryActivate() => false;

        #endregion

        #region Private Methods

        /// <summary>
        /// Calculates the final movement vector by adding platform delta movement to player movement.
        /// </summary>
        /// <param name="playerMovement">The player's intended movement vector.</param>
        /// <returns>The final movement vector including platform motion.</returns>
        private Vector3 CalculateFinalMovementWithPlatform(Vector3 playerMovement)
        {
            DetectPlatform();

            if (m_IsOnPlatform && m_CurrentPlatform != null)
            {
                Vector3 platformDelta = m_CurrentPlatform.position - m_LastPlatformPosition;
                Vector3 playerHorizontalMovement = new Vector3(playerMovement.x, 0, playerMovement.z);
                Vector3 finalMovement = playerHorizontalMovement + platformDelta;

                if (playerMovement.y > 0)
                {
                    finalMovement.y += playerMovement.y;
                }

                m_LastPlatformPosition = m_CurrentPlatform.position;
                return finalMovement;
            }

            return playerMovement;
        }

        /// <summary>
        /// Detects whether the player is currently standing on a moving platform using a sphere cast.
        /// </summary>
        private void DetectPlatform()
        {
            float checkDistance = (m_Controller.height / 2) - m_Controller.radius + 0.1f;

            if (Physics.SphereCast(m_Controller.center + transform.position, m_Controller.radius, Vector3.down, out RaycastHit hit, checkDistance, m_Motor.groundLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.TryGetComponent<AutomatedNetworkTransform>(out _))
                {
                    if (!m_IsOnPlatform)
                    {
                        m_IsOnPlatform = true;
                        m_CurrentPlatform = hit.transform;
                        m_LastPlatformPosition = m_CurrentPlatform.position;
                    }
                    return;
                }
            }

            m_IsOnPlatform = false;
            m_CurrentPlatform = null;
        }

        #endregion
    }
}
