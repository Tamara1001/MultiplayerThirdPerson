using UnityEngine;
using Unity.Netcode;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Network-serializable data structure for bullet impact visual effects.
    /// Contains position, normal, and hit information for spawning impact effects across the network.
    /// </summary>
    [System.Serializable]
    public struct ImpactVisualData : INetworkSerializable
    {
        /// <summary>
        /// The world position where the impact occurred.
        /// </summary>
        public Vector3 impactPosition;

        /// <summary>
        /// The surface normal at the impact point.
        /// </summary>
        public Vector3 impactNormal;

        /// <summary>
        /// Whether the impact hit a valid target (used to determine impact effect type).
        /// </summary>
        public bool hitTarget;

        /// <summary>
        /// Serializes the impact data for network transmission.
        /// </summary>
        /// <typeparam name="T">The reader/writer type for serialization.</typeparam>
        /// <param name="serializer">The buffer serializer.</param>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref impactPosition);
            serializer.SerializeValue(ref impactNormal);
            serializer.SerializeValue(ref hitTarget);
        }
    }

    /// <summary>
    /// Network-serializable data structure for player weapon and aiming state.
    /// Used to synchronize weapon animations across the network.
    /// </summary>
    [System.Serializable]
    public struct PlayerWeaponState : INetworkSerializable
    {
        /// <summary>
        /// Whether the player currently has a weapon equipped.
        /// </summary>
        public bool hasWeapon;

        /// <summary>
        /// Whether the player is currently aiming.
        /// </summary>
        public bool isAiming;

        /// <summary>
        /// The index of the currently equipped weapon.
        /// </summary>
        public int weaponIndex;

        /// <summary>
        /// Blend weight for the aiming animation (0.0 to 1.0).
        /// </summary>
        public float weaponAim;

        /// <summary>
        /// Blend weight for the idle weapon animation (0.0 to 1.0).
        /// </summary>
        public float weaponIdle;

        /// <summary>
        /// Updates the weapon state and calculates animation blend weights.
        /// </summary>
        /// <param name="newHasWeapon">Whether the player has a weapon equipped.</param>
        /// <param name="newIsAiming">Whether the player is aiming.</param>
        /// <param name="newWeaponIndex">The index of the equipped weapon.</param>
        /// <returns>True if the state changed, false otherwise.</returns>
        public bool UpdateState(bool newHasWeapon, bool newIsAiming, int newWeaponIndex)
        {
            if (hasWeapon != newHasWeapon || isAiming != newIsAiming || weaponIndex != newWeaponIndex)
            {
                hasWeapon = newHasWeapon;
                isAiming = newIsAiming;
                weaponIndex = newWeaponIndex;

                // Calculate animation blend weights based on state
                if (hasWeapon)
                {
                    if (isAiming)
                    {
                        weaponAim = 1.0f;
                        weaponIdle = 0.0f;
                    }
                    else
                    {
                        weaponAim = 0.0f;
                        weaponIdle = 1.0f;
                    }
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Serializes the weapon state for network transmission.
        /// Only serializes blend weights when a weapon is equipped.
        /// </summary>
        /// <typeparam name="T">The reader/writer type for serialization.</typeparam>
        /// <param name="serializer">The buffer serializer.</param>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref hasWeapon);
            serializer.SerializeValue(ref isAiming);
            serializer.SerializeValue(ref weaponIndex);

            // Only serialize blend weights if the player has a weapon
            if (hasWeapon)
            {
                serializer.SerializeValue(ref weaponAim);
                serializer.SerializeValue(ref weaponIdle);
            }
            else
            {
                weaponAim = 0.0f;
                weaponIdle = 0.0f;
            }
        }
    }

    /// <summary>
    /// Network-serializable data structure for player animation rigging state.
    /// Controls IK weights and offsets for aiming animations.
    /// </summary>
    [System.Serializable]
    public struct PlayerAimRiggingState : INetworkSerializable
    {
        /// <summary>
        /// Whether the player has a weapon equipped.
        /// </summary>
        public bool hasWeapon;

        /// <summary>
        /// Whether the player is currently aiming.
        /// </summary>
        public bool isAiming;

        /// <summary>
        /// Offset for the right hand IK constraint.
        /// </summary>
        public Vector3 rightHandOffset;

        /// <summary>
        /// Weight for the right hand IK constraint (0.0 to 1.0).
        /// </summary>
        public float rightHandWeight;

        /// <summary>
        /// Offset for the spine aim constraint.
        /// </summary>
        public Vector3 spineOffset;

        /// <summary>
        /// Weight for the spine aim constraint (0.0 to 1.0).
        /// </summary>
        public float spineWeight;

        /// <summary>
        /// Weight for the left hand IK constraint (0.0 to 1.0).
        /// </summary>
        public float leftHandWeight;

        /// <summary>
        /// Serializes the rigging state for network transmission.
        /// </summary>
        /// <typeparam name="T">The reader/writer type for serialization.</typeparam>
        /// <param name="serializer">The buffer serializer.</param>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref hasWeapon);
            serializer.SerializeValue(ref isAiming);
            serializer.SerializeValue(ref rightHandOffset);
            serializer.SerializeValue(ref rightHandWeight);
            serializer.SerializeValue(ref spineOffset);
            serializer.SerializeValue(ref spineWeight);
            serializer.SerializeValue(ref leftHandWeight);
        }
    }
}
