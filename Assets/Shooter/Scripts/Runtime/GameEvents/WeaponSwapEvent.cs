using UnityEngine;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// A ScriptableObject-based event that carries <see cref="WeaponSwapPayload"/> data.
    /// This event is raised when a weapon is swapped, providing information about both the old and new weapons.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponSwapEvent", menuName = "Game Events/Weapon Swap Event")]
    public class WeaponSwapEvent : GameEvent<WeaponSwapPayload> { }

    /// <summary>
    /// Payload structure containing information about a weapon swap operation.
    /// </summary>
    public struct WeaponSwapPayload
    {
        /// <summary>
        /// The weapon being swapped out.
        /// </summary>
        public IWeapon OldWeapon;

        /// <summary>
        /// The weapon being swapped in.
        /// </summary>
        public IWeapon NewWeapon;
    }
}
