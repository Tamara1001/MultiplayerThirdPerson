using UnityEngine;
using Unity.Netcode;
using Blocks.Gameplay.Core;

namespace FFA
{
    /// <summary>
    /// An <see cref="IPlayerAddon"/> that detects kill events for the FFA deathmatch mode
    /// and broadcasts them as a <see cref="KillConfirmedEvent"/> to any interested listener
    /// (e.g., <see cref="KillFeedUI"/>).
    ///
    /// ┌─ HOW IT WORKS ──────────────────────────────────────────────────────────────┐
    /// │  The template's CoreStatsHandler synchronises every stat change via a       │
    /// │  NetworkList<RuntimeStat>. When that list changes on any client,            │
    /// │  CoreStatsHandler.BroadcastStatChange fires the scene-wide StatChangeEvent   │
    /// │  with a StatChangePayload containing BOTH the victim and attacker ClientIds. │
    /// │                                                                              │
    /// │  This addon listens to that same event, filters for health depletions        │
    /// │  caused by Damage (i.e. a kill), resolves player display names and raises   │
    /// │  KillConfirmedEvent — a separate, focused event for kill-feed consumers.    │
    /// │                                                                              │
    /// │  Note: ShooterHitProcessor.RegisterKillForAttacker already increments       │
    /// │  CorePlayerState.KillCount. This addon does NOT touch kill counts; it only  │
    /// │  provides the kill-feed event that the template was missing.                │
    /// └─────────────────────────────────────────────────────────────────────────────┘
    ///
    /// ┌─ SETUP ─────────────────────────────────────────────────────────────────────┐
    /// │  1. Add this component to the Player prefab (same GameObject as             │
    /// │     ShooterAddon). CorePlayerManager discovers it automatically via         │
    /// │     GetComponents<IPlayerAddon>() in Awake.                                 │
    /// │  2. Create a KillConfirmedEvent asset:                                      │
    /// │       Assets → Create → Game Events → FFA → Kill Confirmed Event           │
    /// │  3. Assign the same asset to this component's "On Kill Confirmed" field     │
    /// │     AND to KillFeedUI's "On Kill Confirmed" field.                          │
    /// │  4. Assign the scene's StatChangeEvent asset to "On Stat Changed".          │
    /// └─────────────────────────────────────────────────────────────────────────────┘
    /// </summary>
    [AddComponentMenu("FFA/Kill Feed Addon")]
    public class FFA_KillFeedAddon : NetworkBehaviour, IPlayerAddon
    {
        #region Inspector Fields

        [Header("Listening on")]
        [Tooltip("The scene-wide StatChangeEvent asset (same one used by CoreHUD). " +
                 "This is the source of truth for health changes.")]
        [SerializeField] private StatChangeEvent onStatChanged;

        [Header("Broadcasting on")]
        [Tooltip("A KillConfirmedEvent asset shared with KillFeedUI. " +
                 "Create it via: Assets → Create → Game Events → FFA → Kill Confirmed Event")]
        [SerializeField] private KillConfirmedEvent onKillConfirmed;

        #endregion

        #region Private State

        /// <summary>
        /// Reference to the CorePlayerManager that owns this addon.
        /// Stored during <see cref="Initialize"/> and used in lifecycle methods.
        /// </summary>
        private CorePlayerManager m_PlayerManager;

        #endregion

        #region IPlayerAddon Implementation

        /// <summary>
        /// Called once by <see cref="CorePlayerManager"/> in Awake before any network
        /// activity. Stores the manager reference for later use.
        /// </summary>
        /// <param name="playerManager">The owning CorePlayerManager.</param>
        public void Initialize(CorePlayerManager playerManager)
        {
            m_PlayerManager = playerManager;
        }

        /// <summary>
        /// Called by <see cref="CorePlayerManager.OnNetworkSpawn"/> on every client.
        /// We subscribe to <see cref="StatChangeEvent"/> here so that this addon
        /// receives kill events regardless of which client is the local owner.
        ///
        /// Why not restrict to IsOwner?
        ///   StatChangeEvent fires on ALL clients because the NetworkList replication
        ///   triggers OnStatsListChanged everywhere. Every client needs to show the
        ///   kill feed, so we subscribe unconditionally.
        /// </summary>
        public void OnPlayerSpawn()
        {
            if (onStatChanged != null)
            {
                onStatChanged.RegisterListener(HandleStatChanged);
            }
            else
            {
                Debug.LogWarning(
                    $"[FFA_KillFeedAddon] 'onStatChanged' is not assigned on {gameObject.name}. " +
                    "Kill feed will not function. Assign the StatChangeEvent asset in the Inspector.",
                    this);
            }
        }

        /// <summary>
        /// Called by <see cref="CorePlayerManager.OnNetworkDespawn"/> on every client.
        /// Unregisters the listener to prevent memory leaks and ghost callbacks.
        /// </summary>
        public void OnPlayerDespawn()
        {
            if (onStatChanged != null)
            {
                onStatChanged.UnregisterListener(HandleStatChanged);
            }
        }

        /// <summary>
        /// Called when the player's <see cref="PlayerLifeState"/> changes.
        /// This addon has no visual state tied to life state, so this is a no-op.
        /// </summary>
        public void OnLifeStateChanged(PlayerLifeState previousState, PlayerLifeState newState)
        {
            // No visual state owned by this addon.
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Receives every StatChangePayload broadcast by <see cref="CoreStatsHandler"/>.
        /// Filters down to only health depletions caused by damage (i.e. kills), then
        /// resolves player names and raises <see cref="KillConfirmedEvent"/>.
        ///
        /// Kill detection criteria:
        ///   • statID == StatKeys.Health        — we only care about health
        ///   • sourceType == Damage             — ignore self-regen or fall damage
        ///   • currentValue &lt;= 0             — health has actually hit zero (the kill)
        /// </summary>
        /// <param name="payload">The stat change data from CoreStatsHandler.</param>
        private void HandleStatChanged(StatChangePayload payload)
        {
            // ── Filter: only process health being depleted by Damage ──────────
            if (payload.statID != StatKeys.Health)                    return;
            if (payload.sourceType != ModificationSource.Damage)      return;
            if (payload.currentValue > 0f)                            return;

            // ── Guard: prevent duplicate events when this addon is on multiple
            //    player prefabs in the scene. Only one addon instance should
            //    raise the event per kill. We nominate the VICTIM's own addon
            //    because it is guaranteed to be on the correct NetworkObject.
            //    The victim's OwnerClientId matches payload.targetPlayerId.
            if (m_PlayerManager == null) return;
            if (m_PlayerManager.PlayerState == null) return;
            if (m_PlayerManager.PlayerState.OwnerClientId != payload.targetPlayerId) return;

            // ── Resolve display names ─────────────────────────────────────────
            string killerName = ResolvePlayerName(payload.sourcePlayerId);
            string victimName = ResolvePlayerName(payload.targetPlayerId);

            // ── Build and raise the kill payload ──────────────────────────────
            var killPayload = new KillConfirmedPayload
            {
                killerClientId = payload.sourcePlayerId,
                victimClientId = payload.targetPlayerId,
                killerName     = killerName,
                victimName     = victimName
            };

            if (onKillConfirmed != null)
            {
                onKillConfirmed.Raise(killPayload);
            }
            else
            {
                Debug.LogWarning(
                    "[FFA_KillFeedAddon] 'onKillConfirmed' is not assigned. " +
                    "Kill event was detected but could not be broadcast. " +
                    "Assign the KillConfirmedEvent asset in the Inspector.",
                    this);
            }
        }

        /// <summary>
        /// Resolves a player's display name from their NetworkManager ClientId.
        /// Uses <see cref="NetworkManager.SpawnManager.GetPlayerNetworkObject"/> which
        /// is the same pattern used by <see cref="CoreHUD.GetPlayerName"/>.
        ///
        /// Falls back to "Player-{clientId}" if the player object or its
        /// <see cref="CorePlayerState"/> cannot be found (e.g., during disconnection).
        /// </summary>
        /// <param name="clientId">The NetworkManager ClientId to look up.</param>
        /// <returns>The player's display name, or a safe fallback string.</returns>
        private string ResolvePlayerName(ulong clientId)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null)
            {
                var playerObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
                if (playerObject != null &&
                    playerObject.TryGetComponent<CorePlayerState>(out var playerState))
                {
                    string name = playerState.PlayerName;
                    if (!string.IsNullOrEmpty(name))
                    {
                        return name;
                    }
                }
            }

            // Safe fallback — always returns a non-null, non-empty string.
            return $"Player-{clientId}";
        }

        #endregion
    }
}
