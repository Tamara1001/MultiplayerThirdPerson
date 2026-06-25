using TMPro;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// An <see cref="IPlayerAddon"/> that displays a player's name above their head using a world-space canvas.
    /// The nameplate is only visible to other players (hidden for the local owner) and automatically
    /// rotates to face the camera. It is hidden when the player is eliminated.
    /// </summary>
    public class NamePlateAddon : MonoBehaviour, IPlayerAddon
    {
        #region Fields & Properties

        [Header("Name Display")]
        [Tooltip("World space canvas that displays the player's name above their head.")]
        [SerializeField] private Canvas nameDisplayCanvas;
        [Tooltip("TextMeshPro component for displaying the player name.")]
        [SerializeField] private TextMeshProUGUI nameDisplayText;

        private CorePlayerManager m_PlayerManager;
        private Camera m_MainCamera;

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the nameplate addon and subscribes to name change events.
        /// </summary>
        /// <param name="playerManager">The <see cref="CorePlayerManager"/> that owns this addon.</param>
        public void Initialize(CorePlayerManager playerManager)
        {
            m_PlayerManager = playerManager;
            m_PlayerManager.PlayerState.OnNameChanged += UpdateNameDisplay;
        }

        /// <summary>
        /// Called when the player spawns. Sets up the nameplate visibility and initial display.
        /// The nameplate is hidden for the local player and visible for remote players.
        /// </summary>
        public void OnPlayerSpawn()
        {
            m_MainCamera = Camera.main;

            // Hide nameplate for local player, show for remote players
            if (nameDisplayCanvas != null)
            {
                nameDisplayCanvas.gameObject.SetActive(!m_PlayerManager.IsOwner);
            }

            UpdateNameDisplay(m_PlayerManager.PlayerName);
        }

        /// <summary>
        /// Called when the player despawns. Unsubscribes from name change events.
        /// </summary>
        public void OnPlayerDespawn()
        {
            if (m_PlayerManager != null)
            {
                m_PlayerManager.PlayerState.OnNameChanged -= UpdateNameDisplay;
            }
        }

        /// <summary>
        /// Called when the player's life state changes. Hides the nameplate when the player is eliminated.
        /// </summary>
        /// <param name="previousState">The previous life state.</param>
        /// <param name="newState">The new life state.</param>
        public void OnLifeStateChanged(PlayerLifeState previousState, PlayerLifeState newState)
        {
            if (nameDisplayCanvas != null && !m_PlayerManager.IsOwner)
            {
                nameDisplayCanvas.gameObject.SetActive(newState != PlayerLifeState.Eliminated);
            }
        }

        #endregion

        #region Unity Methods

        private void LateUpdate()
        {
            UpdateNameDisplayRotation();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Updates the displayed name text.
        /// </summary>
        /// <param name="newName">The new name to display.</param>
        private void UpdateNameDisplay(string newName)
        {
            if (nameDisplayText != null)
            {
                nameDisplayText.text = newName;
            }
        }

        /// <summary>
        /// Rotates the nameplate canvas to always face the camera (billboard effect).
        /// The 180-degree rotation corrects the canvas facing direction.
        /// </summary>
        private void UpdateNameDisplayRotation()
        {
            if (nameDisplayCanvas == null || !nameDisplayCanvas.gameObject.activeSelf)
            {
                return;
            }

            if (m_MainCamera == null)
            {
                m_MainCamera = Camera.main;
            }

            if (m_MainCamera == null)
            {
                return;
            }

            // Billboard effect: make canvas face the camera
            nameDisplayCanvas.transform.LookAt(m_MainCamera.transform);

            // Correct the facing direction so text reads properly
            nameDisplayCanvas.transform.Rotate(0, 180, 0);
        }

        #endregion
    }
}
