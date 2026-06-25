using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// The central orchestrator for a Core player character.
    /// Refactored to support the Fallback Pattern: it can handle death automatically OR wait for GameManager.
    /// </summary>
    public class CorePlayerManager : NetworkBehaviour
    {
        #region Fields & Properties

        [Header("Core Components")]
        [Tooltip("Reference to the Core Input Handler component.")]
        [SerializeField] private CoreInputHandler coreInput;
        [Tooltip("Reference to the Core Movement component.")]
        [SerializeField] private CoreMovement coreMovement;
        [Tooltip("Reference to the Core Stats Handler component.")]
        [SerializeField] private CoreStatsHandler coreStats;
        [Tooltip("Reference to the Core Camera Controller component.")]
        [SerializeField] private CoreCameraController coreCamera;
        [Tooltip("Reference to the Core Player State component.")]
        [SerializeField] private CorePlayerState corePlayerState;

        [Header("Lifecycle Settings")]
        [Tooltip("If true, this component immediately processes elimination when health depletes. If false, it waits for the Game Manager to set the state.")]
        [SerializeField] private bool autoHandleLifecycle;

        [Header("Input Events")]
        [Tooltip("Event raised when movement input is received.")]
        [SerializeField] private Vector2Event onMoveInput;
        [Tooltip("Event raised when look input is received.")]
        [SerializeField] private Vector2Event onLookInput;
        [Tooltip("Event raised when jump button is pressed.")]
        [SerializeField] private GameEvent onJumpPressed;
        [Tooltip("Event raised when jump button is released.")]
        [SerializeField] private GameEvent onJumpReleased;
        [Tooltip("Event raised when sprint state changes.")]
        [SerializeField] private BoolEvent onSprintStateChanged;

        [Header("Game Events")]
        [Tooltip("Event raised when a player stat is depleted.")]
        [SerializeField] private StatDepletedEvent onStatDepleted;

        [Header("Sound Effects")]
        [Tooltip("Sound to play when the player respawns.")]
        [SerializeField] private SoundDef respawnSFX;

        private List<IPlayerAddon> m_Addons = new List<IPlayerAddon>();
        private PlayerLifeState m_LastLifeState = PlayerLifeState.InitialSpawn;
        private bool m_IsMovementInputEnabled = true;
        private System.Action<Vector2> m_OnMoveInputHandler;

        /// <summary>
        /// Gets the Core Input Handler component.
        /// </summary>
        public CoreInputHandler CoreInput => coreInput;

        /// <summary>
        /// Gets the Core Movement component.
        /// </summary>
        public CoreMovement CoreMovement => coreMovement;

        /// <summary>
        /// Gets the Core Stats Handler component.
        /// </summary>
        public CoreStatsHandler CoreStats => coreStats;

        /// <summary>
        /// Gets the Core Camera Controller component.
        /// </summary>
        public CoreCameraController CoreCamera => coreCamera;

        /// <summary>
        /// Gets the Core Player State component.
        /// </summary>
        public CorePlayerState PlayerState => corePlayerState;

        /// <summary>
        /// Gets the player's name from the Core Player State, or "Uninitialized" if not available.
        /// </summary>
        public string PlayerName => corePlayerState != null ? corePlayerState.PlayerName : "Uninitialized";

        /// <summary>
        /// Gets a value indicating whether this component automatically handles player lifecycle.
        /// If true, elimination is processed immediately when health depletes.
        /// If false, the Game Manager is expected to handle state transitions.
        /// </summary>
        public bool AutoHandleLifecycle => autoHandleLifecycle;

        #endregion

        #region Unity Methods

        private void Awake()
        {
            CacheComponentReferences();
            InitializeAddons();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                RegisterEventListeners();
            }
            else
            {
                DisableOwnerOnlyComponents();
            }

            if (corePlayerState != null)
            {
                corePlayerState.OnNameChanged += HandlePlayerNameChanged;
                corePlayerState.OnLifeStateChanged += HandleLifeStateChanged;

                if (!string.IsNullOrEmpty(corePlayerState.PlayerName))
                {
                    HandlePlayerNameChanged(corePlayerState.PlayerName);
                }

                m_LastLifeState = corePlayerState.LifeState;
                HandleLifeStateChanged(corePlayerState.LifeState);
            }

            foreach (var addon in m_Addons)
            {
                addon.OnPlayerSpawn();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (corePlayerState != null)
            {
                corePlayerState.OnNameChanged -= HandlePlayerNameChanged;
                corePlayerState.OnLifeStateChanged -= HandleLifeStateChanged;
            }

            if (IsOwner)
            {
                UnregisterEventListeners();
            }

            foreach (var addon in m_Addons)
            {
                addon.OnPlayerDespawn();
            }

            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsOwner) return;

            UpdateCameraTargetRotation();
            HandleSprintingStamina();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets whether movement input is enabled for this player.
        /// When disabled, all movement and sprint states are reset.
        /// This should only be called on the owner's instance.
        /// </summary>
        /// <param name="isEnabled">True to enable movement input, false to disable it.</param>
        public void SetMovementInputEnabled(bool isEnabled)
        {
            m_IsMovementInputEnabled = isEnabled;

            if (!isEnabled && coreMovement != null)
            {
                coreMovement.SetMoveInput(Vector2.zero);
                coreMovement.SetSprintState(false);
            }
        }

        #endregion

        #region Private Methods

        private void InitializeAddons()
        {
            if (m_Addons == null) m_Addons = new List<IPlayerAddon>();
            else m_Addons.Clear();

            GetComponents(m_Addons);

            foreach (var addon in m_Addons)
            {
                addon.Initialize(this);
            }
        }

        private void HandleSprint(bool isSprinting)
        {
            if (!m_IsMovementInputEnabled) return;

            if (coreStats == null || coreMovement == null)
            {
                Debug.LogWarning("[CorePlayerManager] coreStats or coreMovement is null in HandleSprint");
                return;
            }

            if (isSprinting && coreStats.GetCurrentValue(StatKeys.Stamina) < 1f)
            {
                coreMovement.SetSprintState(false);
                return;
            }
            coreMovement.SetSprintState(isSprinting);
        }

        private void HandleJump()
        {
            if (!m_IsMovementInputEnabled) return;

            if (coreMovement == null)
            {
                Debug.LogWarning("[CorePlayerManager] coreMovement is null in HandleJump");
                return;
            }

            if (!coreMovement.IsGrounded) return;

            if (coreStats == null)
            {
                Debug.LogWarning("[CorePlayerManager] coreStats is null in HandleJump");
                return;
            }

            float jumpStaminaCost = CoreMovement.GetAbilityStaminaCost<JumpAbility>();
            if (coreStats.TryConsumeStat(StatKeys.Stamina, jumpStaminaCost, OwnerClientId))
            {
                coreMovement.PerformJump();
            }
        }

        private void HandleMoveInput(Vector2 input)
        {
            if (!m_IsMovementInputEnabled) return;

            if (coreMovement == null)
            {
                Debug.LogWarning("[CorePlayerManager] coreMovement is null in HandleMoveInput");
                return;
            }

            coreMovement.SetMoveInput(input);
        }

        private void HandleStatDepleted(StatDepletedPayload payload)
        {
            if (!IsOwner || payload.playerId != OwnerClientId || payload.statID != StatKeys.Health) return;

            // Fallback Pattern: If autoHandleLifecycle is true, this component immediately processes
            // elimination. Otherwise, it waits for the Game Manager to detect the event and change the state.
            if (autoHandleLifecycle)
            {
                if (corePlayerState == null)
                {
                    Debug.LogWarning("[CorePlayerManager] corePlayerState is null in HandleStatDepleted");
                    return;
                }
                corePlayerState.SetLifeState(PlayerLifeState.Eliminated);
            }
        }

        private void HandleLifeStateChanged(PlayerLifeState newState)
        {
            PlayerLifeState previousState = m_LastLifeState;
            m_LastLifeState = newState;

            bool isEliminated = newState == PlayerLifeState.Eliminated;
            bool isActive = !isEliminated;

            if (IsOwner)
            {
                if (coreInput != null) coreInput.enabled = isActive;
                if (coreCamera != null) coreCamera.enabled = isActive;
                SetMovementInputEnabled(isActive);
            }

            if (coreMovement != null)
            {
                coreMovement.IsMovementEnabled = isActive;

                // Reset physics and play respawn effects when returning to life
                if (newState == PlayerLifeState.Respawned || newState == PlayerLifeState.InitialSpawn)
                {
                    if (respawnSFX != null)
                    {
                        CoreDirector.RequestAudio(respawnSFX)
                             .WithPosition(transform.position)
                             .Play();
                    }

                    coreMovement.ResetMovementForces();
                }

                // CharacterController must be disabled when player is eliminated to prevent physics glitches
                var characterController = coreMovement.GetComponent<CharacterController>();
                if (characterController != null) characterController.enabled = isActive;
            }

            foreach (var addon in m_Addons)
            {
                addon.OnLifeStateChanged(previousState, newState);
            }
        }

        private void HandlePlayerNameChanged(string newName)
        {
            if (string.IsNullOrEmpty(newName)) return;

            if (NetworkObject.IsPlayerObject)
            {
                gameObject.name = newName;
            }
        }

        private void RegisterEventListeners()
        {
            m_OnMoveInputHandler = HandleMoveInput;

            if (onMoveInput == null)
                Debug.LogWarning("[CorePlayerManager] onMoveInput is null in RegisterEventListeners");
            else
                onMoveInput.RegisterListener(m_OnMoveInputHandler);

            if (onLookInput == null || coreCamera == null)
                Debug.LogWarning("[CorePlayerManager] onLookInput or coreCamera is null in RegisterEventListeners");
            else
                onLookInput.RegisterListener(coreCamera.SetLookInput);

            if (onJumpPressed == null)
                Debug.LogWarning("[CorePlayerManager] onJumpPressed is null in RegisterEventListeners");
            else
                onJumpPressed.RegisterListener(HandleJump);

            if (onJumpReleased == null || coreMovement == null)
                Debug.LogWarning("[CorePlayerManager] onJumpReleased or coreMovement is null in RegisterEventListeners");
            else
                onJumpReleased.RegisterListener(coreMovement.OnVariableActionReleased);

            if (onSprintStateChanged == null)
                Debug.LogWarning("[CorePlayerManager] onSprintStateChanged is null in RegisterEventListeners");
            else
                onSprintStateChanged.RegisterListener(HandleSprint);

            if (onStatDepleted == null)
                Debug.LogWarning("[CorePlayerManager] onStatDepleted is null in RegisterEventListeners");
            else
                onStatDepleted.RegisterListener(HandleStatDepleted);
        }

        private void UnregisterEventListeners()
        {
            if (onMoveInput != null)
                onMoveInput.UnregisterListener(m_OnMoveInputHandler);

            if (onLookInput != null && coreCamera != null)
                onLookInput.UnregisterListener(coreCamera.SetLookInput);

            if (onJumpPressed != null)
                onJumpPressed.UnregisterListener(HandleJump);

            if (onJumpReleased != null && coreMovement != null)
                onJumpReleased.UnregisterListener(coreMovement.OnVariableActionReleased);

            if (onSprintStateChanged != null)
                onSprintStateChanged.UnregisterListener(HandleSprint);

            if (onStatDepleted != null)
                onStatDepleted.UnregisterListener(HandleStatDepleted);
        }

        private void CacheComponentReferences()
        {
            if (coreInput == null) coreInput = GetComponent<CoreInputHandler>();
            if (coreMovement == null) coreMovement = GetComponent<CoreMovement>();
            if (coreStats == null) coreStats = GetComponent<CoreStatsHandler>();
            if (coreCamera == null) coreCamera = GetComponent<CoreCameraController>();
            if (corePlayerState == null) corePlayerState = GetComponent<CorePlayerState>();
        }

        private void DisableOwnerOnlyComponents()
        {
            if (coreInput) coreInput.enabled = false;
            if (coreCamera) coreCamera.enabled = false;
        }

        private void UpdateCameraTargetRotation()
        {
            if (coreMovement != null && coreCamera != null)
            {
                coreMovement.SetTargetRotation(coreCamera.CurrentHorizontalLookAngle);
            }
        }

        private void HandleSprintingStamina()
        {
            if (coreMovement == null)
            {
                Debug.LogWarning("[CorePlayerManager] coreMovement is null in HandleSprintingStamina");
                return;
            }

            if (coreStats == null)
            {
                Debug.LogWarning("[CorePlayerManager] coreStats is null in HandleSprintingStamina");
                return;
            }

            if (!coreMovement.IsSprinting || coreMovement.CurrentSpeed <= 0.1f || !coreMovement.IsGrounded) return;

            float staminaToConsume = CoreMovement.GetAbilityStaminaCost<WalkAbility>() * Time.deltaTime;
            if (!coreStats.TryConsumeStat(StatKeys.Stamina, staminaToConsume, OwnerClientId))
            {
                coreMovement.SetSprintState(false);
            }
        }

        #endregion
    }
}
