using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// A versatile, network-aware interactable object whose behavior is defined by attaching one or more
    /// <see cref="IInteractionEffect"/> components. This modular approach allows for creating complex interactions
    /// (e.g., granting an item, playing a sound, and then despawning) by combining simple, reusable effect scripts.
    /// This component is an in-scene NetworkObject, meaning it must exist in the scene before the network session starts.
    /// See: https://docs-multiplayer.unity3d.com/netcode/current/basics/scenemanagement/inscene-placed-networkobjects/
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class ModularInteractable : NetworkBehaviour, IInteractable
    {
        #region Fields & Properties

        /// <summary>
        /// Gets the method by which this interaction is triggered (e.g., button press, collision).
        /// </summary>
        public InteractionTriggerMode TriggerMode => triggerMode;

        /// <summary>
        /// Gets the priority of this interactable. When multiple interactables are detected,
        /// the one with the highest priority value is chosen as the focus.
        /// </summary>
        public int Priority => priority;

        /// <summary>
        /// Gets the text to display on the UI prompt when this object is focused.
        /// Returns an empty string for interactions that are not triggered by a button press.
        /// </summary>
        public string InteractionPromptText =>
            triggerMode == InteractionTriggerMode.OnButtonPress ? promptText : string.Empty;

        [Header("Interaction Settings")]
        [Tooltip("Defines how the interaction is triggered (e.g., on button press, collision, or gaining focus).")]
        [SerializeField] private InteractionTriggerMode triggerMode = InteractionTriggerMode.OnButtonPress;

        [Tooltip("The priority of this interactable. Higher numbers are prioritized when multiple interactables are in range.")]
        [SerializeField] private int priority;

        [Tooltip("The text that will be displayed on the UI when this interactable is in focus.")]
        [SerializeField] private string promptText = "Interact";

        [Tooltip("Cooldown in seconds between automatic interactions (e.g., for OnCollision) to prevent spamming.")]
        [SerializeField] private float autoInteractionCooldown = 0.5f;

        [Tooltip("If true, this object can only be interacted with once. After the first interaction completes, it becomes non-interactable.")]
        [SerializeField] private bool interactOnce;

        [Header("Despawn Settings")]
        [Tooltip("If true, the object will request to be despawned from the network after being interacted with.")]
        [SerializeField] private bool despawnAfterInteraction;

        [Tooltip("The delay in seconds before the object despawns after a successful interaction. A value of 0 will cause an immediate despawn.")]
        [SerializeField] private float despawnDelay = 3.0f;

        [Header("Events")]
        [Tooltip("Event raised when a stat is depleted. Used to listen for player elimination.")]
        [SerializeField] private StatDepletedEvent onStatDepletedEvent;

        // Cached list of all IInteractionEffect components attached to this object
        private List<IInteractionEffect> m_InteractionEffects;

        // Dictionary tracking currently running effects and their coroutines
        private readonly Dictionary<IInteractionEffect, Coroutine> m_RunningEffects = new Dictionary<IInteractionEffect, Coroutine>();

        // Coroutine tracking the current interaction effect processing
        private Coroutine m_ProcessEffectsCoroutine;

        // GameObject that initiated the current interaction
        private GameObject m_CurrentInteractor;

        // Flag tracking whether effects have been initialized
        private bool m_HasInitializedEffects;

        // Flag tracking whether despawn sequence is active
        private bool m_IsDespawnSequenceActive;

        // Flag tracking whether an interaction is currently active
        private bool m_IsInteractionActive;

        // Flag tracking whether this object has been interacted with (for interactOnce)
        private bool m_HasBeenInteracted;

        // Cooldown timer for automatic physics-based interactions
        private float m_CooldownTimer;

        #endregion

        #region Unity Methods

        /// <summary>
        /// Manages the cooldown timer for automatic interactions.
        /// </summary>
        private void Update()
        {
            if (m_CooldownTimer > 0)
            {
                m_CooldownTimer -= Time.deltaTime;
            }
        }

        /// <summary>
        /// Called by Unity's physics engine when another collider enters this object's trigger.
        /// If the trigger mode is set to OnTriggerEnter, this will initiate the interaction.
        /// Only triggers for player-owned objects.
        /// </summary>
        /// <param name="other">The collider that entered the trigger.</param>
        private void OnTriggerEnter(Collider other)
        {
            // Only the owner of the entering object can trigger the interaction
            if (triggerMode == InteractionTriggerMode.OnTriggerEnter &&
                other.TryGetComponent<NetworkObject>(out var netObj) && netObj.IsOwner &&
                IsPlayer(other.gameObject))
            {
                Interact(other.gameObject);
            }
        }

        /// <summary>
        /// Called by Unity's physics engine when a collision occurs.
        /// If the trigger mode is set to OnRigidbodyCollision, this will initiate the interaction.
        /// Only triggers for player-owned objects.
        /// </summary>
        /// <param name="collision">The collision data.</param>
        private void OnCollisionEnter(Collision collision)
        {
            // Only the owner of the colliding object can trigger the interaction
            if (triggerMode == InteractionTriggerMode.OnRigidbodyCollision &&
                collision.gameObject.TryGetComponent<NetworkObject>(out var netObj) && netObj.IsOwner &&
                IsPlayer(collision.gameObject))
            {
                Interact(collision.gameObject);
            }
        }

        /// <summary>
        /// Initializes the object's state when it is spawned on the network.
        /// Validates interaction effects, subscribes to player elimination events, and resets state.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameObject.SetActive(true);
            m_CooldownTimer = 0f;
            m_HasBeenInteracted = false;
            if (HasAuthority)
            {
                m_IsDespawnSequenceActive = false;
                m_IsInteractionActive = false;
            }

            // Validate that there are interaction effects attached
            var effects = GetComponents<IInteractionEffect>();
            if (effects == null || effects.Length == 0)
            {
                Debug.LogWarning($"[ModularInteractable] {gameObject.name}: No IInteractionEffect components attached. This interactable will have no effect.", this);
            }

            // Subscribe to player elimination events
            onStatDepletedEvent?.RegisterListener(HandlePlayerEliminated);
        }

        /// <summary>
        /// Cleans up when the object is despawned from the network.
        /// Hides the object and unsubscribes from player elimination events.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            gameObject.SetActive(false);

            // Unsubscribe from player elimination events
            onStatDepletedEvent?.UnregisterListener(HandlePlayerEliminated);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Determines if the specified interactor can currently interact with this object.
        /// </summary>
        /// <param name="interactor">The GameObject attempting to interact.</param>
        /// <returns>True if the interaction is possible, false otherwise.</returns>
        public bool CanInteract(GameObject interactor)
        {
            bool hasCooldown = IsAutomaticPhysicsInteraction() && m_CooldownTimer > 0;
            bool hasBeenInteractedOnce = interactOnce && m_HasBeenInteracted;
            return this.enabled && IsSpawned && !m_IsInteractionActive && !hasCooldown && !hasBeenInteractedOnce;
        }

        /// <summary>
        /// Executes the primary interaction logic by processing all attached <see cref="IInteractionEffect"/> components.
        /// </summary>
        /// <param name="interactor">The GameObject that initiated the interaction.</param>
        public void Interact(GameObject interactor)
        {
            if (interactor == null)
            {
                Debug.LogWarning($"[ModularInteractable] {gameObject.name}: Interact called with null interactor.", this);
                return;
            }

            if (!IsPlayer(interactor))
            {
                Debug.LogWarning($"[ModularInteractable] {gameObject.name}: Interactor '{interactor.name}' is not a player.", this);
                return;
            }

            if (!CanInteract(interactor))
            {
                return;
            }

            InitializeEffects();

            if (IsAutomaticPhysicsInteraction())
            {
                m_CooldownTimer = autoInteractionCooldown;
            }

            m_ProcessEffectsCoroutine = StartCoroutine(ProcessInteractionEffects(interactor));
        }

        /// <summary>
        /// Initiates the server-authoritative process to despawn this network object.
        /// </summary>
        public void RequestDespawn()
        {
            RequestDespawnRpc();
        }

        /// <summary>
        /// Gets a collection of all interaction effects that are currently being executed.
        /// </summary>
        /// <returns>An enumerable collection of the running IInteractionEffect instances.</returns>
        public IEnumerable<IInteractionEffect> GetRunningEffects()
        {
            return m_RunningEffects.Keys;
        }

        /// <summary>
        /// Stops a specific, currently running interaction effect of a given type.
        /// </summary>
        /// <typeparam name="T">The type of the effect to stop.</typeparam>
        public void StopEffect<T>() where T : class, IInteractionEffect
        {
            IInteractionEffect effectToStop = null;
            foreach (var effect in m_RunningEffects.Keys)
            {
                if (effect is T)
                {
                    effectToStop = effect;
                    break;
                }
            }

            if (effectToStop != null && m_RunningEffects.TryGetValue(effectToStop, out Coroutine coroutine))
            {
                StopCoroutine(coroutine);
                effectToStop.CancelEffect(m_CurrentInteractor);
                m_RunningEffects.Remove(effectToStop);
            }
            else
            {
                Debug.LogWarning($"[ModularInteractable] {gameObject.name}: Cannot stop effect of type {typeof(T).Name}. Effect is not currently running.", this);
            }
        }

        /// <summary>
        /// Stops all currently running interaction effects and cancels the entire interaction sequence.
        /// </summary>
        public void StopAllEffects()
        {
            if (!m_IsInteractionActive) return;
            if (m_ProcessEffectsCoroutine != null)
            {
                StopCoroutine(m_ProcessEffectsCoroutine);
            }

            // Iterate over a copy as the original collection will be modified
            var activeEffects = new List<KeyValuePair<IInteractionEffect, Coroutine>>(m_RunningEffects.Count);
            foreach (var kvp in m_RunningEffects)
            {
                activeEffects.Add(kvp);
            }

            foreach (var runningEffect in activeEffects)
            {
                StopCoroutine(runningEffect.Value);
                runningEffect.Key.CancelEffect(m_CurrentInteractor);
            }

            m_RunningEffects.Clear();
            m_IsInteractionActive = false;
            m_CurrentInteractor = null;
            m_ProcessEffectsCoroutine = null;
        }

        #endregion

        #region RPCs

        /// <summary>
        /// An RPC sent to the server to request that this object be despawned.
        /// This ensures that despawning is handled authoritatively.
        /// </summary>
        [Rpc(SendTo.Authority)]
        private void RequestDespawnRpc()
        {
            if (m_IsDespawnSequenceActive)
            {
                Debug.LogWarning($"[ModularInteractable] {gameObject.name}: Despawn request ignored. Despawn sequence is already active.", this);
                return;
            }

            if (!IsSpawned)
            {
                Debug.LogWarning($"[ModularInteractable] {gameObject.name}: Despawn request ignored. Object is not spawned.", this);
                return;
            }

            StartCoroutine(DespawnSequence());
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Checks if the current trigger mode is one that is activated automatically by a physics event.
        /// </summary>
        /// <returns>True if the trigger mode is physics-based, false otherwise.</returns>
        private bool IsAutomaticPhysicsInteraction()
        {
            return triggerMode == InteractionTriggerMode.OnCharacterControllerHit ||
                   triggerMode == InteractionTriggerMode.OnTriggerEnter ||
                   triggerMode == InteractionTriggerMode.OnRigidbodyCollision;
        }

        /// <summary>
        /// Checks if the given GameObject is a player by looking for a <see cref="CorePlayerManager"/> component.
        /// </summary>
        /// <param name="obj">The GameObject to check.</param>
        /// <returns>True if the GameObject has a CorePlayerManager component, false otherwise.</returns>
        private bool IsPlayer(GameObject obj)
        {
            return obj.GetComponent<CorePlayerManager>() != null;
        }

        /// <summary>
        /// Lazily initializes the list of interaction effects by finding all components that implement
        /// <see cref="IInteractionEffect"/> on this GameObject and sorting them by priority (highest first).
        /// </summary>
        private void InitializeEffects()
        {
            if (m_HasInitializedEffects) return;

            var effectsArray = GetComponents<IInteractionEffect>();
            m_InteractionEffects = new List<IInteractionEffect>(effectsArray);
            m_InteractionEffects.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            if (m_InteractionEffects.Count == 0)
            {
                Debug.LogWarning($"[ModularInteractable] {gameObject.name}: No IInteractionEffect components found during initialization.", this);
            }

            m_HasInitializedEffects = true;
        }

        /// <summary>
        /// Coroutine that executes each attached <see cref="IInteractionEffect"/> sequentially in order of priority.
        /// Waits for each effect's coroutine to complete before starting the next one.
        /// After all effects are processed, may request to despawn itself if configured.
        /// </summary>
        /// <param name="interactor">The GameObject that initiated the interaction.</param>
        /// <returns>Enumerator for coroutine execution.</returns>
        private IEnumerator ProcessInteractionEffects(GameObject interactor)
        {
            m_IsInteractionActive = true;
            m_CurrentInteractor = interactor;

            if (m_InteractionEffects == null || m_InteractionEffects.Count == 0)
            {
                Debug.LogWarning($"[ModularInteractable] {gameObject.name}: No interaction effects to process.", this);
                m_IsInteractionActive = false;
                m_CurrentInteractor = null;
                m_ProcessEffectsCoroutine = null;
                yield break;
            }

            foreach (var effect in m_InteractionEffects)
            {
                if (effect == null)
                {
                    Debug.LogWarning($"[ModularInteractable] {gameObject.name}: Encountered null effect in interaction effects list. Skipping.", this);
                    continue;
                }

                // Check if interactor was destroyed during processing
                if (m_CurrentInteractor == null)
                {
                    Debug.LogWarning($"[ModularInteractable] {gameObject.name}: Interactor was destroyed during effect processing. Stopping remaining effects.", this);
                    break;
                }

                var effectRoutine = effect.ApplyEffect(interactor, this.gameObject);
                if (effectRoutine != null)
                {
                    Coroutine effectCoroutine = StartCoroutine(effectRoutine);
                    m_RunningEffects.Add(effect, effectCoroutine);
                    yield return effectCoroutine;
                    m_RunningEffects.Remove(effect);
                }
            }

            if (interactOnce)
            {
                m_HasBeenInteracted = true;
            }

            if (despawnAfterInteraction)
            {
                RequestDespawn();
            }

            m_IsInteractionActive = false;
            m_CurrentInteractor = null;
            m_ProcessEffectsCoroutine = null;
        }

        /// <summary>
        /// Server-side coroutine that handles the delayed despawning of the object.
        /// Waits for the configured delay, then despawns the NetworkObject if still valid.
        /// </summary>
        /// <returns>Enumerator for coroutine execution.</returns>
        private IEnumerator DespawnSequence()
        {
            m_IsDespawnSequenceActive = true;

            if (despawnDelay > 0f)
            {
                yield return new WaitForSeconds(despawnDelay);
            }

            // Check if the object still exists before trying to despawn it
            if (this == null)
            {
                yield break;
            }

            if (IsSpawned && HasAuthority)
            {
                NetworkObject.Despawn(false);
            }
            else
            {
                m_IsDespawnSequenceActive = false;
            }
        }

        /// <summary>
        /// Handles player elimination events from the stat depletion system.
        /// If the eliminated player is the current interactor, all running effects are stopped
        /// to prevent interaction with a dead player.
        /// </summary>
        /// <param name="payload">The stat depleted payload containing player ID and stat information.</param>
        private void HandlePlayerEliminated(StatDepletedPayload payload)
        {
            // Only handle Health depletion (player elimination)
            if (payload.statID != StatKeys.Health || !m_IsInteractionActive)
            {
                return;
            }

            // Check if the eliminated player is the current interactor
            if (m_CurrentInteractor == null)
            {
                return;
            }

            if (!m_CurrentInteractor.TryGetComponent<NetworkObject>(out var netObj))
            {
                Debug.LogWarning($"[ModularInteractable] {gameObject.name}: Current interactor '{m_CurrentInteractor.name}' does not have a NetworkObject component.", this);
                return;
            }

            if (netObj.OwnerClientId == payload.playerId)
            {
                StopAllEffects();
            }
        }

        #endregion
    }
}
