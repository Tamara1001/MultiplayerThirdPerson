using UnityEngine;
using Unity.Netcode;
using System.Collections;
using Blocks.Gameplay.Core;
using UnityEngine.Animations.Rigging;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Manages player aim targeting and animation rigging for the shooter system.
    /// Handles both precise aim positions (for shooting accuracy) and visual aim positions (for IK/animation).
    /// Synchronizes aim state across the network and applies recoil effects.
    /// </summary>
    public class AimController : NetworkBehaviour
    {
        #region Fields & Properties

        [Header("Aiming Configuration")]
        [Tooltip("The transform representing the aim point origin.")]
        [SerializeField] private Transform aimTransform;
        [Tooltip("The visual transform used for IK targeting in animations.")]
        [SerializeField] private Transform visualAimPointTransform;
        [Tooltip("Layer mask for aim raycasting to determine what can be targeted.")]
        [SerializeField] private LayerMask aimRaycastLayerMask = ~0;
        [Tooltip("Maximum distance for aim raycasting.")]
        [SerializeField] private float aimRaycastDistance = 100f;
        [Tooltip("Speed at which the visual aim point smoothly follows the target position.")]
        [SerializeField] private float aimSmoothingSpeed = 20f;
        [Tooltip("Speed at which the spine offset lerps to target values.")]
        [SerializeField] private float spineAimOffsetLerpSpeed = 15f;

        [Header("Visual Aiming Settings")]
        [Tooltip("When aiming at a close surface, the visual IK target will not get closer than this distance from the camera.")]
        [SerializeField] private float minVisualAimDistance = 2f;
        [Tooltip("The distance at which the visual aim point blends fully to the max distance.")]
        [SerializeField] private float visualAimBlendDistance = 15f;

        [Header("Network Optimization")]
        [Tooltip("Squared distance threshold to trigger a network update. Avoids frequent updates for tiny movements.")]
        [SerializeField] private float networkUpdateThresholdSqr = 0.01f;
        [Tooltip("The maximum number of aim updates sent per second. Prevents flooding the network.")]
        [SerializeField] private float maxNetworkUpdateRate = 30f;

        [Header("Animation Rigging")]
        [Tooltip("The rig component controlling aim animations.")]
        [SerializeField] private Rig aimRig;
        [Tooltip("The attachable node for weapon attachment.")]
        [SerializeField] private AttachableNode attachableNode;
        [Tooltip("The duration over which the rig values will smoothly transition.")]
        [SerializeField] private float riggingLerpDuration = 0.15f;

        [Header("Listening to Events")]
        [Tooltip("Event triggered when the aiming state changes.")]
        [SerializeField] private BoolEvent onAimingStateChanged;
        [Tooltip("Event triggered when the weapon is swapped.")]
        [SerializeField] private WeaponSwapEvent onWeaponChanged;
        [Tooltip("Event triggered when the weapon is fired.")]
        [SerializeField] private FloatEvent onWeaponFiredEvent;

        [Header("IK Constraints")]
        [Tooltip("The multi-aim constraint controlling spine rotation.")]
        [SerializeField] private MultiAimConstraint spineConstraint;

        [Header("Recoil Settings")]
        [Tooltip("Speed at which recoil is applied to the aim point.")]
        [SerializeField] private float recoilKickSpeed = 50f;

        [Header("Obstruction Settings")]
        [Tooltip("X offset applied to spine when weapon is obstructed.")]
        [SerializeField] private float obstructionSpineXOffset = -17f;

        private readonly NetworkVariable<Vector3> m_NetworkedPreciseAimPosition = new NetworkVariable<Vector3>();
        private readonly NetworkVariable<Vector3> m_NetworkedVisualAimPosition = new NetworkVariable<Vector3>();
        private readonly NetworkVariable<PlayerAimRiggingState> m_NetworkedAimRiggingState =
            new NetworkVariable<PlayerAimRiggingState>();

        private Camera m_MainCamera;
        private Vector3 m_CurrentRecoil;
        private Vector3 m_CurrentSpineOffset;
        private Vector3 m_CurrentLocalVisualAimPosition;
        private Vector3 m_TargetRecoilOffset = Vector3.zero;
        private Vector3 m_CurrentRecoilOffset = Vector3.zero;
        private float m_CurrentRecoilReturnSpeed = 10f;
        private float m_TimeOfLastNetworkUpdate;
        private WeaponData m_CurrentWeaponData;
        private Coroutine m_RiggingCoroutine;

        private bool m_IsAiming;
        private bool m_HasWeapon;
        private bool m_IsWeaponObstructed;

        /// <summary>
        /// Gets a value indicating whether the player is currently aiming.
        /// </summary>
        public bool IsAiming => m_IsAiming;

        /// <summary>
        /// Gets the transform representing the aim point origin.
        /// </summary>
        public Transform AimTransform => aimTransform;

        /// <summary>
        /// Gets the precise aim target position used for shooting accuracy calculations.
        /// This position is based on camera raycasts and represents the exact point where bullets should travel.
        /// </summary>
        public Vector3 PreciseAimTargetPosition => m_NetworkedPreciseAimPosition.Value;

        /// <summary>
        /// Gets the visual aim target position used for animation and IK rigging.
        /// This position is adaptively adjusted based on distance to prevent unnatural close-range IK poses.
        /// </summary>
        public Vector3 VisualAimTargetPosition => m_NetworkedVisualAimPosition.Value;

        /// <summary>
        /// Gets whether the weapon is currently obstructed by geometry.
        /// </summary>
        public bool IsWeaponObstructed => m_IsWeaponObstructed;

        #endregion

        #region Unity Methods

        private void Awake()
        {
            if (aimTransform == null)
            {
                Debug.LogError("AimController: Aim Transform is not assigned!", this);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            m_MainCamera = Camera.main;

            // Enable RigBuilder at runtime to avoid Edit mode warnings
            if (aimRig != null)
            {
                var rigBuilder = aimRig.GetComponentInParent<RigBuilder>();
                if (rigBuilder != null && !rigBuilder.enabled)
                {
                    rigBuilder.enabled = true;
                    rigBuilder.Build();
                }
            }

            if (IsOwner)
            {
                m_CurrentLocalVisualAimPosition = visualAimPointTransform.position;

                onWeaponChanged.RegisterListener(HandleWeaponChanged);
                onAimingStateChanged.RegisterListener(HandleAimingStateChanged);
                onWeaponFiredEvent.RegisterListener(HandleWeaponFired);

                UpdateAimPositions();
                UpdateRiggingState();
            }
            else
            {
                if (visualAimPointTransform != null)
                {
                    visualAimPointTransform.position = m_NetworkedVisualAimPosition.Value;
                    m_CurrentLocalVisualAimPosition = visualAimPointTransform.position;
                }
            }

            m_NetworkedPreciseAimPosition.OnValueChanged += OnNetworkedPreciseAimPositionChanged;
            m_NetworkedVisualAimPosition.OnValueChanged += OnNetworkedVisualAimPositionChanged;
            m_NetworkedAimRiggingState.OnValueChanged += OnNetworkedAimRiggingStateChanged;

            if (visualAimPointTransform != null)
            {
                m_CurrentLocalVisualAimPosition = m_NetworkedVisualAimPosition.Value;
                visualAimPointTransform.position = m_CurrentLocalVisualAimPosition;
            }

            if (!IsOwner)
            {
                m_IsAiming = m_NetworkedAimRiggingState.Value.isAiming;
                m_HasWeapon = m_NetworkedAimRiggingState.Value.hasWeapon;
            }
            ApplyRiggingState(m_NetworkedAimRiggingState.Value);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (IsOwner)
            {
                onWeaponChanged.UnregisterListener(HandleWeaponChanged);
                onAimingStateChanged.UnregisterListener(HandleAimingStateChanged);
                onWeaponFiredEvent.UnregisterListener(HandleWeaponFired);
            }

            m_NetworkedPreciseAimPosition.OnValueChanged -= OnNetworkedPreciseAimPositionChanged;
            m_NetworkedVisualAimPosition.OnValueChanged -= OnNetworkedVisualAimPositionChanged;
            m_NetworkedAimRiggingState.OnValueChanged -= OnNetworkedAimRiggingStateChanged;
        }

        private void Update()
        {
            if (!IsOwner || !IsSpawned) return;

            if (Time.time >= m_TimeOfLastNetworkUpdate + (1f / maxNetworkUpdateRate))
            {
                if (m_IsAiming)
                {
                    UpdateAimPositions();
                }
                else
                {
                    UpdateDefaultAimPositions();
                }
            }

            if (m_CurrentWeaponData != null)
            {
                UpdateAnimationRigging();
            }
        }

        private void LateUpdate()
        {
            // Apply recoil smoothing with kick and return speeds
            m_CurrentRecoilOffset = Vector3.Lerp(m_CurrentRecoilOffset, m_TargetRecoilOffset, Time.deltaTime * recoilKickSpeed);
            m_TargetRecoilOffset = Vector3.Lerp(m_TargetRecoilOffset, Vector3.zero, Time.deltaTime * m_CurrentRecoilReturnSpeed);

            if (visualAimPointTransform != null)
            {
                bool shouldShowVisualAimPoint = m_IsAiming || (!IsOwner && NetworkManager.IsConnectedClient);

                if (shouldShowVisualAimPoint)
                {
                    m_CurrentLocalVisualAimPosition = Vector3.Lerp(m_CurrentLocalVisualAimPosition,
                        m_NetworkedVisualAimPosition.Value, Time.deltaTime * aimSmoothingSpeed);
                    visualAimPointTransform.position = m_CurrentLocalVisualAimPosition + m_CurrentRecoilOffset;
                    visualAimPointTransform.gameObject.SetActive(true);
                }
                else
                {
                    visualAimPointTransform.gameObject.SetActive(false);
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets whether the weapon is currently obstructed by geometry.
        /// When obstructed, the spine offset is adjusted to reflect the weapon being pushed back.
        /// </summary>
        /// <param name="isObstructed">True if the weapon is obstructed, false otherwise.</param>
        public void SetWeaponObstructed(bool isObstructed)
        {
            m_IsWeaponObstructed = isObstructed;
        }

        /// <summary>
        /// Attempts to retrieve the current weapon index.
        /// For owners, this retrieves the index from the current weapon data.
        /// For non-owners, this retrieves the index from the WeaponController component.
        /// </summary>
        /// <param name="weaponIndex">The weapon index if available.</param>
        /// <returns>True if a weapon index was successfully retrieved, false otherwise.</returns>
        public bool TryGetWeaponIndex(out int weaponIndex)
        {
            if (IsOwner)
            {
                if (m_CurrentWeaponData != null)
                {
                    weaponIndex = m_CurrentWeaponData.weaponTypeID;
                    return true;
                }
            }
            else
            {
                var weaponController = GetComponent<WeaponController>();
                if (weaponController != null)
                {
                    weaponIndex = weaponController.GetWeaponIndex();
                    return true;
                }
            }
            weaponIndex = 0;
            return false;
        }

        /// <summary>
        /// Updates the spine weight for animation rigging overrides.
        /// This should only be called on the owner's instance.
        /// </summary>
        /// <param name="spineWeight">The new spine weight value to apply.</param>
        public void UpdateAnimationRiggingOverrides(float spineWeight)
        {
            if (!IsOwner) return;

            PlayerAimRiggingState state = m_NetworkedAimRiggingState.Value;
            state.spineWeight = spineWeight;
            m_NetworkedAimRiggingState.Value = state;
        }

        /// <summary>
        /// Resets the rigging state to default values and stops any active rigging coroutines.
        /// Clears aiming state and recoil offsets.
        /// </summary>
        public void ResetRiggingState()
        {
            if (m_RiggingCoroutine != null)
            {
                StopCoroutine(m_RiggingCoroutine);
                m_RiggingCoroutine = null;
            }

            m_IsAiming = false;
            m_TargetRecoilOffset = Vector3.zero;
            m_CurrentRecoilOffset = Vector3.zero;

            if (IsOwner)
            {
                var resetState = new PlayerAimRiggingState
                {
                    hasWeapon = m_HasWeapon,
                    isAiming = false
                };
                m_NetworkedAimRiggingState.Value = resetState;
            }
        }

        #endregion

        #region Private Methods

        private void HandleWeaponChanged(WeaponSwapPayload payload)
        {
            m_HasWeapon = payload.NewWeapon != null;
            if (m_HasWeapon)
            {
                if (payload.NewWeapon != null)
                {
                    var weaponData = payload.NewWeapon.GetWeaponData();
                    m_CurrentWeaponData = weaponData;
                    if (weaponData != null)
                    {
                        m_CurrentRecoil = weaponData.recoil;
                        m_CurrentRecoilReturnSpeed = weaponData.recoilReturnSpeed;
                        m_CurrentSpineOffset = weaponData.spineOffset;
                    }
                }
            }
            else
            {
                m_CurrentWeaponData = null;
                m_CurrentRecoilReturnSpeed = 10f;
            }
            if (IsOwner)
            {
                UpdateRiggingState();
            }
        }

        private void HandleAimingStateChanged(bool isAiming)
        {
            m_IsAiming = isAiming;
            if (IsOwner)
            {
                if (!isAiming)
                {
                    m_TargetRecoilOffset = Vector3.zero;
                    m_CurrentRecoilOffset = Vector3.zero;
                }
                else if (m_HasWeapon && m_CurrentWeaponData != null)
                {
                    // When entering aim state with a weapon, ensure spine weight is set
                    // This handles the case where the player respawns with a weapon already equipped
                    UpdateAnimationRiggingOverrides(m_CurrentWeaponData.spineAimWeight);
                }
                UpdateRiggingState();
            }
        }

        private void HandleWeaponFired(float fireRate)
        {
            if (!IsOwner) return;
            ApplyRecoilRpc(m_CurrentRecoil);
        }

        [Rpc(SendTo.Everyone)]
        private void ApplyRecoilRpc(Vector3 recoil)
        {
            m_TargetRecoilOffset += recoil;
        }

        private void UpdateAimPositions()
        {
            if (m_MainCamera == null)
            {
                UpdateFallbackAimPositions();
                return;
            }

            Ray screenCenterRay = m_MainCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));

            Vector3 newPreciseAimPosition;
            if (Physics.Raycast(screenCenterRay, out RaycastHit hit, aimRaycastDistance, aimRaycastLayerMask))
            {
                newPreciseAimPosition = hit.point;
            }
            else
            {
                newPreciseAimPosition = screenCenterRay.origin + screenCenterRay.direction * aimRaycastDistance;
            }

            // Calculate adaptive visual aim position that blends between min and max distance
            float hitDistance = Vector3.Distance(screenCenterRay.origin, newPreciseAimPosition);
            float blendT = Mathf.Clamp01(hitDistance / visualAimBlendDistance);
            float targetVisualDistance = Mathf.Lerp(minVisualAimDistance, aimRaycastDistance, blendT);
            Vector3 newVisualAimPosition = screenCenterRay.origin + screenCenterRay.direction * targetVisualDistance;

            bool preciseChanged = Vector3.SqrMagnitude(m_NetworkedPreciseAimPosition.Value - newPreciseAimPosition) > networkUpdateThresholdSqr;
            bool visualChanged = Vector3.SqrMagnitude(m_NetworkedVisualAimPosition.Value - newVisualAimPosition) > networkUpdateThresholdSqr;

            if (preciseChanged || visualChanged)
            {
                if (preciseChanged) m_NetworkedPreciseAimPosition.Value = newPreciseAimPosition;
                if (visualChanged) m_NetworkedVisualAimPosition.Value = newVisualAimPosition;
                m_TimeOfLastNetworkUpdate = Time.time;
            }
        }

        private void UpdateDefaultAimPositions()
        {
            Transform defaultAimSource = GetDefaultAimSource();
            Vector3 defaultPrecisePoint = defaultAimSource.position + defaultAimSource.forward * aimRaycastDistance;
            Vector3 defaultVisualPoint = defaultAimSource.position + defaultAimSource.forward * aimRaycastDistance;

            bool preciseChanged = Vector3.SqrMagnitude(m_NetworkedPreciseAimPosition.Value - defaultPrecisePoint) > networkUpdateThresholdSqr;
            bool visualChanged = Vector3.SqrMagnitude(m_NetworkedVisualAimPosition.Value - defaultVisualPoint) > networkUpdateThresholdSqr;

            if (preciseChanged || visualChanged)
            {
                if (preciseChanged) m_NetworkedPreciseAimPosition.Value = defaultPrecisePoint;
                if (visualChanged) m_NetworkedVisualAimPosition.Value = defaultVisualPoint;
                m_TimeOfLastNetworkUpdate = Time.time;
            }
        }

        private void UpdateFallbackAimPositions()
        {
            Transform fallbackAimSource = GetDefaultAimSource();
            Vector3 fallbackPoint = fallbackAimSource.position + fallbackAimSource.forward * aimRaycastDistance;

            m_NetworkedPreciseAimPosition.Value = fallbackPoint;
            m_NetworkedVisualAimPosition.Value = fallbackPoint;
            m_TimeOfLastNetworkUpdate = Time.time;
        }

        private Transform GetDefaultAimSource()
        {
            if (m_MainCamera != null) return m_MainCamera.transform;
            return aimTransform ?? transform;
        }

        private void UpdateAnimationRigging()
        {
            if (spineConstraint != null && m_CurrentWeaponData != null)
            {
                Vector3 targetSpineOffset = m_CurrentWeaponData.spineOffset;
                if (m_IsWeaponObstructed)
                {
                    targetSpineOffset.x += obstructionSpineXOffset;
                }

                m_CurrentSpineOffset = Vector3.Lerp(m_CurrentSpineOffset, targetSpineOffset, Time.deltaTime * spineAimOffsetLerpSpeed);
                PlayerAimRiggingState state = m_NetworkedAimRiggingState.Value;
                if (Vector3.SqrMagnitude(state.spineOffset - m_CurrentSpineOffset) > 0.00001f)
                {
                    state.spineOffset = m_CurrentSpineOffset;
                    m_NetworkedAimRiggingState.Value = state;
                }
            }
        }

        private void UpdateRiggingState()
        {
            PlayerAimRiggingState currentRigState = m_NetworkedAimRiggingState.Value;
            if (currentRigState.hasWeapon != m_HasWeapon || currentRigState.isAiming != m_IsAiming)
            {
                currentRigState.hasWeapon = m_HasWeapon;
                currentRigState.isAiming = m_IsAiming;
                m_NetworkedAimRiggingState.Value = currentRigState;
            }
        }

        private void OnNetworkedPreciseAimPositionChanged(Vector3 previousValue, Vector3 newValue)
        {
        }

        private void OnNetworkedVisualAimPositionChanged(Vector3 previousValue, Vector3 newValue)
        {
        }

        private void OnNetworkedAimRiggingStateChanged(PlayerAimRiggingState previousValue, PlayerAimRiggingState newValue)
        {
            if (!IsOwner)
            {
                m_IsAiming = newValue.isAiming;
                m_HasWeapon = newValue.hasWeapon;
            }
            ApplyRiggingState(newValue);
        }

        private void ApplyRiggingState(PlayerAimRiggingState state)
        {
            if (m_RiggingCoroutine != null)
            {
                StopCoroutine(m_RiggingCoroutine);
            }
            m_RiggingCoroutine = StartCoroutine(LerpRiggingStateCoroutine(state));
        }

        private IEnumerator LerpRiggingStateCoroutine(PlayerAimRiggingState targetState)
        {
            if (aimRig == null || spineConstraint == null)
            {
                yield break;
            }

            float elapsedTime = 0f;

            float startAimWeight = aimRig.weight;
            float startSpineWeight = spineConstraint.weight;
            Vector3 startSpineOffset = spineConstraint.data.offset;

            float targetAimWeight = targetState.hasWeapon ? (targetState.isAiming ? 1.0f : 0.0f) : 0.0f;
            float targetSpineWeight = targetState.spineWeight;
            Vector3 targetSpineOffset = targetState.spineOffset;

            while (elapsedTime < riggingLerpDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / riggingLerpDuration);

                aimRig.weight = Mathf.Lerp(startAimWeight, targetAimWeight, t);
                spineConstraint.weight = Mathf.Lerp(startSpineWeight, targetSpineWeight, t);

                var spineData = spineConstraint.data;
                spineData.offset = Vector3.Lerp(startSpineOffset, targetSpineOffset, t);
                spineConstraint.data = spineData;

                yield return null;
            }

            aimRig.weight = targetAimWeight;
            spineConstraint.weight = targetSpineWeight;
            var finalSpineData = spineConstraint.data;
            finalSpineData.offset = targetSpineOffset;
            spineConstraint.data = finalSpineData;

            m_RiggingCoroutine = null;
        }

        #endregion
    }
}
