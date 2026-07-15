using System.Collections;
using UnityEngine;
using Blocks.Gameplay.Core;
using Blocks.Gameplay.Shooter;

namespace FFA
{
    /// <summary>
    /// An <see cref="IPlayerAddon"/> that listens to the <see cref="MatchManager.Phase"/>
    /// NetworkVariable and freezes / unfreezes the local player during the end-of-match
    /// screen without entering the full <see cref="PlayerLifeState.Eliminated"/> death path
    /// (which would trigger ragdoll, VFX, and stat-depleted events).
    ///
    /// ┌─ WHAT THIS DOES ────────────────────────────────────────────────────────────┐
    /// │  • EndScreen phase → disables CoreInputHandler, CoreCameraController,       │
    /// │    movement input, and shooting (via ShooterAddon.OnEliminated).            │
    /// │  • Active phase    → re-enables all of the above.                           │
    /// │  • Restarting phase → same as EndScreen (keeps freeze while world resets).  │
    /// │                                                                              │
    /// │  This addon runs on EVERY client for EVERY player object, but all          │
    /// │  state changes are guarded with IsOwner so they only affect the local       │
    /// │  player's own controls.                                                     │
    /// └─────────────────────────────────────────────────────────────────────────────┘
    ///
    /// ┌─ SETUP ─────────────────────────────────────────────────────────────────────┐
    /// │  Add this component to the Player prefab alongside ShooterAddon.           │
    /// │  CorePlayerManager discovers it automatically via GetComponents<IPlayerAddon>. │
    /// │  No Inspector wiring needed — it finds MatchManager at runtime.            │
    /// └─────────────────────────────────────────────────────────────────────────────┘
    /// </summary>
    [AddComponentMenu("FFA/Match Phase Addon")]
    public class FFA_MatchPhaseAddon : MonoBehaviour, IPlayerAddon
    {
        #region Private State

        private CorePlayerManager m_PlayerManager;
        private ShooterAddon      m_ShooterAddon;
        private Coroutine         m_SubscribeCoroutine;

        #endregion

        #region IPlayerAddon Implementation

        /// <summary>
        /// Stores the owning <see cref="CorePlayerManager"/> and grabs the sibling
        /// <see cref="ShooterAddon"/> reference for shooting control.
        /// Called once by <see cref="CorePlayerManager"/> in Awake — before any network activity.
        /// </summary>
        public void Initialize(CorePlayerManager playerManager)
        {
            m_PlayerManager = playerManager;
            // ShooterAddon is a sibling component on the same GameObject.
            m_ShooterAddon = playerManager.GetComponent<ShooterAddon>();
        }

        /// <summary>
        /// Called when the player's NetworkObject spawns on this client.
        /// Starts a coroutine that waits for MatchManager to be ready (it spawns
        /// slightly after the player prefab) and then subscribes to phase changes.
        /// Applies the current phase immediately so late-joining clients are synced.
        /// </summary>
        public void OnPlayerSpawn()
        {
            m_SubscribeCoroutine = StartCoroutine(WaitAndSubscribe());
        }

        /// <summary>
        /// Called when the player's NetworkObject despawns.
        /// Unsubscribes from MatchManager to prevent callbacks on a destroyed object.
        /// </summary>
        public void OnPlayerDespawn()
        {
            if (m_SubscribeCoroutine != null)
            {
                StopCoroutine(m_SubscribeCoroutine);
                m_SubscribeCoroutine = null;
            }

            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
            }
        }

        /// <summary>
        /// Called when the player's <see cref="PlayerLifeState"/> changes.
        /// This addon does not react to life-state transitions — it only reacts to
        /// <see cref="MatchManager.MatchPhase"/> changes.
        /// </summary>
        public void OnLifeStateChanged(PlayerLifeState previousState, PlayerLifeState newState)
        {
            // Intentionally empty: phase control is independent of life-state.
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Waits until <see cref="MatchManager.Instance"/> is available (it is a
        /// NetworkBehaviour that spawns slightly after player prefabs), then subscribes
        /// to <see cref="MatchManager.OnPhaseChanged"/> and applies the current phase.
        /// </summary>
        private IEnumerator WaitAndSubscribe()
        {
            yield return new WaitUntil(() => MatchManager.Instance != null);

            MatchManager.Instance.OnPhaseChanged += HandlePhaseChanged;

            // Apply the current phase immediately — handles the case where a player
            // joins mid-match while the end-screen is already showing.
            HandlePhaseChanged(MatchManager.Instance.Phase);
        }

        /// <summary>
        /// Reacts to a <see cref="MatchManager.MatchPhase"/> change by enabling or
        /// disabling the local player's input and shooting systems.
        ///
        /// Guard: only the owner of this player object should modify their own controls.
        /// Non-owner instances of this addon (remote players) do nothing here.
        /// </summary>
        /// <param name="phase">The new match phase.</param>
        private void HandlePhaseChanged(MatchManager.MatchPhase phase)
        {
            // Only control this client's own player.
            if (m_PlayerManager == null || !m_PlayerManager.IsOwner) return;

            bool allowInput = (phase == MatchManager.MatchPhase.Active);

            // ── Input & Camera ────────────────────────────────────────────────────
            // CoreInputHandler.enabled = false kills all raw Unity Input System
            // callbacks so no movement, look, jump, or fire events are raised.
            if (m_PlayerManager.CoreInput != null)
                m_PlayerManager.CoreInput.enabled = allowInput;

            // CoreCameraController.enabled = false stops the camera from processing
            // look input and prevents the player from turning during the end screen.
            if (m_PlayerManager.CoreCamera != null)
                m_PlayerManager.CoreCamera.enabled = allowInput;

            // SetMovementInputEnabled zeroes move input and sprint state so the
            // CharacterController receives no horizontal velocity next frame.
            m_PlayerManager.SetMovementInputEnabled(allowInput);

            // ── Shooting ──────────────────────────────────────────────────────────
            // ShooterAddon.OnEliminated(true) mirrors exactly what happens on death:
            //   weaponController.enabled = false  → fire/reload events are ignored
            //   weaponController.SetCurrentWeaponActive(false) → hides weapon mesh
            // OnEliminated(false) reverses both.
            if (m_ShooterAddon != null)
                m_ShooterAddon.OnEliminated(!allowInput);

            // ── Movement physics ──────────────────────────────────────────────────
            // CoreMovement.IsMovementEnabled = false keeps gravity active but stops
            // the ability pipeline (walk, jump, sprint). The player stays grounded.
            if (m_PlayerManager.CoreMovement != null)
                m_PlayerManager.CoreMovement.IsMovementEnabled = allowInput;
        }

        #endregion
    }
}
