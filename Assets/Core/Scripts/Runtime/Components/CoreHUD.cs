using UnityEngine;
using Unity.Netcode;
using System.Collections;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Manages the player's heads-up display (HUD) using Unity's UI Toolkit.
    /// This component handles health/stamina bars, notifications, respawn overlays, and player status updates.
    /// Only active for the local player (owner).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CoreHUD : NetworkBehaviour
    {
        #region Fields & Properties

        [Header("Component Dependencies")]
        [SerializeField] private CoreStatsHandler coreStats;

        [Header("Listening to Events")]
        [SerializeField] private StatChangeEvent onStatChanged;
        [SerializeField] private NotificationEvent onNotification;
        [SerializeField] private RespawnStatusEvent onRespawnStatusChanged;

        // UI Element References
        private Label m_MessageLabel;
        private Label m_CountdownLabel;
        private UIDocument m_UIDocument;
        private ProgressBar m_PlayerHealthBar;
        private ProgressBar m_PlayerStaminaBar;
        private VisualElement m_MessageOverlay;
        private VisualElement m_PlayerHealthBarFill;
        private VisualElement m_PlayerStaminaBarFill;
        private VisualElement m_NotificationContainer;

        // Notification System
        private readonly List<NotificationData> m_ActiveNotifications = new List<NotificationData>();

        // Lifecycle Management
        private Coroutine m_EliminatedCoroutine;

        // Constants
        private const int k_MaxNotifications = 3;
        private const float k_NotificationDuration = 3f;
        private static readonly Color k_StaminaBarColor = new Color(0.83f, 0.29f, 0.29f, 0.75f);
        private static readonly Color k_HealthBarColor = new Color(0.29f, 0.83f, 0.43f, 0.75f);

        #endregion

        #region Nested Classes

        /// <summary>
        /// Represents a single notification with its UI element and lifecycle management.
        /// </summary>
        private class NotificationData
        {
            public VisualElement Element;
            public Label Label;
            public Coroutine LifetimeCoroutine;
            public string Message;
        }

        #endregion

        #region Unity & Network Lifecycle

        /// <summary>
        /// Handles network spawn initialization for the local player's HUD.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                DisableForNonOwner();
                return;
            }

            if (!ValidateComponents()) return;

            CreateHUD();
            Initialize();
            RegisterEventListeners();
            StartCoroutine(InitialHUDUpdate());
        }

        /// <summary>
        /// Called during OnNetworkSpawn to allow derived classes to perform additional initialization.
        /// </summary>
        protected virtual void Initialize()
        {
            // Override in derived classes for additional initialization
        }

        /// <summary>
        /// Handles cleanup when the network object is despawned.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                UnregisterEventListeners();
                ClearAllNotifications();
            }
        }

        #endregion

        #region Event Management

        /// <summary>
        /// Registers all event listeners for HUD updates.
        /// </summary>
        private void RegisterEventListeners()
        {
            if (onStatChanged != null) onStatChanged.RegisterListener(HandleStatChanged);
            if (onRespawnStatusChanged != null) onRespawnStatusChanged.RegisterListener(HandleRespawnStatusChanged);
            if (onNotification != null) onNotification.RegisterListener(HandleNotification);

            RegisterAdditionalListeners();
        }

        /// <summary>
        /// Registers additional event listeners.
        /// </summary>
        protected virtual void RegisterAdditionalListeners()
        {
        }

        /// <summary>
        /// Unregisters all event listeners to prevent memory leaks.
        /// </summary>
        private void UnregisterEventListeners()
        {
            if (onStatChanged != null) onStatChanged.UnregisterListener(HandleStatChanged);
            if (onRespawnStatusChanged != null) onRespawnStatusChanged.UnregisterListener(HandleRespawnStatusChanged);
            if (onNotification != null) onNotification.UnregisterListener(HandleNotification);

            UnregisterAdditionalListeners();
        }

        /// <summary>
        /// Unregisters additional event listeners.
        /// </summary>
        protected virtual void UnregisterAdditionalListeners()
        {
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles respawn status changes, updating the overlay message and countdown.
        /// </summary>
        /// <param name="payload">The respawn status payload.</param>
        private void HandleRespawnStatusChanged(RespawnStatusPayload payload)
        {
            if (payload.playerId != OwnerClientId) return;

            var divider = m_MessageOverlay?.Q<VisualElement>("message-divider");

            // Hide overlay if both message and subtext are empty
            if (string.IsNullOrEmpty(payload.message) && string.IsNullOrEmpty(payload.subtext))
            {
                if (m_MessageOverlay != null)
                {
                    m_MessageOverlay.style.display = DisplayStyle.None;
                }
                return;
            }

            // Show overlay with message
            if (m_MessageOverlay != null)
            {
                m_MessageOverlay.style.display = DisplayStyle.Flex;

                if (m_MessageLabel != null)
                {
                    m_MessageLabel.text = payload.message;
                }

                // Control countdown/divider visibility
                if (m_CountdownLabel != null)
                {
                    m_CountdownLabel.text = payload.subtext;
                    m_CountdownLabel.style.display = payload.showSubtext ? DisplayStyle.Flex : DisplayStyle.None;
                }

                if (divider != null)
                {
                    divider.style.display = payload.showSubtext ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }

        /// <summary>
        /// Handles stat change events, updating progress bars and handling elimination notifications.
        /// </summary>
        /// <param name="payload">The stat change payload.</param>
        private void HandleStatChanged(StatChangePayload payload)
        {
            // Handle elimination notifications
            ProcessEliminationNotification(payload);

            // Process networked stat changes (available to all clients)
            HandleStatChangedNetworked(payload);

            // Process local player stat changes
            if (payload.targetPlayerId == OwnerClientId)
            {
                UpdateLocalPlayerStats(payload);
                HandleStatChangedLocal(payload);
            }
        }

        /// <summary>
        /// Handles local player stat changes.
        /// </summary>
        /// <param name="payload">The stat change payload.</param>
        protected virtual void HandleStatChangedLocal(StatChangePayload payload)
        {
            // Override in derived classes to handle additional local stat changes
        }

        /// <summary>
        /// Handles networked stat changes visible to all clients.
        /// </summary>
        /// <param name="payload">The stat change payload.</param>
        protected virtual void HandleStatChangedNetworked(StatChangePayload payload)
        {
            // Override in derived classes to handle additional networked stat changes
        }

        /// <summary>
        /// Handles notification events by displaying them in the HUD.
        /// </summary>
        /// <param name="payload">The notification payload containing the client ID and message.</param>
        private void HandleNotification(NotificationPayload payload)
        {
            AddNotification(payload.message);
        }

        #endregion

        #region UI Creation & Setup

        /// <summary>
        /// Creates and initializes the HUD using the UIDocument component.
        /// </summary>
        private void CreateHUD()
        {
            m_UIDocument = GetComponent<UIDocument>();
            if (m_UIDocument == null)
            {
                Debug.LogError("CoreHUD requires a UIDocument component.", this);
                return;
            }

            var root = m_UIDocument.rootVisualElement;
            CacheUIElements(root);
            ConfigureUIElements();
            QueryHUDElements(root);
            SetHUDDefaults();
        }

        /// <summary>
        /// Caches references to common UI elements from the root visual element.
        /// </summary>
        /// <param name="root">The root visual element of the UIDocument.</param>
        private void CacheUIElements(VisualElement root)
        {
            m_MessageLabel = root.Q<Label>("message-label");
            m_CountdownLabel = root.Q<Label>("countdown-label");
            m_MessageOverlay = root.Q<VisualElement>("message-overlay");
            m_PlayerHealthBar = root.Q<ProgressBar>("player-health-bar");
            m_PlayerStaminaBar = root.Q<ProgressBar>("player-stamina-bar");
            m_NotificationContainer = root.Q<VisualElement>("notification-container");
        }

        /// <summary>
        /// Configures the appearance and initial state of UI elements.
        /// </summary>
        private void ConfigureUIElements()
        {
            // Configure message overlay
            if (m_MessageOverlay != null)
            {
                m_MessageOverlay.style.display = DisplayStyle.None;
            }

            // Configure progress bar colors
            if (m_PlayerHealthBar != null)
            {
                m_PlayerHealthBarFill = m_PlayerHealthBar.Q<VisualElement>(null, "unity-progress-bar__progress");
                if (m_PlayerHealthBarFill != null)
                {
                    m_PlayerHealthBarFill.style.backgroundColor = k_HealthBarColor;
                }
            }

            if (m_PlayerStaminaBar != null)
            {
                m_PlayerStaminaBarFill = m_PlayerStaminaBar.Q<VisualElement>(null, "unity-progress-bar__progress");
                if (m_PlayerStaminaBarFill != null)
                {
                    m_PlayerStaminaBarFill.style.backgroundColor = k_StaminaBarColor;
                }
            }
        }

        /// <summary>
        /// Queries for additional HUD elements.
        /// </summary>
        /// <param name="root">The root visual element of the UIDocument.</param>
        protected virtual void QueryHUDElements(VisualElement root)
        {
            // Override in derived classes to query additional HUD elements
        }

        /// <summary>
        /// Sets default HUD states.
        /// </summary>
        protected virtual void SetHUDDefaults()
        {
            // Override in derived classes to set default HUD states
        }

        #endregion

        #region Notification System

        /// <summary>
        /// Adds a notification to the HUD notification system.
        /// </summary>
        /// <param name="message">The message text to display.</param>
        private void AddNotification(string message)
        {
            if (m_NotificationContainer == null) return;

            // Remove the oldest notification if at capacity
            if (m_ActiveNotifications.Count >= k_MaxNotifications)
            {
                RemoveNotification(m_ActiveNotifications[0]);
            }

            // Create notification elements
            var notificationElement = new VisualElement();
            notificationElement.AddToClassList("notification-item");

            var label = new Label(message);
            notificationElement.Add(label);

            // Create notification data
            var notificationData = new NotificationData
            {
                Element = notificationElement,
                Label = label,
                Message = message
            };

            // Add to UI and tracking
            m_NotificationContainer.Add(notificationElement);
            m_ActiveNotifications.Add(notificationData);

            // Start lifetime management
            notificationData.LifetimeCoroutine = StartCoroutine(NotificationLifetimeCoroutine(notificationData));
        }

        /// <summary>
        /// Manages the lifetime of a notification, including fade-out animation.
        /// </summary>
        /// <param name="notification">The notification data to manage.</param>
        /// <returns>IEnumerator for coroutine execution.</returns>
        private IEnumerator NotificationLifetimeCoroutine(NotificationData notification)
        {
            yield return new WaitForSeconds(k_NotificationDuration);

            // Fade out animation
            if (notification.Element != null)
            {
                notification.Element.AddToClassList("notification-fade-out");
                // Wait for fade animation
                yield return new WaitForSeconds(0.3f);
            }

            RemoveNotification(notification);
        }

        /// <summary>
        /// Removes a notification from the HUD and cleans up its resources.
        /// </summary>
        /// <param name="notification">The notification to remove.</param>
        private void RemoveNotification(NotificationData notification)
        {
            if (notification == null) return;

            if (notification.LifetimeCoroutine != null)
            {
                StopCoroutine(notification.LifetimeCoroutine);
            }

            if (notification.Element != null && m_NotificationContainer != null)
            {
                m_NotificationContainer.Remove(notification.Element);
            }

            m_ActiveNotifications.Remove(notification);
        }

        /// <summary>
        /// Clears all active notifications from the HUD.
        /// </summary>
        private void ClearAllNotifications()
        {
            var notificationsToRemove = new List<NotificationData>(m_ActiveNotifications);
            foreach (var notification in notificationsToRemove)
            {
                RemoveNotification(notification);
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Validates that all required components are present.
        /// </summary>
        /// <returns>True if all components are valid, false otherwise.</returns>
        private bool ValidateComponents()
        {
            if (coreStats == null)
            {
                Debug.LogError("CoreHUD could not find CoreStatsHandler! The HUD will not function.", this);
                enabled = false;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Disables the HUD for non-owner clients.
        /// </summary>
        private void DisableForNonOwner()
        {
            if (TryGetComponent<UIDocument>(out var uiDoc))
            {
                uiDoc.enabled = false;
            }
            enabled = false;
        }

        /// <summary>
        /// Coroutine that performs initial HUD updates after spawn.
        /// </summary>
        /// <returns>IEnumerator for coroutine execution.</returns>
        private IEnumerator InitialHUDUpdate()
        {
            yield return null;

            if (coreStats != null)
            {
                foreach (var stat in coreStats.GetAllStats())
                {
                    HandleStatChanged(new StatChangePayload
                    {
                        statName = stat.name,
                        currentValue = stat.current,
                        maxValue = stat.max
                    });
                }
            }
        }

        /// <summary>
        /// Updates the local player's stat displays (health and stamina bars).
        /// </summary>
        /// <param name="payload">The stat change payload.</param>
        private void UpdateLocalPlayerStats(StatChangePayload payload)
        {
            if (payload.statID == StatKeys.Health)
            {
                if (m_PlayerHealthBar != null)
                {
                    m_PlayerHealthBar.highValue = payload.maxValue;
                    m_PlayerHealthBar.value = payload.currentValue;
                }
            }
            else if (payload.statID == StatKeys.Stamina)
            {
                if (m_PlayerStaminaBar != null)
                {
                    m_PlayerStaminaBar.highValue = payload.maxValue;
                    m_PlayerStaminaBar.value = payload.currentValue;
                }
            }
        }

        /// <summary>
        /// Processes elimination notifications when a player's health reaches zero.
        /// </summary>
        /// <param name="payload">The stat change payload to check for elimination.</param>
        private void ProcessEliminationNotification(StatChangePayload payload)
        {
            if (payload.statID == StatKeys.Health && payload.currentValue <= 0 && payload.sourceType == ModificationSource.Damage)
            {
                string sourcePlayerName = GetPlayerName(payload.sourcePlayerId);
                string targetPlayerName = GetPlayerName(payload.targetPlayerId);

                string eliminationMessage = (sourcePlayerName == targetPlayerName && payload.sourcePlayerId == payload.targetPlayerId)
                    ? $"{targetPlayerName} eliminated themselves"
                    : $"{sourcePlayerName} eliminated {targetPlayerName}";

                AddNotification(eliminationMessage);
            }
        }

        /// <summary>
        /// Gets a player name by ID, looking up from CorePlayerState.
        /// </summary>
        /// <param name="playerId">The player's network client ID.</param>
        /// <returns>The player's display name or a fallback format.</returns>
        private string GetPlayerName(ulong playerId)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null)
            {
                var playerObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(playerId);
                if (playerObject != null && playerObject.TryGetComponent<CorePlayerState>(out var playerState))
                {
                    string playerName = playerState.PlayerName;
                    if (!string.IsNullOrEmpty(playerName))
                    {
                        return playerName;
                    }
                }
            }
            return $"Player-{playerId}";
        }

        #endregion
    }
}
