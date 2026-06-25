using UnityEngine;
using Unity.Netcode;
using Unity.Cinemachine;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Manages the player's camera rig, handling look input and switching between different camera modes.
    /// This component works with Cinemachine to control the active virtual camera by adjusting priorities.
    /// It should be attached to the player prefab and will only activate for the local owner.
    /// </summary>
    public class CoreCameraController : NetworkBehaviour
    {
        #region Fields & Properties

        [Header("Camera Setup")]
        [Tooltip("List of camera mode prefabs to instantiate. Each should have a CoreCameraMode component.")]
        [SerializeField] private List<CoreCameraMode> cameraModePrefabs = new List<CoreCameraMode>();
        [Tooltip("The transform the camera should follow and look at. If null, this component's transform is used.")]
        [SerializeField] private Transform lookTarget;
        [Tooltip("The name of the camera mode to activate on spawn.")]
        [SerializeField] private string initialModeName = "FreeLook";

        [Header("Default Look Settings")]
        [Tooltip("Whether to process look input from the input handler.")]
        [SerializeField] private bool enableLookInput = true;
        [Tooltip("Default sensitivity for look input (used when camera doesn't override).")]
        [SerializeField] private float defaultLookSensitivity = 1.0f;
        [Tooltip("Default maximum angle in degrees the camera can look up or down (used when camera doesn't override).")]
        [SerializeField] private float defaultVerticalLookLimit = 70.0f;

        [Header("Listening to Events")]
        [Tooltip("Event that provides look input from the CoreInputHandler.")]
        [SerializeField] private Vector2Event onLookInput;

        /// <summary>
        /// Gets the current horizontal rotation angle of the look target.
        /// </summary>
        public float CurrentHorizontalLookAngle => m_CurrentHorizontalLookAngle;

        /// <summary>
        /// Gets the currently active player rotation coupling mode, determined by the active camera mode.
        /// </summary>
        public CoreMovement.CouplingMode CurrentPlayerRotationMode { get; private set; }

        /// <summary>
        /// Gets the currently active camera mode.
        /// </summary>
        public CoreCameraMode ActiveCameraMode { get; private set; }

        // Internal state for look angles and sensitivity.
        private float m_CurrentVerticalLookAngle;
        private float m_CurrentHorizontalLookAngle;
        private float m_LookSensitivity;
        private float m_VerticalLookLimit;

        // Runtime references.
        private CinemachineBrain m_CinemachineBrain;
        private readonly List<CoreCameraMode> m_RegisteredCameraModes = new List<CoreCameraMode>();
        // Tracks which camera modes were instantiated by this controller and should be destroyed on despawn.
        private readonly HashSet<CoreCameraMode> m_InstantiatedCameraModes = new HashSet<CoreCameraMode>();

        #endregion

        #region Unity & Network Lifecycle

        private void Awake()
        {
            m_LookSensitivity = defaultLookSensitivity;
            m_VerticalLookLimit = defaultVerticalLookLimit;

            if (lookTarget == null)
            {
                lookTarget = transform;
            }

            if(onLookInput == null && enableLookInput)
            {
                Debug.LogError("[CoreCameraController] Look input event is not assigned.", this);
            }

            // Initialize horizontal angle based on the initial rotation of the player object.
            m_CurrentHorizontalLookAngle = transform.rotation.eulerAngles.y;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // This component is only active for the local player who owns this object.
            if (IsOwner)
            {
                SetupCinemachine();
                SwitchCameraMode(initialModeName);
                if (enableLookInput)
                {
                    onLookInput.RegisterListener(SetLookInput);
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                // Only destroy camera modes that were instantiated by this controller.
                // External/scene cameras are not destroyed.
                foreach (var mode in m_InstantiatedCameraModes)
                {
                    if (mode != null)
                    {
                        Destroy(mode.gameObject);
                    }
                }
                m_InstantiatedCameraModes.Clear();
                m_RegisteredCameraModes.Clear();

                if (enableLookInput)
                {
                    onLookInput.UnregisterListener(SetLookInput);
                }
            }
            base.OnNetworkDespawn();
        }

        private void LateUpdate()
        {
            // We update the camera rotation in LateUpdate to ensure all character movement for the frame has been processed.
            // This prevents visual jitter.
            if (lookTarget != null && IsOwner && enableLookInput)
            {
                Quaternion horizontalRotation = Quaternion.Euler(0f, m_CurrentHorizontalLookAngle, 0f);
                Quaternion verticalRotation = Quaternion.Euler(m_CurrentVerticalLookAngle, 0f, 0f);
                lookTarget.rotation = horizontalRotation * verticalRotation;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Sets the look input, updating the horizontal and vertical look angles.
        /// </summary>
        /// <param name="lookInput">The 2D vector from the input device.</param>
        public void SetLookInput(Vector2 lookInput)
        {
            if (!IsOwner || !enableLookInput) return;

            m_CurrentHorizontalLookAngle += lookInput.x * m_LookSensitivity;

            // Clamp the vertical angle to prevent the camera from flipping over.
            m_CurrentVerticalLookAngle = Mathf.Clamp(m_CurrentVerticalLookAngle - (lookInput.y * m_LookSensitivity), -m_VerticalLookLimit, m_VerticalLookLimit);
        }

        /// <summary>
        /// Sets the look sensitivity at runtime.
        /// </summary>
        /// <param name="sensitivity">The new sensitivity value.</param>
        public void SetLookSensitivity(float sensitivity)
        {
            m_LookSensitivity = sensitivity;
        }

        /// <summary>
        /// Switches the active camera mode by changing Cinemachine virtual camera priorities.
        /// </summary>
        /// <param name="modeName">The name of the camera mode to activate.</param>
        /// <returns>True if the mode was found and switched successfully, false otherwise.</returns>
        public bool SwitchCameraMode(string modeName)
        {
            CoreCameraMode targetMode = m_RegisteredCameraModes.Find(m => m.ModeName == modeName);
            if (targetMode == null)
            {
                Debug.LogWarning($"Camera mode '{modeName}' not found.");
                return false;
            }

            if (targetMode.CinemachineCamera == null)
            {
                Debug.LogWarning($"CinemachineCamera for mode '{modeName}' is not available.");
                return false;
            }

            // Update the active camera mode reference.
            ActiveCameraMode = targetMode;

            // Update the current player rotation mode.
            CurrentPlayerRotationMode = targetMode.PlayerRotationMode;

            // Apply look settings from the camera mode or use defaults.
            if (targetMode.OverrideLookSettings)
            {
                m_LookSensitivity = targetMode.LookSensitivity;
                m_VerticalLookLimit = targetMode.VerticalLookLimit;
            }
            else
            {
                m_LookSensitivity = defaultLookSensitivity;
                m_VerticalLookLimit = defaultVerticalLookLimit;
            }

            // Set priorities: the target camera gets active priority, all others get inactive.
            foreach (var mode in m_RegisteredCameraModes)
            {
                if (mode != null)
                {
                    mode.SetActive(mode == targetMode);
                }
            }

            return true;
        }

        /// <summary>
        /// Registers a camera mode at runtime by instantiating from a prefab.
        /// The instantiated camera will be destroyed when the player despawns.
        /// For scene cameras that should not be destroyed, use RegisterCameraModeInstance instead.
        /// </summary>
        /// <param name="cameraModePrefab">The camera mode prefab to instantiate and register.</param>
        /// <returns>The registered CoreCameraMode instance, or null if registration failed.</returns>
        public CoreCameraMode RegisterCameraMode(CoreCameraMode cameraModePrefab)
        {
            if (cameraModePrefab == null)
            {
                Debug.LogWarning("[CoreCameraController] Cannot register a null camera mode prefab.", this);
                return null;
            }

            // Validate no duplicate mode names exist to prevent ambiguous SwitchCameraMode calls.
            if (m_RegisteredCameraModes.Exists(m => m.ModeName == cameraModePrefab.ModeName))
            {
                Debug.LogWarning($"[CoreCameraController] A camera mode with name '{cameraModePrefab.ModeName}' is already registered. Duplicate names are not allowed.", this);
                return null;
            }

            // Always instantiate the prefab to avoid modifying the original asset.
            CoreCameraMode modeToRegister = Instantiate(cameraModePrefab);

            // Set up targets and add to the registered list.
            modeToRegister.SetTargets(lookTarget);
            m_RegisteredCameraModes.Add(modeToRegister);
            m_InstantiatedCameraModes.Add(modeToRegister);

            // Ensure the newly registered mode is inactive by default.
            modeToRegister.SetActive(false);

            return modeToRegister;
        }

        /// <summary>
        /// Registers an existing in-scene camera mode instance. Use this for cameras already placed in the scene.
        /// </summary>
        /// <param name="cameraModeInstance">The camera mode instance to register.</param>
        /// <returns>True if registration was successful, false otherwise.</returns>
        public bool RegisterCameraModeInstance(CoreCameraMode cameraModeInstance)
        {
            if (cameraModeInstance == null)
            {
                Debug.LogWarning("[CoreCameraController] Cannot register a null camera mode instance.", this);
                return false;
            }

            if (m_RegisteredCameraModes.Contains(cameraModeInstance))
            {
                Debug.LogWarning($"[CoreCameraController] Camera mode instance '{cameraModeInstance.ModeName}' is already registered.", this);
                return false;
            }

            // Validate no duplicate mode names exist to prevent ambiguous SwitchCameraMode calls.
            if (m_RegisteredCameraModes.Exists(m => m.ModeName == cameraModeInstance.ModeName))
            {
                Debug.LogWarning($"[CoreCameraController] A camera mode with name '{cameraModeInstance.ModeName}' is already registered. Duplicate names are not allowed.", this);
                return false;
            }

            // Set up targets and add to the registered list.
            cameraModeInstance.SetTargets(lookTarget);
            m_RegisteredCameraModes.Add(cameraModeInstance);

            // Ensure the newly registered mode is inactive by default.
            cameraModeInstance.SetActive(false);

            return true;
        }

        /// <summary>
        /// Unregisters a camera mode at runtime.
        /// </summary>
        /// <param name="cameraMode">The camera mode to unregister.</param>
        /// <param name="destroy">If true, destroys the camera mode GameObject. If false, only removes from the list.</param>
        /// <returns>True if the mode was found and unregistered, false otherwise.</returns>
        public bool UnregisterCameraMode(CoreCameraMode cameraMode, bool destroy = false)
        {
            if (cameraMode == null)
            {
                Debug.LogWarning("[CoreCameraController] Cannot unregister a null camera mode.", this);
                return false;
            }

            if (!m_RegisteredCameraModes.Contains(cameraMode))
            {
                Debug.LogWarning($"[CoreCameraController] Camera mode '{cameraMode.ModeName}' is not registered.", this);
                return false;
            }

            // If this was the active camera mode, we need to switch to another one.
            if (ActiveCameraMode == cameraMode)
            {
                // Find another mode to switch to before clearing the active mode.
                CoreCameraMode fallbackMode = null;
                foreach (var mode in m_RegisteredCameraModes)
                {
                    if (mode != cameraMode && mode != null)
                    {
                        fallbackMode = mode;
                        break;
                    }
                }

                // Switch to the fallback mode if found, otherwise clear the active mode.
                if (fallbackMode != null)
                {
                    SwitchCameraMode(fallbackMode.ModeName);
                }
                else
                {
                    ActiveCameraMode = null;
                    CurrentPlayerRotationMode = CoreMovement.CouplingMode.Decoupled;
                }
            }

            m_RegisteredCameraModes.Remove(cameraMode);
            m_InstantiatedCameraModes.Remove(cameraMode);

            if (destroy && cameraMode != null)
            {
                Destroy(cameraMode.gameObject);
            }

            return true;
        }

        /// <summary>
        /// Unregisters a camera mode by name.
        /// </summary>
        /// <param name="modeName">The name of the camera mode to unregister.</param>
        /// <param name="destroy">If true, destroys the camera mode GameObject. If false, only removes from the list.</param>
        /// <returns>True if the mode was found and unregistered, false otherwise.</returns>
        public bool UnregisterCameraMode(string modeName, bool destroy = false)
        {
            CoreCameraMode targetMode = m_RegisteredCameraModes.Find(m => m.ModeName == modeName);
            if (targetMode == null)
            {
                Debug.LogWarning($"[CoreCameraController] Camera mode '{modeName}' not found for unregistration.", this);
                return false;
            }

            return UnregisterCameraMode(targetMode, destroy);
        }

        /// <summary>
        /// Gets all currently registered camera modes.
        /// </summary>
        /// <returns>A read-only list of registered camera modes.</returns>
        public IReadOnlyList<CoreCameraMode> GetRegisteredCameraModes()
        {
            return m_RegisteredCameraModes.AsReadOnly();
        }

        /// <summary>
        /// Checks if a camera mode with the specified name is registered.
        /// </summary>
        /// <param name="modeName">The name of the camera mode to check.</param>
        /// <returns>True if registered, false otherwise.</returns>
        public bool IsCameraModeRegistered(string modeName)
        {
            return m_RegisteredCameraModes.Exists(m => m.ModeName == modeName);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Initializes the Cinemachine setup by finding the brain, instantiating camera prefabs,
        /// and linking all camera modes to their targets.
        /// </summary>
        private void SetupCinemachine()
        {
            var mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogError("No main camera found in the scene. [CoreCameraController] requires a main camera.", this);
                return;
            }

            // Ensure a CinemachineBrain exists on the main camera.
            m_CinemachineBrain = mainCam.GetComponent<CinemachineBrain>();
            if (m_CinemachineBrain == null)
            {
                m_CinemachineBrain = mainCam.gameObject.AddComponent<CinemachineBrain>();
            }

            if (cameraModePrefabs.Count == 0)
            {
                Debug.LogError("[CoreCameraController] No camera mode prefabs assigned.", this);
                return;
            }

            // Instantiate each camera mode prefab and set up targets.
            foreach (var prefab in cameraModePrefabs)
            {
                if (prefab == null)
                {
                    Debug.LogWarning("Null camera mode prefab in [CoreCameraController]", this);
                    continue;
                }

                // Validate no duplicate mode names exist to prevent ambiguous SwitchCameraMode calls.
                if (m_RegisteredCameraModes.Exists(m => m.ModeName == prefab.ModeName))
                {
                    Debug.LogWarning($"[CoreCameraController] Duplicate camera mode name '{prefab.ModeName}' found in prefab list. Skipping duplicate.", this);
                    continue;
                }

                CoreCameraMode instance = Instantiate(prefab);
                instance.SetTargets(lookTarget);
                m_RegisteredCameraModes.Add(instance);
                m_InstantiatedCameraModes.Add(instance);
            }
        }

        #endregion
    }
}
