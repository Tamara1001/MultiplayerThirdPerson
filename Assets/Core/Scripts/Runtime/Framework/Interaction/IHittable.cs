using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Defines the contract for any game object that can be "hit" by another entity, such as a projectile or a melee attack.
    /// This interface provides a standardized system for receiving hit information. Components implementing this interface, like
    /// <see cref="HitProcessor"/>, can be attached to players, enemies, or destructible objects to handle incoming damage or forces.
    /// It includes a client-side entry point (<see cref="OnHit"/>) and the necessary RPC for server-authoritative processing (<see cref="SubmitHitRpc"/>).
    /// </summary>
    public interface IHittable
    {
        #region Public Methods

        /// <summary>
        /// The primary method called on a client when a hit is detected on this object.
        /// The implementation of this method is responsible for initiating the process of sending the hit data
        /// to the server for authoritative validation.
        /// </summary>
        /// <param name="info">A struct containing all relevant data about the hit (damage, position, attacker, etc.).</param>
        void OnHit(HitInfo info);

        /// <summary>
        /// A server-bound RPC (Remote Procedure Call) that transmits the hit information.
        /// This ensures that the consequences of a hit (like dealing damage) are processed authoritatively,
        /// preventing cheating.
        /// </summary>
        /// <param name="info">The hit data to be sent to the authority.</param>
        /// <param name="rpcParams">Optional RPC parameters for targeting specific clients (not typically used here).</param>
        [Rpc(SendTo.Authority)]
        void SubmitHitRpc(HitInfo info, RpcParams rpcParams = default);

        #endregion
    }
}
