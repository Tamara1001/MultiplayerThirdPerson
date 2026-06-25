using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// An abstract base class for components that can process hit information. It implements the <see cref="IHittable"/>
    /// interface to provide a standardized way of receiving hit data. Its primary role is to act as a client-side
    /// entry point for a hit, which it then forwards to the authority via an RPC for authoritative processing.
    /// Derived classes (e.g., <see cref="ShooterHitProcessor"/>, <see cref="PhysicsObjectHitProcessor"/>) must implement the
    /// <see cref="HandleHit"/> method to define the specific consequences of a hit, such as applying damage or a physical force.
    /// </summary>
    /// <remarks>
    /// Network Flow:
    /// 1. Client detects hit → calls OnHit()
    /// 2. OnHit() checks authority:
    ///    - If authority (server/host): processes immediately via HandleHit()
    ///    - If not authority (client): sends RPC to server
    /// 3. Server receives RPC → calls HandleHit() authoritatively
    /// 4. HandleHit() applies game logic (damage, physics, etc.)
    ///
    /// This pattern ensures all hit processing happens on the server, preventing client-side cheating.
    /// </remarks>
    public abstract class HitProcessor : NetworkBehaviour, IHittable
    {
        #region IHittable Implementation

        /// <summary>
        /// Public entry point called when this object is hit by something on a client.
        /// This method takes the hit information and forwards it to the authority via an RPC for validation and processing.
        /// </summary>
        /// <param name="info">A struct containing all relevant data about the hit (damage, position, attacker, etc.).</param>
        public void OnHit(HitInfo info)
        {
            // Check if this instance has authority (is the server or owner with authority)
            if (HasAuthority)
            {
                // We're already on the authoritative instance, process the hit directly
                HandleHit(info);
            }
            else
            {
                // We're on a non-authoritative client, send the hit to the server for validation
                SubmitHitRpc(info);
            }
        }

        /// <summary>
        /// An RPC (Remote Procedure Call) sent to Authority. This receives the hit information on the
        /// authoritative instance and passes it to the HandleHit method for processing.
        /// </summary>
        /// <param name="info">The hit data sent from the client.</param>
        /// <param name="rpcParams">RPC parameters (unused in this case).</param>
        [Rpc(SendTo.Authority)]
        public void SubmitHitRpc(HitInfo info, RpcParams rpcParams = default)
        {
            // This method executes only on the server/host, ensuring that the game logic for the hit
            // is handled authoritatively. This prevents clients from manipulating hit data or
            // applying unauthorized damage/effects.
            HandleHit(info);
        }

        #endregion

        #region Protected Abstract Methods

        /// <summary>
        /// The core logic that defines what happens when this object is hit. This method must be implemented by
        /// any class that inherits from HitProcessor. It is called only on the server/authoritative instance.
        /// </summary>
        /// <param name="info">The validated hit information to be processed.</param>
        /// <remarks>
        /// Implementations should define specific behavior such as:
        /// - Applying damage to health components
        /// - Applying physics forces to rigidbodies
        /// - Triggering visual/audio effects
        /// - Updating game state or statistics
        /// </remarks>
        protected abstract void HandleHit(HitInfo info);

        #endregion
    }
}
