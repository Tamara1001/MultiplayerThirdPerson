namespace Blocks.Gameplay.Core
{
    public interface IPlayerAddon
    {
        /// <summary>
        /// Called once by the CorePlayerManager in Awake to provide a reference to itself.
        /// </summary>
        void Initialize(CorePlayerManager playerManager);

        /// <summary>
        /// Called when the player's network object is spawned (OnNetworkSpawn).
        /// </summary>
        void OnPlayerSpawn();

        /// <summary>
        /// Called when the player's network object is despawned (OnNetworkDespawn).
        /// </summary>
        void OnPlayerDespawn();

        /// <summary>
        /// Called when the player's life state changes (e.g., Alive -> Eliminated, Eliminated -> Respawned).
        /// Replaces the old OnEliminated method for granular state control.
        /// </summary>
        void OnLifeStateChanged(PlayerLifeState previousState, PlayerLifeState newState);
    }
}
