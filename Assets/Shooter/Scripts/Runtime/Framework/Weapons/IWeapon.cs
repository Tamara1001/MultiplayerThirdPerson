using UnityEngine;
using Unity.Netcode;

namespace Blocks.Gameplay.Shooter
{
    public interface IWeapon
    {
        /// <summary>
        /// Called on the owning player when they pull the trigger.
        /// </summary>
        /// <param name="ownerCharacter">The GameObject of the character/entity firing the weapon.</param>
        /// <param name="fireOrigin">A suggested origin for the fire action (e.g., camera position).</param>
        /// <param name="fireDirection">A suggested direction for the fire action (e.g., camera forward).</param>
        void Fire(GameObject ownerCharacter, Vector3 fireOrigin, Vector3 fireDirection);

        /// <summary>
        /// Called when the player releases the fire button or is interrupted.
        /// </summary>
        void StopFiring();

        /// <summary>
        /// Checks if the weapon is currently in a state that allows firing.
        /// </summary>
        bool CanFire();

        /// <summary>
        /// Attempts to initiate a reload sequence for the weapon.
        /// </summary>
        void TryReload();

        /// <summary>
        /// Gets the current state of the weapon.
        /// </summary>
        WeaponState GetCurrentState();

        /// <summary>
        /// Gets the weapon's data configuration.
        /// </summary>
        /// <returns></returns>
        WeaponData GetWeaponData();

        /// <summary>
        /// Plays a tracer visual effect from a start to an end point.
        /// </summary>
        void PlayTracerEffect(Vector3 start, Vector3 end);

        /// <summary>
        /// Plays an impact visual and audio effect at the hit location.
        /// </summary>
        void PlayImpactEffect(Vector3 position, Vector3 normal, float damage, NetworkObjectReference parentRef);
    }
}
