using System;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.VFX;
using Unity.Cinemachine;
using Blocks.Gameplay.Core;
using Unity.Netcode.Components;
using System.Collections.Generic;

namespace Blocks.Gameplay.Shooter
{
    public class ExplosiveEffect : NetworkBehaviour, IProjectileEffect
    {
        #region Fields & Properties

        [Header("Collision Settings")]
        [Tooltip("List of colliders on the projectile that should be enabled/disabled during the explosion lifecycle.")]
        [SerializeField] private List<Collider> colliders;

        [Header("Sound Effects")]
        [Tooltip("Sound definition to play when the explosion occurs.")]
        [SerializeField] private SoundDef explosionSoundEffect;

        [Header("Explosion Settings")]
        [Tooltip("Radius of the explosion damage sphere.")]
        [SerializeField] private float explosionRadius = 5.0f;
        [Tooltip("Maximum damage dealt at the center of the explosion.")]
        [SerializeField] private float explosionDamage = 50.0f;
        [Tooltip("Visual effect prefab to instantiate at the explosion position.")]
        [SerializeField] private GameObject explosionEffectPrefab;
        [Tooltip("Duration before the explosion effect is destroyed.")]
        [SerializeField] private float explosionEffectTime = 0.5f;
        [Tooltip("Layer mask to determine which objects can be damaged by the explosion.")]
        [SerializeField] private LayerMask explosionLayerMask = -1;

        [Header("Trigger Settings")]
        [Tooltip("If true, the projectile will explode immediately upon collision.")]
        [SerializeField] private bool explodeOnImpact = true;
        [Tooltip("Time in seconds before the projectile explodes automatically.")]
        [SerializeField] private float fuseTime = 3.0f;
        [Tooltip("If true, the projectile will explode when the fuse time expires.")]
        [SerializeField] private bool explodeOnTimeout = true;

        [Header("Lifetime Settings")]
        [Tooltip("If true, the projectile will use deferred despawn to allow network synchronization.")]
        [SerializeField] private bool deferredDespawn = true;
        [Tooltip("Number of network ticks to wait before despawning the projectile.")]
        [SerializeField] private int deferredDespawnTicks = 4;

        private GameObject m_Owner;
        private ModularProjectile m_Projectile;
        private ShootingContext m_ShootingContext;
        private readonly NetworkVariable<bool> m_HasExploded = new NetworkVariable<bool>(false);
        private float m_EndOfLife;
        private readonly Collider[] m_HitColliderBuffer = new Collider[64];

        public bool IsDeferredDespawnEnabled => deferredDespawn;
        public int DeferredDespawnTicks => deferredDespawnTicks;

        #endregion

        #region Events

        public event Action<ModularProjectile> OnEffectComplete;

        #endregion

        #region Public Methods

        public void Initialize(ModularProjectile projectile)
        {
            m_Projectile = projectile;
        }

        public void Setup(GameObject owner, IWeapon sourceWeapon, ShootingContext context)
        {
            m_Owner = owner;
            m_ShootingContext = context;
            IgnoreCollision(owner, gameObject, true);
        }

        public void OnLaunch()
        {
            m_EndOfLife = Time.realtimeSinceStartup + fuseTime;
            m_HasExploded.Value = false;
            EnableColliders(true);
        }

        public void ProcessUpdate()
        {
            // Only the authority should check for timeout explosions
            if (!HasAuthority || m_HasExploded.Value) return;

            if (explodeOnTimeout && Time.realtimeSinceStartup > m_EndOfLife)
            {
                Explode(transform.position);
            }
        }

        public void Cleanup()
        {
            if (m_Owner != null)
            {
                IgnoreCollision(m_Owner, gameObject, false);
            }
        }

        /// <summary>
        /// Provides contact event configuration for the projectile.
        /// </summary>
        /// <returns>Contact event handler information specifying event priorities and non-rigidbody collision handling.</returns>
        public ContactEventHandlerInfo GetContactEventHandlerInfo()
        {
            return new ContactEventHandlerInfo { ProvideNonRigidBodyContactEvents = true, HasContactEventPriority = HasAuthority };
        }

        /// <summary>
        /// Gets the rigidbody component of the projectile.
        /// </summary>
        /// <returns>The rigidbody attached to the projectile.</returns>
        public Rigidbody GetRigidbody()
        {
            return m_Projectile.GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Handles collision events for the projectile. Only the authority processes collisions to prevent duplicate explosions.
        /// </summary>
        /// <param name="eventId">Unique identifier for the contact event.</param>
        /// <param name="averageNormal">Average normal vector of the contact.</param>
        /// <param name="collidingBody">The rigidbody that was collided with, if any.</param>
        /// <param name="contactPoint">The point of contact in world space.</param>
        /// <param name="hasCollisionStay">Whether this is a continuous collision.</param>
        /// <param name="averagedCollisionStayNormal">Average normal for continuous collisions.</param>
        public void ContactEvent(ulong eventId, Vector3 averageNormal, Rigidbody collidingBody, Vector3 contactPoint, bool hasCollisionStay = false, Vector3 averagedCollisionStayNormal = default)
        {
            // Only process collisions on the authority to prevent duplicate explosions
            if (!IsSpawned || m_HasExploded.Value || !HasAuthority) return;
            if (collidingBody != null && collidingBody.gameObject == m_Owner) return;

            if (explodeOnImpact)
            {
                Explode(contactPoint);
            }
        }

        #endregion

        #region Private Methods

        private void Explode(Vector3 explosionPosition)
        {
            if (!HasAuthority || m_HasExploded.Value) return;

            m_HasExploded.Value = true;
            ExplodeRpc(explosionPosition);
        }

        /// <summary>
        /// Executes the explosion effects and damage calculations on all clients.
        /// </summary>
        /// <param name="explosionPosition">The world position where the explosion occurs.</param>
        [Rpc(SendTo.Everyone)]
        private void ExplodeRpc(Vector3 explosionPosition)
        {
            ShowExplosionVFX(explosionPosition);

            CoreDirector.RequestCameraShake()
                .WithImpulseDefinition(CinemachineImpulseDefinition.ImpulseShapes.Explosion,
                    CinemachineImpulseDefinition.ImpulseTypes.Propagating,
                    0.2f)
                .WithVelocity(0.02f)
                .AtPosition(explosionPosition)
                .Execute();

            PlayExplosionSfx(explosionPosition);

            // Disable colliders to prevent further collision events after explosion
            EnableColliders(false);

            // Only the authority should calculate and apply damage
            if (!HasAuthority) return;

            int hitCount = Physics.OverlapSphereNonAlloc(explosionPosition, explosionRadius, m_HitColliderBuffer, explosionLayerMask);
            var processedTargets = new HashSet<IHittable>();

            for (int i = 0; i < hitCount; i++)
            {
                var hitCollider = m_HitColliderBuffer[i];
                if (hitCollider.gameObject == m_Owner) continue;

                var hittable = hitCollider.GetComponentInParent<IHittable>();
                if (hittable != null && !processedTargets.Contains(hittable))
                {
                    processedTargets.Add(hittable);

                    // Calculate damage based on distance from explosion center
                    float distance = Vector3.Distance(explosionPosition, hitCollider.transform.position);
                    float damageFalloff = 1f - Mathf.Clamp01(distance / explosionRadius);
                    float damage = explosionDamage * damageFalloff;

                    var hitInfo = new HitInfo
                    {
                        amount = damage,
                        hitPoint = hitCollider.ClosestPoint(explosionPosition),
                        hitNormal = (hitCollider.transform.position - explosionPosition).normalized,
                        attackerId = OwnerClientId,
                        impactForce = (damage * damageFalloff * Vector3.one)
                    };

                    hittable.OnHit(hitInfo);
                    m_ShootingContext.OnTargetHit?.Invoke(hitCollider.gameObject, hitInfo);
                }
            }
            OnEffectComplete?.Invoke(m_Projectile);
        }

        private void PlayExplosionSfx(Vector3 position)
        {
            CoreDirector.RequestAudio(explosionSoundEffect)
                .WithPosition(position)
                .AsReserved(SoundEmitter.ReservedInfo.ReservedEmitter)
                .Play();
        }

        private void ShowExplosionVFX(Vector3 position)
        {
            if (explosionEffectPrefab == null) return;

            GameObject instance = Instantiate(explosionEffectPrefab, position, Quaternion.identity);

            if (instance.TryGetComponent<VisualEffect>(out var vfx))
            {
                // Pass the explosion radius to the VFX if it has a DamageRadius parameter
                if (vfx.HasFloat("DamageRadius"))
                {
                    vfx.SetFloat("DamageRadius", explosionRadius);
                }
                vfx.Play();
                Destroy(instance, explosionEffectTime);
            }
            else
            {
                Destroy(instance, explosionEffectTime);
            }
        }

        private void EnableColliders(bool enable)
        {
            foreach (var col in colliders)
            {
                if (col != null) col.enabled = enable;
            }
        }

        /// <summary>
        /// Configures collision ignoring between two game objects by traversing their hierarchy roots.
        /// This ensures that all colliders in both object hierarchies ignore each other.
        /// </summary>
        /// <param name="objectA">First game object.</param>
        /// <param name="objectB">Second game object.</param>
        /// <param name="shouldIgnore">If true, collisions will be ignored; if false, collisions will be re-enabled.</param>
        private void IgnoreCollision(GameObject objectA, GameObject objectB, bool shouldIgnore)
        {
            if (objectA == null || objectB == null) return;

            // Find the root transforms to ensure we ignore collisions across entire hierarchies
            var rootA = objectA.transform.root.gameObject;
            var rootB = objectB.transform.root.gameObject;

            var collidersA = rootA.GetComponentsInChildren<Collider>();
            var collidersB = rootB.GetComponentsInChildren<Collider>();

            foreach (var colliderA in collidersA)
            {
                foreach (var colliderB in collidersB)
                {
                    Physics.IgnoreCollision(colliderA, colliderB, shouldIgnore);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, 0.5f, 0, 0.5f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }

        #endregion
    }
}
