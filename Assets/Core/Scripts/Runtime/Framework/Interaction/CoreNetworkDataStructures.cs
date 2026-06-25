using System;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// A struct that encapsulates all the necessary information about a single "hit" event.
    /// This is used to pass data from a source of damage (like a projectile or weapon) to a target
    /// that implements the <see cref="IHittable"/> interface. It is network-serializable to allow
    /// hit information to be sent from a client to the server for authoritative processing.
    /// </summary>
    [Serializable]
    public struct HitInfo : INetworkSerializable
    {
        #region Fields

        /// <summary>
        /// The base amount of damage or effect magnitude.
        /// </summary>
        public float amount;

        /// <summary>
        /// The world-space coordinate where the hit occurred.
        /// </summary>
        public Vector3 hitPoint;

        /// <summary>
        /// The surface normal at the point of impact.
        /// </summary>
        public Vector3 hitNormal;

        /// <summary>
        /// The NetworkObjectId of the player or entity that initiated the hit.
        /// </summary>
        public ulong attackerId;

        /// <summary>
        /// The physical force to be applied at the hit point, used for knockback or physics reactions.
        /// </summary>
        public Vector3 impactForce;

        #endregion

        #region INetworkSerializable Implementation

        /// <summary>
        /// Defines how the struct data is written to and read from the network stream.
        /// </summary>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref amount);
            serializer.SerializeValue(ref hitPoint);
            serializer.SerializeValue(ref hitNormal);
            serializer.SerializeValue(ref attackerId);
            serializer.SerializeValue(ref impactForce);
        }

        #endregion
    }

    /// <summary>
    /// A struct representing the current state of a single gameplay stat (e.g., Health, Stamina) at runtime.
    /// This structure is designed to be lightweight and efficient for network synchronization within a <see cref="NetworkList{T}"/>.
    /// It implements <see cref="INetworkSerializable"/> to define how its data is sent over the network and
    /// <see cref="IEquatable{T}"/> for efficient comparisons.
    /// </summary>
    public struct RuntimeStat : INetworkSerializable, IEquatable<RuntimeStat>
    {
        #region Fields

        /// <summary>
        /// A unique integer hash representing the stat's name (e.g., from Animator.StringToHash("Health")).
        /// Using a hash is more network-efficient than sending the full string name.
        /// </summary>
        public int StatHash;

        /// <summary>
        /// The current floating-point value of the stat.
        /// </summary>
        public float CurrentValue;

        /// <summary>
        /// The unique identifier of the player who caused this stat modification.
        /// Used to track the source of damage, healing, or other stat changes for attribution purposes.
        /// </summary>
        public ulong SourcePlayerId;

        /// <summary>
        /// The type of source that caused this stat modification (e.g., player, environment, ability).
        /// </summary>
        public ModificationSource SourceType;

        #endregion

        #region INetworkSerializable Implementation

        /// <summary>
        /// Defines how the struct data is written to and read from the network stream.
        /// </summary>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref StatHash);
            serializer.SerializeValue(ref CurrentValue);
            serializer.SerializeValue(ref SourcePlayerId);
            serializer.SerializeValue(ref SourceType);
        }

        #endregion

        #region IEquatable Implementation

        /// <summary>
        /// Compares this RuntimeStat instance to another for equality.
        /// Two instances are considered equal if both their statHash and currentValue are the same.
        /// </summary>
        public bool Equals(RuntimeStat other)
        {
            return StatHash == other.StatHash &&
                   Mathf.Approximately(CurrentValue, other.CurrentValue) &&
                   SourcePlayerId == other.SourcePlayerId &&
                   SourceType == other.SourceType;
        }

        #endregion
    }

    /// <summary>
    /// A struct representing a player's essential network identity information.
    /// This structure is used to synchronize player data across the network, containing
    /// the minimum information needed to identify and display a player in a multiplayer session.
    /// It implements <see cref="INetworkSerializable"/> for efficient network transmission and
    /// <see cref="IEquatable{T}"/> for efficient comparisons in network collections.
    /// </summary>
    [Serializable]
    public struct PlayerData : INetworkSerializable, IEquatable<PlayerData>
    {
        #region Fields

        /// <summary>
        /// The unique client identifier assigned by the network session.
        /// This ID is used to distinguish between different connected clients.
        /// </summary>
        public ulong clientId;

        /// <summary>
        /// The display name of the player, stored as a fixed-size string for network efficiency.
        /// Limited to 64 bytes to minimize bandwidth usage during synchronization.
        /// </summary>
        public FixedString64Bytes playerName;

        #endregion

        #region INetworkSerializable Implementation

        /// <summary>
        /// Defines how the struct data is written to and read from the network stream.
        /// </summary>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref clientId);
            serializer.SerializeValue(ref playerName);
        }

        #endregion

        #region IEquatable Implementation

        /// <summary>
        /// Compares this PlayerData instance to another for equality.
        /// Two instances are considered equal if both their clientId and playerName are the same.
        /// </summary>
        public bool Equals(PlayerData other)
        {
            return clientId == other.clientId && playerName.Equals(other.playerName);
        }

        /// <summary>
        /// Compares this PlayerData instance to an object for equality.
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is PlayerData other && Equals(other);
        }

        /// <summary>
        /// Generates a hash code for this PlayerData instance based on its clientId and playerName.
        /// Used for efficient storage and lookup in hash-based collections.
        /// </summary>
        public override int GetHashCode()
        {
            return (ClientId: clientId, PlayerName: playerName).GetHashCode();
        }

        #endregion
    }
}
