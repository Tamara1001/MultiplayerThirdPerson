using UnityEngine;
using Blocks.Gameplay.Core;

namespace FFA
{
    // ─────────────────────────────────────────────────────────────────────────
    // KillConfirmedPayload
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Data payload carried by a <see cref="KillConfirmedEvent"/>.
    /// Contains the full identity of both the attacker and the victim so that
    /// any listener can display, log, or process the kill without needing to
    /// perform additional network lookups.
    /// </summary>
    [System.Serializable]
    public struct KillConfirmedPayload
    {
        /// <summary>The NetworkManager ClientId of the player who scored the kill.</summary>
        public ulong killerClientId;

        /// <summary>The NetworkManager ClientId of the player who was eliminated.</summary>
        public ulong victimClientId;

        /// <summary>
        /// The display name of the killer, resolved at the moment the kill was confirmed.
        /// Cached here so listeners do not need to do a network lookup.
        /// </summary>
        public string killerName;

        /// <summary>
        /// The display name of the victim, resolved at the moment the kill was confirmed.
        /// </summary>
        public string victimName;

        /// <summary>
        /// True when the killer and victim are the same player (self-elimination).
        /// Listeners can use this flag to display an alternate message such as
        /// "PlayerX eliminated themselves".
        /// </summary>
        public bool isSelfElimination => killerClientId == victimClientId;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // KillConfirmedEvent
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// A ScriptableObject event channel that broadcasts a <see cref="KillConfirmedPayload"/>
    /// whenever a player is eliminated by another player (or by themselves).
    ///
    /// Follows the same pattern as every other event in the template (e.g.,
    /// <see cref="StatChangeEvent"/>, <see cref="NotificationEvent"/>):
    ///   • Create one asset in the Project window via the context menu.
    ///   • Assign the asset to both the <see cref="FFA_KillFeedAddon"/> (raiser)
    ///     and the <see cref="KillFeedUI"/> (listener) in the Inspector.
    ///   • No direct references are needed between the raiser and the listener.
    /// </summary>
    [CreateAssetMenu(
        fileName = "KillConfirmedEvent",
        menuName  = "Game Events/FFA/Kill Confirmed Event")]
    public class KillConfirmedEvent : GameEvent<KillConfirmedPayload>
    {
        // Intentionally empty — all functionality is in GameEvent<T>.
    }
}
