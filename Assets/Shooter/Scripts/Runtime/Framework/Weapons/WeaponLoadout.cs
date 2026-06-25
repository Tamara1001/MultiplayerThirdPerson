using UnityEngine;
using System.Collections.Generic;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// A ScriptableObject that defines a weapon loadout configuration.
    /// Contains a list of weapon prefabs and specifies which weapon should be equipped by default.
    /// Each weapon prefab should have a ModularWeapon component with its WeaponData assigned.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponLoadout", menuName = "Shooter/Weapon Loadout")]
    public class WeaponLoadout : ScriptableObject
    {
        #region Fields & Properties

        [Header("Loadout Configuration")]
        [Tooltip("List of weapon prefabs in this loadout. Each prefab should contain a ModularWeapon component with WeaponData assigned.")]
        public List<GameObject> weaponPrefabs = new List<GameObject>();

        [Tooltip("Index of the starting weapon. Must be a valid index within the weaponPrefabs list.")]
        public int defaultWeaponIndex = 0;

        #endregion
    }
}
