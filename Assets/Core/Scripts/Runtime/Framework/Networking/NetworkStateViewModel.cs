using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// View model that maintains the current state of network connections
    /// and provides data for UI display
    /// </summary>
    public class NetworkStateViewModel
    {
        /// <summary>Current state of the network connection</summary>
        public GameNetworkManager.ConnectionStates ConnectionState { get; set; } = GameNetworkManager.ConnectionStates.None;

        /// <summary>
        /// Updates all state properties in a single atomic operation
        /// </summary>
        /// <param name="connectionState">Current connection state</param>
        /// <param name="topology">Network topology being used</param>
        /// <param name="netcodeStatus">Current Netcode status message</param>
        /// <param name="isConnectedOrHost">Whether connected as client or host</param>
        public void UpdateState(
            GameNetworkManager.ConnectionStates connectionState,
            NetworkTopologyTypes topology,
            string netcodeStatus,
            bool isConnectedOrHost)
        {
            ConnectionState = connectionState;
        }
    }
}
