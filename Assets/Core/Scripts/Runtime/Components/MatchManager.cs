using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Manages the FFA match lifecycle: timer countdown, winner determination,
    /// end-of-match phase transitions, world cleanup, and full player reset.
    ///
    /// ┌─ MATCH PHASE STATE MACHINE ─────────────────────────────────────────────────┐
    /// │  Active      → Normal gameplay. All player input is enabled.               │
    /// │  EndScreen   → Winner announced. All input frozen for <restartDelay> secs. │
    /// │  Restarting  → Brief transition while world is cleaned and players reset.  │
    /// │                At the end, phase returns to Active.                         │
    /// └─────────────────────────────────────────────────────────────────────────────┘
    ///
    /// ┌─ AUTHORITY NOTES ───────────────────────────────────────────────────────────┐
    /// │  All NetworkVariable writes happen on the session owner (IsOwner == true).  │
    /// │  ResetAllPlayers() runs only on the server/session owner and uses RPCs      │
    /// │  to route owner-only operations (heal, ammo) to each player's own client.  │
    /// └─────────────────────────────────────────────────────────────────────────────┘
    ///
    /// ┌─ SETUP ─────────────────────────────────────────────────────────────────────┐
    /// │  1. Assign 'spawnPoints' in the Inspector (Transforms in the scene).       │
    /// │  2. Assign 'powerUpSpawners' (all PowerUpSpawner instances in the scene).  │
    /// │  3. Add FFA_MatchPhaseAddon to the Player prefab — it subscribes here.     │
    /// └─────────────────────────────────────────────────────────────────────────────┘
    /// </summary>
    public class MatchManager : NetworkBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────────
        // MatchPhase enum — public so FFA_MatchPhaseAddon and UI can read it
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Represents the current state of the FFA match lifecycle.
        /// Synchronized via <see cref="m_Phase"/> so all clients react simultaneously.
        /// </summary>
        public enum MatchPhase : byte
        {
            /// <summary>Normal gameplay — all player input is enabled.</summary>
            Active,

            /// <summary>
            /// Timer hit zero. Winner screen is shown.
            /// All player input is frozen by <see cref="FFA_MatchPhaseAddon"/>.
            /// </summary>
            EndScreen,

            /// <summary>
            /// Brief transition while the world is cleaned up and players are reset.
            /// Input stays frozen. Returns to <see cref="Active"/> when done.
            /// </summary>
            Restarting
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Singleton
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Singleton instance. Set on <see cref="OnNetworkSpawn"/>, cleared on despawn.
        /// FFA_MatchPhaseAddon polls this via WaitUntil() to subscribe safely.
        /// </summary>
        public static MatchManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticInstance() => Instance = null;

        // ─────────────────────────────────────────────────────────────────────────
        // Inspector Fields
        // ─────────────────────────────────────────────────────────────────────────

        [Header("Match Settings")]
        [Tooltip("Total match duration in seconds (default: 300 = 5 minutes).")]
        [SerializeField] private float matchDuration = 300f;

        [Tooltip("Seconds the winner screen stays visible before the match restarts.")]
        [SerializeField] private float restartDelay = 10f;

        [Header("Spawning")]
        [Tooltip("List of Transforms used as respawn locations. Players are cycled through these.")]
        [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

        // ─────────────────────────────────────────────────────────────────────────
        // NetworkVariables — written by session owner, read by all
        // ─────────────────────────────────────────────────────────────────────────

        private readonly NetworkVariable<float> m_TimeRemaining = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<bool> m_MatchEnded = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<ulong> m_WinnerId = new NetworkVariable<ulong>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>
        /// Current match lifecycle phase. Written by the session owner.
        /// All clients react via the <see cref="OnPhaseChanged"/> C# event
        /// (raised by the NetworkVariable's OnValueChanged callback).
        /// </summary>
        private readonly NetworkVariable<MatchPhase> m_Phase = new NetworkVariable<MatchPhase>(
            MatchPhase.Active,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        // ─────────────────────────────────────────────────────────────────────────
        // Public Properties & C# Events
        // ─────────────────────────────────────────────────────────────────────────

        public float      TimeRemaining => m_TimeRemaining.Value;
        public bool       MatchEnded    => m_MatchEnded.Value;
        public ulong      WinnerId      => m_WinnerId.Value;

        /// <summary>
        /// The current match phase. Read by <see cref="FFA_MatchPhaseAddon"/> to apply
        /// the current state on late-join.
        /// </summary>
        public MatchPhase Phase => m_Phase.Value;

        /// <summary>Raised on all clients when the timer ticks (via NetworkVariable callback).</summary>
        public event System.Action<float>      OnTimerUpdated;

        /// <summary>Raised on all clients when the match ends (winner declared).</summary>
        public event System.Action<ulong>      OnMatchEndedEvent;

        /// <summary>
        /// Raised on all clients whenever <see cref="MatchPhase"/> changes.
        /// <see cref="FFA_MatchPhaseAddon"/> subscribes here to freeze/unfreeze input.
        /// </summary>
        public event System.Action<MatchPhase> OnPhaseChanged;

        // ─────────────────────────────────────────────────────────────────────────
        // Network Lifecycle
        // ─────────────────────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            Instance = this;

            if (IsOwner)
            {
                m_TimeRemaining.Value = matchDuration;
                m_MatchEnded.Value    = false;
                m_Phase.Value         = MatchPhase.Active;
            }

            // Subscribe to all NetworkVariable changes on every client
            m_TimeRemaining.OnValueChanged += (_, v)  => OnTimerUpdated?.Invoke(v);
            m_MatchEnded.OnValueChanged    += HandleMatchEndedChanged;
            m_Phase.OnValueChanged         += (_, p)  => OnPhaseChanged?.Invoke(p);
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;

            m_TimeRemaining.OnValueChanged -= (_, v)  => OnTimerUpdated?.Invoke(v);
            m_MatchEnded.OnValueChanged    -= HandleMatchEndedChanged;
            m_Phase.OnValueChanged         -= (_, p)  => OnPhaseChanged?.Invoke(p);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Unity Update (session owner only)
        // ─────────────────────────────────────────────────────────────────────────

        private void Update()
        {
            // Only the session owner runs the timer
            if (!IsOwner || m_MatchEnded.Value) return;

            m_TimeRemaining.Value -= Time.deltaTime;
            if (m_TimeRemaining.Value <= 0f)
            {
                m_TimeRemaining.Value = 0f;
                EndMatch();
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Match Lifecycle (session owner only)
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Determines the winner by highest kill count and transitions the match to
        /// the EndScreen phase. Kicks off the restart coroutine.
        /// Only runs on the session owner.
        /// </summary>
        private void EndMatch()
        {
            // ── Determine winner ──────────────────────────────────────────────────
            ulong winner   = 0;
            int bestKills  = -1;

            foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
            {
                var playerObj = kvp.Value.PlayerObject;
                var state     = playerObj != null ? playerObj.GetComponent<CorePlayerState>() : null;
                if (state != null && state.KillCount > bestKills)
                {
                    bestKills = state.KillCount;
                    winner    = kvp.Key;
                }
            }

            m_WinnerId.Value  = winner;
            m_MatchEnded.Value = true;

            // Freeze all players on every client immediately via the phase variable.
            // FFA_MatchPhaseAddon.HandlePhaseChanged(EndScreen) runs on all clients.
            m_Phase.Value = MatchPhase.EndScreen;

            StartCoroutine(RestartAfterDelay());
        }

        /// <summary>
        /// Waits for the end-screen timer, cleans up the world, resets all players,
        /// and restarts the match. Runs only on the session owner.
        /// </summary>
        private IEnumerator RestartAfterDelay()
        {
            // ── Phase 1: winner screen ────────────────────────────────────────────
            yield return new WaitForSeconds(restartDelay);

            // ── Phase 2: brief freeze during world reset ──────────────────────────
            m_Phase.Value = MatchPhase.Restarting;

            // Wait one frame so the phase change replicates before heavy operations
            yield return null;

            // ── Phase 3: reset kill counts ────────────────────────────────────────
            foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
            {
                var playerObj = kvp.Value.PlayerObject;
                playerObj?.GetComponent<CorePlayerState>()?.ResetKills();
            }

            // ── Phase 4: Wait for cleanup (handled externally by FFA_MatchResetCoordinator) ──
            yield return null;

            // ── Phase 5: reposition and restore all players ───────────────────────
            ResetAllPlayers();

            // Wait two frames for teleports to settle and RPCs to reach owners
            yield return null;
            yield return null;

            // ── Phase 6: restart match variables ─────────────────────────────────
            m_TimeRemaining.Value = matchDuration;
            m_MatchEnded.Value    = false;

            // Unfreeze all players on every client
            m_Phase.Value = MatchPhase.Active;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Player Reset (session owner — iterates ConnectedClients)
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Iterates all connected clients on the server and for each player:
        /// <list type="number">
        ///   <item>Teleports them to a spawn point via <see cref="CoreMovement.SetPosition"/>.</item>
        ///   <item>Zeroes their physics state via <see cref="CoreMovement.ResetMovementForces"/>.</item>
        ///   <item>Routes a full heal to the owner via <see cref="CorePlayerState.RequestFullHealRpc"/>.</item>
        ///   <item>Transitions the life state to Respawned via <see cref="CorePlayerState.SetLifeState"/>.</item>
        /// </list>
        /// Steps 3-4 use existing owner-routing patterns because NetworkList and
        /// NetworkVariable have Owner write permissions.
        /// </summary>
        private void ResetAllPlayers()
        {
            if (spawnPoints == null || spawnPoints.Count == 0)
            {
                Debug.LogWarning("[MatchManager] No spawn points assigned. Players will not be repositioned.", this);
            }

            int spawnIndex = 0;

            foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
            {
                var playerObj = kvp.Value.PlayerObject;
                if (playerObj == null) continue;

                // ── 1. Reposition ─────────────────────────────────────────────────
                if (playerObj.TryGetComponent<CoreMovement>(out var movement))
                {
                    Vector3    spawnPos = GetSpawnPosition(spawnIndex);
                    Quaternion spawnRot = GetSpawnRotation(spawnIndex);

                    movement.SetPosition(spawnPos);
                    movement.transform.rotation = spawnRot;

                    // Zero out velocity, external forces, and restore default gravity
                    movement.ResetMovementForces();

                    spawnIndex++;
                }

                // ── 2. Heal + 3. Restore life state ─────────────
                if (playerObj.TryGetComponent<CorePlayerState>(out var playerState))
                {
                    // Full heal: routes to the owner who writes to CoreStatsHandler's NetworkList
                    playerState.RequestFullHealRpc();

                    // Life state Respawned: re-enables input, camera, movement, weapon via CorePlayerManager
                    playerState.SetLifeState(PlayerLifeState.Respawned);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Private Helpers
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the world position of a spawn point by cycling through the list.
        /// Falls back to Vector3.zero if no spawn points are assigned.
        /// </summary>
        private Vector3 GetSpawnPosition(int index)
        {
            if (spawnPoints == null || spawnPoints.Count == 0) return Vector3.zero;
            var point = spawnPoints[index % spawnPoints.Count];
            return point != null ? point.position : Vector3.zero;
        }

        /// <summary>
        /// Returns the world rotation of a spawn point by cycling through the list.
        /// Falls back to Quaternion.identity if no spawn points are assigned.
        /// </summary>
        private Quaternion GetSpawnRotation(int index)
        {
            if (spawnPoints == null || spawnPoints.Count == 0) return Quaternion.identity;
            var point = spawnPoints[index % spawnPoints.Count];
            return point != null ? point.rotation : Quaternion.identity;
        }

        private void HandleMatchEndedChanged(bool oldVal, bool newVal)
        {
            if (newVal) OnMatchEndedEvent?.Invoke(m_WinnerId.Value);
        }
    }
}