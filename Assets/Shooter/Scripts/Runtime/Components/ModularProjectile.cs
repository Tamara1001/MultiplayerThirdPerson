using System;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// A modular networked projectile system that uses composition through <see cref="IMovementBehavior"/>
    /// and <see cref="IProjectileEffect"/> components. Handles synchronized visual activation, movement,
    /// and effect processing across the network.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class ModularProjectile : NetworkTransform
    {
        #region Fields & Properties

        [Header("Visual Settings")]
        [Tooltip("The visual representation of the projectile. Activates with a delay for non-authority clients to sync with network state.")]
        public GameObject visualNode;

        [Header("Configuration")]
        [Tooltip("If true, ignores initial setup values when launching the projectile.")]
        public bool ignoreStartValues;

        private IMovementBehavior m_MovementBehavior;
        private IProjectileEffect m_ProjectileEffect;
        private int m_VisualTickOffset;

        #endregion

        #region Events

        /// <summary>
        /// Event invoked when the projectile's NetworkObject is despawned.
        /// Automatically cleared after invocation to prevent memory leaks.
        /// </summary>
        public Action OnNetworkObjectDespawned;

        #endregion

        #region Unity Methods

        protected override void Awake()
        {
            base.Awake();

            m_MovementBehavior = GetComponent<IMovementBehavior>();
            m_ProjectileEffect = GetComponent<IProjectileEffect>();

            m_MovementBehavior?.Initialize(this);
            if (m_ProjectileEffect != null)
            {
                m_ProjectileEffect.Initialize(this);
            }

            visualNode?.SetActive(false);
        }

        private void Update()
        {
            if (!IsSpawned) return;

            // Delay visual activation for non-authority clients to account for network latency
            if (!HasAuthority && visualNode != null && !visualNode.activeInHierarchy && m_VisualTickOffset < NetworkManager.LocalTime.Tick)
            {
                visualNode.SetActive(true);
            }

            m_ProjectileEffect?.ProcessUpdate();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (m_ProjectileEffect != null)
            {
                m_ProjectileEffect.OnEffectComplete += HandleEffectComplete;
                if (RigidbodyContactEventManager.Instance != null)
                {
                    RigidbodyContactEventManager.Instance.RegisterHandler(m_ProjectileEffect);
                }
            }

            if (HasAuthority)
            {
                visualNode?.SetActive(true);
                m_MovementBehavior?.ApplyInitialState();
                m_ProjectileEffect?.OnLaunch();
            }
            else
            {
                // Calculate the tick at which to activate visuals, accounting for network latency
                m_VisualTickOffset = NetworkManager.LocalTime.Tick + NetworkManager.NetworkTimeSystem.TickLatency;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (m_ProjectileEffect != null)
            {
                m_ProjectileEffect.OnEffectComplete -= HandleEffectComplete;
                m_ProjectileEffect.Cleanup();
                if (RigidbodyContactEventManager.Instance != null)
                {
                    RigidbodyContactEventManager.Instance.RegisterHandler(m_ProjectileEffect, false);
                }
            }

            visualNode?.SetActive(false);

            OnNetworkObjectDespawned?.Invoke();
            OnNetworkObjectDespawned = null;

            base.OnNetworkDespawn();
        }

        protected override void OnAuthorityPushTransformState(ref NetworkTransformState networkTransformState)
        {
            m_MovementBehavior?.CheckBoundary();
            base.OnAuthorityPushTransformState(ref networkTransformState);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes and launches the projectile with the specified context.
        /// Sets up movement behavior and projectile effects with the provided parameters.
        /// </summary>
        /// <param name="position">The starting position of the projectile.</param>
        /// <param name="direction">The direction the projectile should travel.</param>
        /// <param name="initialVelocity">The initial velocity vector.</param>
        /// <param name="weapon">The weapon that fired this projectile.</param>
        /// <param name="context">The shooting context containing additional information about the shot.</param>
        /// <param name="owner">The GameObject that owns this projectile (typically the shooter).</param>
        public void LaunchWithContext(Vector3 position, Vector3 direction, Vector3 initialVelocity,
            ModularWeapon weapon, ShootingContext context, GameObject owner = null)
        {
            m_MovementBehavior?.SetupMovement(position, direction, initialVelocity);
            m_ProjectileEffect?.Setup(owner, weapon, context);
        }

        #endregion

        #region Private Methods

        private void HandleEffectComplete(ModularProjectile projectile)
        {
            if (HasAuthority && IsSpawned)
            {
                // Use deferred despawn to allow effects to complete before removing the projectile
                if (m_ProjectileEffect != null && m_ProjectileEffect.IsDeferredDespawnEnabled)
                {
                    NetworkObject.DeferDespawn(m_ProjectileEffect.DeferredDespawnTicks);
                }
                else
                {
                    NetworkObject.Despawn();
                }
            }
        }

        #endregion
    }
}
