using UnityEngine;
using Unity.Netcode;
using System.Collections;
using Blocks.Gameplay.Core;
using Unity.Netcode.Components;
using System.Collections.Generic;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Manages weapon switching, attachment, and firing for networked characters.
    /// Handles weapon loadout spawning, state synchronization, and animation integration.
    /// This component works with <see cref="AimController"/> and synchronizes weapon state across the network.
    /// </summary>
    public class WeaponController : NetworkBehaviour
    {
        #region Fields & Properties

        [Header("Component Dependencies")]
        [Tooltip("Reference to the AimController that manages aiming behavior and aim target positions.")]
        [SerializeField] private AimController aimController;
        [Tooltip("Reference to the CoreStatsHandler for checking player alive state.")]
        [SerializeField] private CoreStatsHandler coreStats;

        [Header("Loadout Configuration")]
        [Tooltip("The weapon loadout configuration containing weapon prefabs and default weapon index.")]
        [SerializeField] private WeaponLoadout weaponLoadout;

        [Header("Listening to Events")]
        [Tooltip("Event raised when the primary action (fire) button is pressed.")]
        [SerializeField] private GameEvent onPrimaryActionPressedEvent;
        [Tooltip("Event raised when the primary action (fire) button is released.")]
        [SerializeField] private GameEvent onPrimaryActionReleasedEvent;
        [Tooltip("Event raised when the reload button is pressed.")]
        [SerializeField] private GameEvent onReloadPressedEvent;
        [Tooltip("Event raised when the next weapon button is pressed.")]
        [SerializeField] private GameEvent onNextWeaponPressedEvent;
        [Tooltip("Event raised when the previous weapon button is pressed.")]
        [SerializeField] private GameEvent onPreviousWeaponPressedEvent;
        [Tooltip("Event raised when the aiming state changes.")]
        [SerializeField] private BoolEvent onAimingStateChanged;

        [Header("Broadcasting on")]
        [Tooltip("Event raised when the active weapon changes, containing old and new weapon references.")]
        [SerializeField] private WeaponSwapEvent onWeaponChanged;

        private readonly List<AttachableBehaviour> m_SpawnedWeaponAttachables = new List<AttachableBehaviour>();
        private readonly List<IWeapon> m_WeaponImplementations = new List<IWeapon>();
        private readonly List<string> m_WeaponPrefabNames = new List<string>();
        private readonly Dictionary<string, AttachableNode> m_AttachmentNodes = new Dictionary<string, AttachableNode>();
        private readonly float m_WeaponSwitchAnimationSyncTime = 0.45f;
        private readonly NetworkVariable<PlayerWeaponState> m_PlayerWeaponState = new NetworkVariable<PlayerWeaponState>(
            new PlayerWeaponState(),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        private IWeapon m_CurrentWeapon;
        private AttachableBehaviour m_CurrentAttachableWeapon;
        private int m_CurrentWeaponIndex;
        private int m_AnimIDWeaponType;
        private bool m_IsAiming;
        private Animator m_Animator;
        private Coroutine m_WeaponSwitchCoroutine;

        static readonly int k_IsSwitchingWeapon = Animator.StringToHash("IsSwitchingWeapon");
        static readonly int k_AnimIDIsReloading = Animator.StringToHash("IsReloading");

        /// <summary>
        /// Gets the currently equipped weapon implementation.
        /// </summary>
        public IWeapon CurrentWeapon => m_CurrentWeapon;

        /// <summary>
        /// Gets the index of the currently equipped weapon in the weapon list.
        /// </summary>
        public int CurrentWeaponIndex => m_CurrentWeaponIndex;

        /// <summary>
        /// Gets the total number of weapons in the loadout.
        /// </summary>
        public int WeaponCount => m_WeaponImplementations?.Count ?? 0;

        #endregion

        #region Unity Methods

        private void Awake()
        {
            m_Animator = GetComponentInChildren<Animator>();
            m_AnimIDWeaponType = Animator.StringToHash("WeaponTypeID");

            var allNodes = GetComponentsInChildren<AttachableNode>(true);
            foreach (var node in allNodes)
            {
                if (!m_AttachmentNodes.TryAdd(node.gameObject.name, node))
                {
                    Debug.LogWarning($"Duplicate AttachableNode name found: {node.gameObject.name}. Only the first one will be used.", this);
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                onPrimaryActionPressedEvent.RegisterListener(HandleFirePressed);
                onPrimaryActionReleasedEvent.RegisterListener(HandleFireReleased);
                onReloadPressedEvent.RegisterListener(HandleReloadPressed);
                onNextWeaponPressedEvent.RegisterListener(HandleNextWeaponPressed);
                onPreviousWeaponPressedEvent.RegisterListener(HandlePreviousWeaponPressed);
                onAimingStateChanged.RegisterListener(HandleAimingStateChanged);

                m_PlayerWeaponState.Value = new PlayerWeaponState { hasWeapon = false, isAiming = false, weaponIndex = 0 };

                if (weaponLoadout != null && weaponLoadout.weaponPrefabs.Count > 0)
                {
                    StartCoroutine(SpawnLoadoutWeaponsCoroutine());
                }
            }

            ApplyPlayerWeaponState();
            m_PlayerWeaponState.OnValueChanged += OnPlayerWeaponStateChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                onPrimaryActionPressedEvent.UnregisterListener(HandleFirePressed);
                onPrimaryActionReleasedEvent.UnregisterListener(HandleFireReleased);
                onReloadPressedEvent.UnregisterListener(HandleReloadPressed);
                onNextWeaponPressedEvent.UnregisterListener(HandleNextWeaponPressed);
                onPreviousWeaponPressedEvent.UnregisterListener(HandlePreviousWeaponPressed);
                onAimingStateChanged.UnregisterListener(HandleAimingStateChanged);

                foreach (var attachableWeapon in m_SpawnedWeaponAttachables)
                {
                    if (attachableWeapon != null && attachableWeapon.NetworkObject != null && attachableWeapon.NetworkObject.IsSpawned)
                    {
                        attachableWeapon.NetworkObject.Despawn();
                    }
                }
            }

            m_SpawnedWeaponAttachables.Clear();
            m_WeaponImplementations.Clear();
            m_WeaponPrefabNames.Clear();
            m_PlayerWeaponState.OnValueChanged -= OnPlayerWeaponStateChanged;

            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsOwner) return;
            UpdatePlayerWeaponState();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Checks if the specified weapon prefab is already in the spawned weapon list.
        /// </summary>
        /// <param name="weaponPrefab">The weapon prefab GameObject to check.</param>
        /// <returns>True if the weapon prefab is already spawned, false otherwise.</returns>
        public bool HasWeaponPrefab(GameObject weaponPrefab)
        {
            if (weaponPrefab == null || m_WeaponPrefabNames == null)
                return false;

            string weaponPrefabName = weaponPrefab.name.Replace("(Clone)", "").Trim();

            foreach (var existingWeaponName in m_WeaponPrefabNames)
            {
                if (string.Equals(weaponPrefabName, existingWeaponName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the weapon index from the current player weapon state.
        /// </summary>
        /// <returns>The current weapon index.</returns>
        public int GetWeaponIndex()
        {
            return m_PlayerWeaponState.Value.weaponIndex;
        }

        /// <summary>
        /// Sets the active state of the current weapon's visual components.
        /// </summary>
        /// <param name="isActive">Whether the weapon should be active or inactive.</param>
        public void SetCurrentWeaponActive(bool isActive)
        {
            if (m_CurrentAttachableWeapon != null)
            {
                var weaponObjectController = m_CurrentAttachableWeapon.GetComponent<ComponentController>();
                if (weaponObjectController != null)
                {
                    weaponObjectController.SetEnabled(isActive);
                }
            }
        }

        #endregion

        #region Private Methods

        private void HandleAimingStateChanged(bool isAiming)
        {
            m_IsAiming = isAiming;

            if (!isAiming)
            {
                m_CurrentWeapon?.StopFiring();
            }

            if (m_CurrentAttachableWeapon != null && m_CurrentAttachableWeapon.HasAuthority)
            {
                m_CurrentAttachableWeapon.Detach();
            }

            UpdateWeaponAttachment();
        }

        private void HandleNextWeaponPressed()
        {
            if (!coreStats.IsAlive || CurrentWeapon.GetCurrentState() == WeaponState.Reloading || WeaponCount <= 1) return;
            if (aimController != null && aimController.IsWeaponObstructed) return;
            SwitchToWeapon((m_CurrentWeaponIndex + 1) % WeaponCount);
        }

        private void HandlePreviousWeaponPressed()
        {
            if (!coreStats.IsAlive || CurrentWeapon.GetCurrentState() == WeaponState.Reloading || WeaponCount <= 1) return;
            if (aimController != null && aimController.IsWeaponObstructed) return;
            SwitchToWeapon((m_CurrentWeaponIndex - 1 + WeaponCount) % WeaponCount);
        }

        private void HandleFirePressed()
        {
            if (!coreStats.IsAlive ||
                CurrentWeapon == null ||
                !CurrentWeapon.CanFire() ||
                !m_IsAiming ||
                m_Animator.GetBool(k_IsSwitchingWeapon) ||
                m_Animator.GetBool(k_AnimIDIsReloading)) return;

            Vector3 fireOrigin = Camera.main != null ? Camera.main.transform.position : transform.position;
            Vector3 aimTargetPosition = aimController.PreciseAimTargetPosition;

            Transform muzzleTransform = (CurrentWeapon as ModularWeapon)?.Muzzle ?? aimController.AimTransform;
            Vector3 fireDirection = (aimTargetPosition - muzzleTransform.position).normalized;

            CurrentWeapon.Fire(gameObject, fireOrigin, fireDirection);
        }

        private void HandleFireReleased()
        {
            m_CurrentWeapon?.StopFiring();
        }

        private void HandleReloadPressed()
        {
            if (!coreStats.IsAlive || CurrentWeapon == null || CurrentWeapon.GetCurrentState() == WeaponState.Reloading) return;

            if (CurrentWeapon is ModularWeapon simpleWeapon && simpleWeapon.NeedsReload())
            {
                CurrentWeapon.TryReload();
            }
        }

        private void OnPlayerWeaponStateChanged(PlayerWeaponState previous, PlayerWeaponState current)
        {
            // Non-owners synchronize weapon changes from the network state
            if (!IsOwner && previous.weaponIndex != current.weaponIndex)
            {
                SetActiveWeapon(current.weaponIndex);
            }
            ApplyPlayerWeaponState();
        }

        private void ApplyPlayerWeaponState()
        {
            if (m_Animator == null) return;

            // Set animator layer weight based on whether the player has a weapon equipped
            m_Animator.SetLayerWeight(1, m_PlayerWeaponState.Value.hasWeapon ? 1.0f : 0.0f);
        }

        private void UpdatePlayerWeaponState()
        {
            if (!IsSpawned) return;

            var currentState = m_PlayerWeaponState.Value;
            if (currentState.UpdateState(CurrentWeapon != null, m_IsAiming, m_CurrentWeaponIndex))
            {
                m_PlayerWeaponState.Value = currentState;
            }
        }

        private IEnumerator SpawnLoadoutWeaponsCoroutine()
        {
            yield return null;

            foreach (var weaponPrefab in weaponLoadout.weaponPrefabs)
            {
                if (weaponPrefab == null) continue;

                GameObject weaponContainer = Instantiate(weaponPrefab);
                NetworkObject weaponNetworkObject = weaponContainer.GetComponent<NetworkObject>();
                if (weaponNetworkObject == null) { Destroy(weaponContainer); continue; }

                AttachableBehaviour attachableWeapon = weaponContainer.GetComponentInChildren<AttachableBehaviour>();
                if (attachableWeapon == null) { Destroy(weaponContainer); continue; }

                ModularWeapon weaponComponent = attachableWeapon.GetComponent<ModularWeapon>();
                if (weaponComponent == null) { Destroy(weaponContainer); continue; }

                weaponNetworkObject.SpawnWithOwnership(OwnerClientId);
                yield return new WaitUntil(() => weaponNetworkObject.IsSpawned);

                weaponContainer.SetActive(false);
                m_SpawnedWeaponAttachables.Add(attachableWeapon);
                m_WeaponImplementations.Add(weaponComponent);
                m_WeaponPrefabNames.Add(weaponPrefab.name.Replace("(Clone)", "").Trim());
            }

            if (m_WeaponImplementations.Count > 0)
            {
                int startIndex = Mathf.Clamp(weaponLoadout.defaultWeaponIndex, 0, m_WeaponImplementations.Count - 1);
                SetActiveWeapon(startIndex);
            }
        }

        private void SwitchToWeapon(int index)
        {
            if (!IsOwner) return;
            SetActiveWeapon(index);
            UpdatePlayerWeaponState();
        }

        private IEnumerator AnimationSyncedWeaponSwitch(int index, IWeapon oldWeapon)
        {
            // TODO: Fix animation timing to match in a better way by using animation events
            // Wait for the weapon switching animation to reach the swap point
            if (oldWeapon != null && oldWeapon != m_WeaponImplementations[index] && m_IsAiming)
            {
                m_Animator.SetBool(k_IsSwitchingWeapon, true);
                yield return new WaitForSeconds(m_WeaponSwitchAnimationSyncTime);
            }

            // Detach the old weapon from the attachment node
            if (m_CurrentAttachableWeapon != null)
            {
                if (m_CurrentAttachableWeapon.HasAuthority) m_CurrentAttachableWeapon.Detach();
            }

            // Update to the new weapon
            m_CurrentWeaponIndex = index;
            m_CurrentAttachableWeapon = m_SpawnedWeaponAttachables[m_CurrentWeaponIndex];
            m_CurrentWeapon = m_WeaponImplementations[m_CurrentWeaponIndex];

            if (m_CurrentAttachableWeapon != null)
            {
                UpdateWeaponAttachment();
            }

            // Raise weapon swap event for owner
            if (IsOwner && onWeaponChanged != null && oldWeapon != m_CurrentWeapon)
            {
                onWeaponChanged.Raise(new WeaponSwapPayload { OldWeapon = oldWeapon, NewWeapon = m_CurrentWeapon });
            }

            // Update animator with new weapon type
            if (m_CurrentWeapon != null)
            {
                var weaponData = m_CurrentWeapon.GetWeaponData();
                if (weaponData != null)
                {
                    m_Animator.SetInteger(m_AnimIDWeaponType, weaponData.weaponTypeID);
                }
            }

            // Update animation rigging overrides for the new weapon
            if (m_CurrentAttachableWeapon != null)
            {
                if (aimController != null && m_CurrentWeapon != null)
                {
                    var weaponData = m_CurrentWeapon.GetWeaponData();
                    if (weaponData != null)
                    {
                        aimController.UpdateAnimationRiggingOverrides(weaponData.spineAimWeight);
                    }
                }
            }

            // Reset weapon switching animator state and coroutine reference
            m_Animator.SetBool(k_IsSwitchingWeapon, false);
            m_WeaponSwitchCoroutine = null;
        }

        private void SetActiveWeapon(int index)
        {
            if (m_WeaponImplementations == null || m_WeaponImplementations.Count == 0)
            {
                m_CurrentWeapon = null;
                m_CurrentAttachableWeapon = null;
                return;
            }

            IWeapon oldWeapon = m_CurrentWeapon;
            oldWeapon?.StopFiring();

            // Reset obstruction state on the old weapon to prevent stuck animator states
            if (oldWeapon is ModularWeapon modularWeapon)
            {
                modularWeapon.ResetObstructionState();
            }

            // Cancel any in-progress weapon switch coroutine
            if (m_WeaponSwitchCoroutine != null)
            {
                StopCoroutine(m_WeaponSwitchCoroutine);
                m_Animator.SetBool(k_IsSwitchingWeapon, false);
            }

            m_WeaponSwitchCoroutine = StartCoroutine(AnimationSyncedWeaponSwitch(index, oldWeapon));
        }

        private void UpdateWeaponAttachment()
        {
            if (m_CurrentAttachableWeapon == null || !m_CurrentAttachableWeapon.HasAuthority || m_CurrentWeapon == null)
            {
                return;
            }

            WeaponData weaponData = m_CurrentWeapon.GetWeaponData();
            if (weaponData == null)
            {
                Debug.LogWarning($"Current weapon '{m_CurrentAttachableWeapon.name}' is missing WeaponData.", this);
                return;
            }

            // Select the appropriate attachment node based on aiming state
            string targetNodeName = m_IsAiming ? weaponData.handAttachmentNodeName : weaponData.idleAttachmentNodeName;

            if (!string.IsNullOrEmpty(targetNodeName) && m_AttachmentNodes.TryGetValue(targetNodeName, out AttachableNode targetNode))
            {
                if (targetNode != null)
                {
                    m_CurrentAttachableWeapon.Attach(targetNode);
                }
            }
            else if (!string.IsNullOrEmpty(targetNodeName))
            {
                Debug.LogWarning($"AttachableNode named '{targetNodeName}' not found for weapon '{weaponData.weaponName}'.", this);
            }
        }

        #endregion
    }
}
