using UnityEngine;
using Unity.Cinemachine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Defines settings for a camera mode. Attach this to a CinemachineCamera GameObject
    /// to configure its behavior when used with CoreCameraController.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public class CoreCameraMode : MonoBehaviour
    {
        #region Fields

        [Header("Mode Identification")]
        [Tooltip("Unique name to identify this camera mode for switching.")]
        [SerializeField] private string modeName = "Default";

        [Header("Priority Settings")]
        [Tooltip("The priority of this camera when it is active.")]
        [SerializeField] private int activePriority = 10;
        [Tooltip("The priority of this camera when it is inactive.")]
        [SerializeField] private int inactivePriority;

        [Header("Player Coupling")]
        [Tooltip("The player rotation mode to use when this camera is active.")]
        [SerializeField] private CoreMovement.CouplingMode playerRotationMode = CoreMovement.CouplingMode.Decoupled;

        [Header("Look Settings")]
        [Tooltip("Whether this camera overrides the default look settings.")]
        [SerializeField] private bool overrideLookSettings;
        [Tooltip("Sensitivity for look input when this camera is active.")]
        [SerializeField] private float lookSensitivity = 1.0f;
        [Tooltip("The maximum angle in degrees the camera can look up or down.")]
        [SerializeField] private float verticalLookLimit = 70.0f;

        // Cached reference to the CinemachineCamera component.
        private CinemachineCamera m_CinemachineCamera;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the unique name of this camera mode.
        /// </summary>
        public string ModeName => modeName;

        /// <summary>
        /// Gets the priority when this camera is active.
        /// </summary>
        public int ActivePriority => activePriority;

        /// <summary>
        /// Gets the priority when this camera is inactive.
        /// </summary>
        public int InactivePriority => inactivePriority;

        /// <summary>
        /// Gets the player rotation coupling mode for this camera.
        /// </summary>
        public CoreMovement.CouplingMode PlayerRotationMode => playerRotationMode;

        /// <summary>
        /// Gets whether this camera overrides default look settings.
        /// </summary>
        public bool OverrideLookSettings => overrideLookSettings;

        /// <summary>
        /// Gets the look sensitivity for this camera mode.
        /// </summary>
        public float LookSensitivity => lookSensitivity;

        /// <summary>
        /// Gets the vertical look limit for this camera mode.
        /// </summary>
        public float VerticalLookLimit => verticalLookLimit;

        /// <summary>
        /// Gets the CinemachineCamera component attached to this GameObject.
        /// </summary>
        public CinemachineCamera CinemachineCamera
        {
            get
            {
                if (m_CinemachineCamera == null)
                {
                    m_CinemachineCamera = GetComponent<CinemachineCamera>();
                }
                return m_CinemachineCamera;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the camera priority based on whether it should be active or inactive.
        /// </summary>
        /// <param name="isActive">True to set active priority, false for inactive.</param>
        public void SetActive(bool isActive)
        {
            if (CinemachineCamera != null)
            {
                CinemachineCamera.Priority = isActive ? activePriority : inactivePriority;
            }
        }

        /// <summary>
        /// Sets the Follow and LookAt targets for this camera.
        /// </summary>
        /// <param name="target">The transform to follow and look at.</param>
        public void SetTargets(Transform target)
        {
            if (CinemachineCamera != null && target != null)
            {
                CinemachineCamera.Follow = target;
                CinemachineCamera.LookAt = target;
            }
        }

        #endregion
    }
}
