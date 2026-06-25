using UnityEngine;
using Unity.Netcode;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Implements instant-hit shooting behavior using raycasting.
    /// Performs a raycast from the muzzle position in the firing direction, applying spread and damage to hit targets.
    /// </summary>
    public class HitscanShooting : NetworkBehaviour, IShootingBehavior
    {
        #region Fields & Properties

        [Header("Hitscan Settings")]
        [Tooltip("Maximum range of the hitscan raycast.")]
        [SerializeField] private float maxRange = 100f;
        [Tooltip("Force applied to hit targets with rigidbodies.")]
        [SerializeField] private float hitForce = 100f;

        #endregion

        #region Public Methods

        /// <summary>
        /// Performs a hitscan shot by raycasting from the origin in the specified direction.
        /// Applies spread, detects hits, triggers visual effects, and applies damage to <see cref="IHittable"/> targets.
        /// </summary>
        /// <param name="context">The shooting context containing all necessary data for the shot.</param>
        public void Shoot(ShootingContext context)
        {
            Vector3 finalDirection = ApplySpread(context.direction, context.currentSpread);
            Vector3 origin = context.muzzle != null ? context.muzzle.position : context.origin;

            bool hitSomething = Physics.Raycast(origin, finalDirection, out var hit, maxRange, context.hitMask);

            // Prevent the shooter from hitting themselves
            if (hitSomething && hit.collider.transform.root == context.owner.transform.root)
            {
                hitSomething = false;
            }

            Vector3 endPoint = hitSomething ? hit.point : origin + finalDirection * maxRange;
            context.Weapon.PlayTracerEffect(origin, endPoint);

            // Capture the network object reference for impact effects
            NetworkObjectReference parentRef = default;
            if (hitSomething)
            {
                var parentNetObj = hit.collider.GetComponentInParent<NetworkObject>();
                if (parentNetObj != null) parentRef = parentNetObj;
            }

            if (hitSomething)
            {
                context.Weapon.PlayImpactEffect(hit.point, hit.normal, context.damage, parentRef);
                var hittable = hit.collider.GetComponentInParent<IHittable>();
                if (hittable != null)
                {
                    var hitInfo = new HitInfo
                    {
                        amount = context.damage,
                        hitPoint = hit.point,
                        hitNormal = hit.normal,
                        attackerId = context.ownerClientId,
                        impactForce = finalDirection * hitForce
                    };
                    hittable.OnHit(hitInfo);
                    context.OnTargetHit?.Invoke(hit.collider.gameObject, hitInfo);
                }
            }

            context.OnAmmoConsumed?.Invoke(1);
            context.OnHitPointCalculated?.Invoke(endPoint, hitSomething ? hit.normal : Vector3.up);
        }

        /// <summary>
        /// Determines whether shooting is currently allowed.
        /// </summary>
        /// <returns>Always returns true for hitscan shooting.</returns>
        public bool CanShoot() => true;

        /// <summary>
        /// Updates the shooting behavior with a new direction. Not used for hitscan shooting.
        /// </summary>
        /// <param name="updatedDirection">The updated shooting direction.</param>
        /// <param name="deltaTime">Time since last update.</param>
        public void UpdateShooting(Vector3 updatedDirection, float deltaTime) { }

        /// <summary>
        /// Stops the shooting behavior. Not used for hitscan shooting.
        /// </summary>
        public void StopShooting() { }

        #endregion

        #region Private Methods

        /// <summary>
        /// Applies random spread to the firing direction based on the spread angle.
        /// Uses a random point within a unit circle to create cone-shaped spread.
        /// </summary>
        /// <param name="direction">The base firing direction.</param>
        /// <param name="spreadAngle">The maximum spread angle in degrees.</param>
        /// <returns>The direction with spread applied.</returns>
        private Vector3 ApplySpread(Vector3 direction, float spreadAngle)
        {
            if (spreadAngle <= 0)
            {
                return direction;
            }

            Vector2 randomCircle = Random.insideUnitCircle;
            Quaternion spreadRotation = Quaternion.Euler(randomCircle.y * spreadAngle, randomCircle.x * spreadAngle, 0);
            return spreadRotation * direction;
        }

        #endregion
    }
}
