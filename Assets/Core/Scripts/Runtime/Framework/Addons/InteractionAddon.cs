using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// A Player Addon that manages interactions with objects in the world (IInteractable).
    /// It detects targets via raycast (look) and proximity (nearby), manages the "focus" state,
    /// and triggers interactions via input or collision.
    /// </summary>
    public class InteractionAddon : NetworkBehaviour, IPlayerAddon
    {
        #region Fields & Properties

        [Header("Detection Settings")]
        [Tooltip("The layer mask that defines which objects are considered interactable.")]
        [SerializeField] private LayerMask interactionLayer;

        [Tooltip("The maximum distance from the camera for raycast-based interactions.")]
        [SerializeField] private float raycastDistance = 5f;

        [Tooltip("The radius of the sphere around the player for proximity-based interactions.")]
        [SerializeField] private float proximityRadius = 2f;

        [Tooltip("A global cooldown in seconds between interactions to prevent spamming.")]
        [SerializeField] private float interactionCooldown = 0.2f;

        [Header("Listening To")]
        [Tooltip("GameEvent raised when the player presses the interaction button.")]
        [SerializeField] private GameEvent onInteractPressed;

        /// <summary>
        /// Gets or sets a value indicating whether the interactor is currently enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        private CorePlayerManager m_PlayerManager;
        private Camera m_MainCamera;
        private IInteractable m_CurrentFocusedInteractable;
        private float m_CooldownTimer;
        private readonly Collider[] m_ProximityColliders = new Collider[20];

        #endregion

        #region Public Methods

        /// <summary>
        /// Called by CorePlayerManager during Awake.
        /// </summary>
        public void Initialize(CorePlayerManager playerManager)
        {
            m_PlayerManager = playerManager;
        }

        /// <summary>
        /// Called when the player object is spawned.
        /// </summary>
        public void OnPlayerSpawn()
        {
            if (m_PlayerManager.IsOwner)
            {
                m_MainCamera = Camera.main;
                onInteractPressed.RegisterListener(TryInteract);
                IsEnabled = true;
            }
            else
            {
                enabled = false;
            }
        }

        /// <summary>
        /// Called when the player object is despawned.
        /// </summary>
        public void OnPlayerDespawn()
        {
            if (m_PlayerManager.IsOwner)
            {
                onInteractPressed.UnregisterListener(TryInteract);
                ClearFocus();
            }
        }

        /// <summary>
        /// Handles lifecycle changes (Eliminated vs Respawned).
        /// </summary>
        public void OnLifeStateChanged(PlayerLifeState previousState, PlayerLifeState newState)
        {
            if (newState == PlayerLifeState.Eliminated)
            {
                IsEnabled = false;
                ClearFocus();
            }
            else if (newState == PlayerLifeState.Respawned || newState == PlayerLifeState.InitialSpawn)
            {
                IsEnabled = true;
                m_CooldownTimer = 0f;
            }
        }

        #endregion

        #region Unity Methods

        /// <summary>
        /// Handles interaction cooldowns and continuously searches for the best interactable target.
        /// </summary>
        private void Update()
        {
            if (!IsSpawned || !IsOwner || !IsEnabled) return;

            if (m_CooldownTimer > 0)
            {
                m_CooldownTimer -= Time.deltaTime;
            }

            FindBestInteractable();
        }

        /// <summary>
        /// Handles collision events from the CharacterController.
        /// If the collided object is an interactable with a trigger mode of OnCollision, triggers interaction.
        /// </summary>
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!IsSpawned || !IsOwner || !IsEnabled || m_CooldownTimer > 0) return;

            if (hit.gameObject.TryGetComponent<IInteractable>(out var interactable))
            {
                if (interactable.TriggerMode == InteractionTriggerMode.OnCharacterControllerHit && interactable.CanInteract(gameObject))
                {
                    interactable.Interact(gameObject);
                    m_CooldownTimer = interactionCooldown;
                }
            }
        }

        #endregion

        #region Private Methods

        private void ClearFocus()
        {
            m_CurrentFocusedInteractable = null;
        }

        /// <summary>
        /// Scans for interactable objects via raycast and proximity, then determines the best target.
        /// </summary>
        private void FindBestInteractable()
        {
            if (m_MainCamera == null) return;

            var interactables = new List<IInteractable>();

            // Physics-based trigger modes are handled by Unity's collision callbacks
            // and should be excluded from the focus system to prevent duplicate interactions
            bool IsPhysicsBased(InteractionTriggerMode mode)
            {
                return mode == InteractionTriggerMode.OnCharacterControllerHit ||
                       mode == InteractionTriggerMode.OnTriggerEnter ||
                       mode == InteractionTriggerMode.OnRigidbodyCollision;
            }

            if (Physics.Raycast(m_MainCamera.transform.position, m_MainCamera.transform.forward, out var hit, raycastDistance, interactionLayer))
            {
                if (hit.collider.TryGetComponent<IInteractable>(out var raycastTarget) && !IsPhysicsBased(raycastTarget.TriggerMode))
                {
                    interactables.Add(raycastTarget);
                }
            }

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, proximityRadius, m_ProximityColliders, interactionLayer);
            for (int i = 0; i < hitCount; i++)
            {
                var col = m_ProximityColliders[i];
                if (col.TryGetComponent<IInteractable>(out var proximityTarget) &&
                    !interactables.Contains(proximityTarget) &&
                    !IsPhysicsBased(proximityTarget.TriggerMode))
                {
                    interactables.Add(proximityTarget);
                }
            }

            IInteractable bestTarget = null;
            int highestPriority = int.MinValue;

            foreach (var candidate in interactables)
            {
                if (!candidate.CanInteract(gameObject))
                {
                    continue;
                }

                if (bestTarget == null || candidate.Priority > highestPriority)
                {
                    bestTarget = candidate;
                    highestPriority = candidate.Priority;
                }
            }

            if (bestTarget != m_CurrentFocusedInteractable)
            {
                m_CurrentFocusedInteractable = bestTarget;

                // Automatically interact when entering focus for OnFocusEnter trigger mode
                if (m_CurrentFocusedInteractable != null && m_CurrentFocusedInteractable.TriggerMode == InteractionTriggerMode.OnFocusEnter)
                {
                    m_CurrentFocusedInteractable.Interact(gameObject);
                }
            }
        }

        /// <summary>
        /// Attempts to interact with the currently focused object via input.
        /// </summary>
        private void TryInteract()
        {
            if (!IsEnabled || m_CooldownTimer > 0 || m_CurrentFocusedInteractable == null) return;

            if (m_CurrentFocusedInteractable.TriggerMode == InteractionTriggerMode.OnButtonPress &&
                m_CurrentFocusedInteractable.CanInteract(gameObject))
            {
                m_CurrentFocusedInteractable.Interact(gameObject);
                m_CooldownTimer = interactionCooldown;
            }
        }

        #endregion
    }
}
