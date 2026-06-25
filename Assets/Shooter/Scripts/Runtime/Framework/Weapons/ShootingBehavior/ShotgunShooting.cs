using UnityEngine;
using Unity.Netcode;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Implements shotgun-style shooting behavior that fires multiple pellets in a spread pattern.
    /// Each pellet is raycast individually with distance-based damage falloff for realistic shotgun mechanics.
    /// </summary>
    public class ShotgunShooting : NetworkBehaviour, IShootingBehavior
    {
        #region Fields & Properties

        [Header("Shotgun Settings")]
        [Tooltip("Number of pellets fired per shot.")]
        [SerializeField] private int pelletCount = 8;
        [Tooltip("Base spread angle for pellet distribution in degrees.")]
        [SerializeField] private float spreadAngle = 15f;
        [Tooltip("Maximum range for each pellet raycast.")]
        [SerializeField] private float maxRange = 30f;
        [Tooltip("Base damage dealt by each individual pellet before falloff.")]
        [SerializeField] private float damagePerPellet = 5f;
        [Tooltip("Force applied to hit targets per pellet.")]
        [SerializeField] private float hitForcePerPellet = 100f;
        [Tooltip("Damage falloff curve over distance (0 = close range, 1 = max range).")]
        [SerializeField] private AnimationCurve damageFalloff = AnimationCurve.Linear(0, 1, 1, 0.3f);

        #endregion

        #region Public Methods

        /// <summary>
        /// Fires multiple pellets in a spread pattern, each with individual raycasts and damage calculations.
        /// Applies distance-based damage falloff to simulate realistic shotgun behavior.
        /// </summary>
        /// <param name="context">The <see cref="ShootingContext"/> containing all data needed to perform the shot.</param>
        public void Shoot(ShootingContext context)
        {
            Vector3 origin = context.muzzle != null ? context.muzzle.position : context.origin;
            Vector3 baseDirection = ApplySpread(context.direction, context.currentSpread);
            float dynamicSpreadAngle = spreadAngle + context.currentSpread;

            for (int i = 0; i < pelletCount; i++)
            {
                Vector3 spreadDirection = ApplyPelletSpread(baseDirection, dynamicSpreadAngle);

                bool hitSomething = Physics.Raycast(origin, spreadDirection, out RaycastHit hit, maxRange, context.hitMask) &&
                                    hit.collider.transform.root != context.owner.transform.root;

                Vector3 endPoint = hitSomething ? hit.point : origin + spreadDirection * maxRange;
                context.Weapon.PlayTracerEffect(origin, endPoint);

                // Capture the network object reference for impact effects
                NetworkObjectReference parentRef = default;
                if (hitSomething)
                {
                    var parentNetObj = hit.collider.GetComponentInParent<NetworkObject>();
                    if (parentNetObj != null)
                    {
                        parentRef = parentNetObj;
                    }
                }

                if (hitSomething)
                {
                    // Calculate damage with distance-based falloff
                    float distance = Vector3.Distance(origin, hit.point);
                    float falloffMultiplier = damageFalloff.Evaluate(distance / maxRange);
                    float damage = damagePerPellet * falloffMultiplier;
                    context.Weapon.PlayImpactEffect(hit.point, hit.normal, damage, parentRef);

                    var hittable = hit.collider.GetComponentInParent<IHittable>();
                    if (hittable != null)
                    {
                        var hitInfo = new HitInfo
                        {
                            amount = damage,
                            hitPoint = hit.point,
                            hitNormal = hit.normal,
                            attackerId = context.ownerClientId,
                            impactForce = spreadDirection * hitForcePerPellet
                        };
                        hittable.OnHit(hitInfo);
                        context.OnTargetHit?.Invoke(hit.collider.gameObject, hitInfo);
                    }
                }
            }

            context.OnAmmoConsumed?.Invoke(1);
        }

        /// <summary>
        /// Determines whether shooting is currently allowed.
        /// </summary>
        /// <returns>Always returns true for shotgun shooting.</returns>
        public bool CanShoot() => true;

        /// <summary>
        /// Updates ongoing shooting behavior with a new direction. Not used for shotgun shooting.
        /// </summary>
        /// <param name="updatedDirection">The updated shooting direction.</param>
        /// <param name="deltaTime">Time elapsed since the last update.</param>
        public void UpdateShooting(Vector3 updatedDirection, float deltaTime) { }

        /// <summary>
        /// Stops the shooting behavior. Not used for shotgun shooting.
        /// </summary>
        public void StopShooting() { }

        #endregion

        #region Private Methods

        /// <summary>
        /// Applies player-induced spread to the base firing direction.
        /// Used to apply additional spread from player movement or recoil.
        /// </summary>
        /// <param name="direction">The base firing direction.</param>
        /// <param name="playerSpread">The player-induced spread angle in degrees.</param>
        /// <returns>The direction with player spread applied.</returns>
        private Vector3 ApplySpread(Vector3 direction, float playerSpread)
        {
            if (playerSpread <= 0)
            {
                return direction;
            }

            Vector2 randomCircle = Random.insideUnitCircle;
            Quaternion spreadRotation = Quaternion.Euler(randomCircle.y * playerSpread, randomCircle.x * playerSpread, 0);
            return spreadRotation * direction;
        }

        /// <summary>
        /// Applies shotgun pellet spread to create a cone-shaped distribution pattern.
        /// Uses cylindrical coordinates to distribute pellets evenly within the spread angle.
        /// </summary>
        /// <param name="direction">The base firing direction.</param>
        /// <param name="angle">The maximum spread angle in degrees.</param>
        /// <returns>The direction with pellet spread applied.</returns>
        private Vector3 ApplyPelletSpread(Vector3 direction, float angle)
        {
            float halfAngle = angle * 0.5f;
            float randomAngle = Random.Range(0f, 360f);
            float randomRadius = Random.Range(0f, halfAngle);

            Quaternion spreadRotation = Quaternion.AngleAxis(randomAngle, direction);
            Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;

            // Handle edge case where direction is parallel to Vector3.up
            if (right == Vector3.zero) right = Vector3.right;

            Quaternion tiltRotation = Quaternion.AngleAxis(randomRadius, right);
            return spreadRotation * tiltRotation * direction;
        }

        #endregion
    }
}
