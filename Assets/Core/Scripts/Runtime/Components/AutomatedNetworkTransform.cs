using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// An extension of <see cref="NetworkTransform"/> that provides server-authoritative movement for dynamic platforms.
    /// It supports various movement patterns such as following waypoints, lerping to a target, and continuous rotation.
    /// These movement types can be combined and are synchronized over the network.
    /// </summary>
    public class AutomatedNetworkTransform : NetworkTransform
    {
        #region Enums

        /// <summary>
        /// Defines the available movement patterns for the platform. Multiple types can be combined.
        /// </summary>
        [System.Flags]
        public enum MovementType
        {
            None = 0,
            /// <summary>
            /// The platform moves between a series of defined waypoints.
            /// </summary>
            Waypoint = 1 << 0,
            /// <summary>
            /// The platform moves smoothly towards a specified NetworkObject target.
            /// </summary>
            LerpToTarget = 1 << 1,
            /// <summary>
            /// The platform rotates continuously over time.
            /// </summary>
            RotateOverTime = 1 << 2
        }

        /// <summary>
        /// Defines how the platform should traverse its waypoint path.
        /// </summary>
        private enum LoopType
        {
            /// <summary>
            /// Restarts from the first waypoint after reaching the last.
            /// </summary>
            Loop,
            /// <summary>
            /// Reverses direction upon reaching the start or end of the path.
            /// </summary>
            PingPong,
            /// <summary>
            /// Stops moving after reaching the last waypoint.
            /// </summary>
            Once
        }

        #endregion

        #region Fields & Properties

        [Header("General Settings")]
        [Tooltip("If true, the platform will start moving automatically when it spawns on the network.")]
        [SerializeField] private bool autoStart = true;
        [Tooltip("The movement pattern(s) to use. Can be combined (e.g., Waypoint + RotateOverTime).")]
        [SerializeField] private MovementType movementType = MovementType.Waypoint;

        [Header("Waypoint Settings")]
        [Tooltip("A list of transforms that define the platform's movement path.")]
        [SerializeField] private List<Transform> waypoints;
        [Tooltip("The speed at which the platform moves between waypoints.")]
        [SerializeField] private float moveSpeed = 2.0f;
        [Tooltip("The duration in seconds the platform should wait at each waypoint before proceeding.")]
        [SerializeField] private float waypointWaitTime;
        [Tooltip("Defines how the waypoint path should be traversed (Loop, PingPong, or Once).")]
        [SerializeField] private LoopType loopType = LoopType.Loop;

        [Header("Lerp To Target Settings")]
        [Tooltip("The NetworkObject to move towards. Can be set in the editor or at runtime.")]
        [SerializeField] private NetworkObject lerpTarget;
        [Tooltip("How quickly the platform moves towards its target. Higher values are faster.")]
        [SerializeField] private float lerpSpeed = 5.0f;
        [Tooltip("The distance from the target at which the platform will stop moving.")]
        [SerializeField] private float stopDistance = 0.1f;
        [Tooltip("If true, the LerpToTarget movement will be disabled once the platform reaches its destination.")]
        [SerializeField] private bool disableOnArrival;

        [Header("Rotation Settings")]
        [Tooltip("The speed of rotation in degrees per second for each axis (X, Y, Z).")]
        [SerializeField] private Vector3 rotationSpeed = new Vector3(0, 90, 0);
        [Tooltip("The coordinate space in which to apply the rotation (Self or World).")]
        [SerializeField] private Space rotationSpace = Space.Self;

        // Networked state variables.
        private readonly NetworkVariable<MovementType> m_CurrentMovementType = new NetworkVariable<MovementType>();
        private readonly NetworkVariable<NetworkObjectReference> m_TargetObject = new NetworkVariable<NetworkObjectReference>();

        // Internal state for waypoint movement.
        private int m_CurrentWaypointIndex;
        private int m_WaypointDirection = 1;
        private bool m_IsWaitingAtWaypoint;
        private float m_WaitTimer;

        #endregion

        #region Unity & Network Lifecycle

        /// <summary>
        /// Initializes the platform's movement state when it spawns on the network.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            // Only the authority (server) should set the initial movement state.
            if (CanCommitToTransform)
            {
                if (autoStart)
                {
                    if ((movementType & MovementType.Waypoint) != 0 && (waypoints == null || waypoints.Count == 0))
                    {
                        Debug.LogWarning("[AutomatedNetworkTransform] Waypoint movement is enabled but no waypoints are assigned.", this);
                    }
                    if ((movementType & MovementType.LerpToTarget) != 0 && lerpTarget == null)
                    {
                        Debug.LogWarning("[AutomatedNetworkTransform] LerpToTarget movement is enabled but no target is assigned.", this);
                    }
                }

                m_CurrentMovementType.Value = autoStart ? movementType : MovementType.None;
                if (lerpTarget != null)
                {
                    m_TargetObject.Value = lerpTarget;
                }
            }
        }

        /// <summary>
        /// Executes the movement logic on the server/authority each frame.
        /// </summary>
        private void Update()
        {
            if (!CanCommitToTransform)
            {
                return;
            }
            MovementType currentType = m_CurrentMovementType.Value;

            // Check flags and execute corresponding movement logic.
            if ((currentType & MovementType.Waypoint) != 0)
            {
                MoveAlongWaypoints();
            }

            if ((currentType & MovementType.LerpToTarget) != 0)
            {
                MoveTowardsTarget();
            }

            if ((currentType & MovementType.RotateOverTime) != 0)
            {
                Rotate();
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Starts the platform's movement using its configured default movement type.
        /// </summary>
        public void StartMovement()
        {
            StartMovementRpc();
        }

        /// <summary>
        /// Sets the target for the LerpToTarget movement type. This does not start the movement.
        /// Use StartMovement() or SetMovementType() to enable the lerp behavior.
        /// </summary>
        /// <param name="target">The NetworkObject to move towards. Can be null to clear the target.</param>
        public void SetLerpTarget(NetworkObject target)
        {
            SetLerpTargetRpc(target);
        }

        /// <summary>
        /// Sets the active movement type for the platform at runtime.
        /// </summary>
        /// <param name="newType">The new movement type to apply.</param>
        public void SetMovementType(MovementType newType)
        {
            SetMovementTypeRpc(newType);
        }

        #endregion

        #region RPCs

        /// <summary>
        /// RPC sent to the authority to start the platform's movement.
        /// </summary>
        [Rpc(SendTo.Authority)]
        private void StartMovementRpc()
        {
            m_CurrentMovementType.Value = movementType;
        }

        /// <summary>
        /// RPC sent to the authority to set the target for LerpToTarget movement.
        /// </summary>
        [Rpc(SendTo.Authority)]
        private void SetLerpTargetRpc(NetworkObjectReference targetRef)
        {
            if (!targetRef.TryGet(out _) && (m_CurrentMovementType.Value & MovementType.LerpToTarget) != 0)
            {
                Debug.LogWarning("[AutomatedNetworkTransform] Setting null target while LerpToTarget movement is active.", this);
            }
            m_TargetObject.Value = targetRef;
        }

        /// <summary>
        /// RPC sent to the authority to change the current movement type.
        /// </summary>
        [Rpc(SendTo.Authority)]
        private void SetMovementTypeRpc(MovementType newType)
        {
            if ((newType & MovementType.Waypoint) != 0 && (waypoints == null || waypoints.Count == 0))
            {
                Debug.LogWarning("[AutomatedNetworkTransform] Enabling Waypoint movement but no waypoints are assigned.", this);
            }
            if ((newType & MovementType.LerpToTarget) != 0 && !m_TargetObject.Value.TryGet(out _))
            {
                Debug.LogWarning("[AutomatedNetworkTransform] Enabling LerpToTarget movement but no target is set.", this);
            }
            m_CurrentMovementType.Value = newType;
        }

        #endregion

        #region Movement Logic (Authority-Only)

        /// <summary>
        /// Applies continuous rotation to the platform based on `rotationSpeed`.
        /// </summary>
        private void Rotate()
        {
            transform.Rotate(rotationSpeed * Time.deltaTime, rotationSpace);
        }

        /// <summary>
        /// Moves the platform towards its target NetworkObject.
        /// </summary>
        private void MoveTowardsTarget()
        {
            if (m_TargetObject.Value.TryGet(out NetworkObject targetObject))
            {
                Vector3 targetPosition = targetObject.transform.position;
                if (Vector3.Distance(transform.position, targetPosition) > stopDistance)
                {
                    transform.position = Vector3.Lerp(transform.position, targetPosition, lerpSpeed * Time.deltaTime);
                }
                else if (disableOnArrival)
                {
                    // Remove the LerpToTarget flag to stop this movement type.
                    m_CurrentMovementType.Value &= ~MovementType.LerpToTarget;
                }
            }
            else
            {
                Debug.LogWarning("[AutomatedNetworkTransform] LerpToTarget movement is active but target NetworkObject could not be resolved.", this);
            }
        }

        /// <summary>
        /// Manages the platform's movement along its defined waypoint path.
        /// </summary>
        private void MoveAlongWaypoints()
        {
            if (waypoints == null || waypoints.Count == 0)
            {
                Debug.LogWarning("[AutomatedNetworkTransform] Cannot move along waypoints: waypoints list is null or empty.", this);
                return;
            }

            // Handle waiting at a waypoint.
            if (m_IsWaitingAtWaypoint)
            {
                m_WaitTimer -= Time.deltaTime;
                if (m_WaitTimer <= 0)
                {
                    m_IsWaitingAtWaypoint = false;
                    UpdateWaypointIndex();
                }
                return;
            }

            if (m_CurrentWaypointIndex < 0 || m_CurrentWaypointIndex >= waypoints.Count)
            {
                Debug.LogError($"[AutomatedNetworkTransform] Waypoint index {m_CurrentWaypointIndex} is out of bounds (0-{waypoints.Count - 1}). Resetting to 0.", this);
                m_CurrentWaypointIndex = 0;
                return;
            }

            if (waypoints[m_CurrentWaypointIndex] == null)
            {
                Debug.LogError($"[AutomatedNetworkTransform] Waypoint at index {m_CurrentWaypointIndex} is null. Skipping to next waypoint.", this);
                UpdateWaypointIndex();
                return;
            }

            // Move towards the current waypoint.
            Vector3 targetPosition = waypoints[m_CurrentWaypointIndex].position;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

            // Check if the waypoint has been reached.
            if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
            {
                if (waypointWaitTime > 0f)
                {
                    m_IsWaitingAtWaypoint = true;
                    m_WaitTimer = waypointWaitTime;
                }
                else
                {
                    UpdateWaypointIndex();
                }
            }
        }

        /// <summary>
        /// Calculates the next waypoint index based on the selected loop type.
        /// </summary>
        private void UpdateWaypointIndex()
        {
            switch (loopType)
            {
                case LoopType.Loop:
                    m_CurrentWaypointIndex = (m_CurrentWaypointIndex + 1) % waypoints.Count;
                    break;
                case LoopType.PingPong:
                    if ((m_CurrentWaypointIndex <= 0 && m_WaypointDirection == -1) ||
                        (m_CurrentWaypointIndex >= waypoints.Count - 1 && m_WaypointDirection == 1))
                    {
                        // Reverse direction.
                        m_WaypointDirection *= -1;
                    }
                    m_CurrentWaypointIndex += m_WaypointDirection;
                    break;
                case LoopType.Once:
                    if (m_CurrentWaypointIndex < waypoints.Count - 1)
                    {
                        m_CurrentWaypointIndex++;
                    }
                    else
                    {
                        // Stop waypoint movement after reaching the end.
                        m_CurrentMovementType.Value &= ~MovementType.Waypoint;
                    }
                    break;
            }
        }

        #endregion
    }
}
