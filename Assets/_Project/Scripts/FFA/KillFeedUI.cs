using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Blocks.Gameplay.Core;

namespace FFA
{
    /// <summary>
    /// Manages the Kill Feed HUD panel for the FFA deathmatch mode.
    ///
    /// ┌─ DESIGN RATIONALE ──────────────────────────────────────────────────────────┐
    /// │  This class extends CoreHUD using the established virtual-method hooks       │
    /// │  (QueryHUDElements, SetHUDDefaults, RegisterAdditionalListeners,            │
    /// │  UnregisterAdditionalListeners). This means:                                 │
    /// │    • The base class handles health bars, respawn overlay, and the existing  │
    /// │      elimination notification system automatically.                          │
    /// │    • KillFeedUI only adds the dedicated left-side kill feed panel on top.   │
    /// │    • No template scripts are modified.                                       │
    /// │                                                                              │
    /// │  The kill feed renders entries as pure UI Toolkit VisualElements to match   │
    /// │  the rest of the project (CoreHUD, WeaponHUD both use UI Toolkit).          │
    /// │  TextMeshPro is NOT used for HUD elements in this template (it is only     │
    /// │  used for 3D world-space canvases such as NamePlateAddon).                  │
    /// └─────────────────────────────────────────────────────────────────────────────┘
    ///
    /// ┌─ SETUP ─────────────────────────────────────────────────────────────────────┐
    /// │  This component replaces (or accompanies) the existing HUD script on the   │
    /// │  local player's UIDocument GameObject. Use one of two approaches:           │
    /// │                                                                              │
    /// │  OPTION A — Standalone (recommended for clean separation):                  │
    /// │    1. Create a NEW GameObject with a UIDocument component.                  │
    /// │    2. Attach KillFeedUI to that GameObject.                                 │
    /// │    3. Assign the KillFeedUI.uxml as its Source Asset.                       │
    /// │    4. Set Sort Order higher than the existing HUD (e.g., 2).               │
    /// │    5. Assign the KillConfirmedEvent asset to the 'On Kill Confirmed' field. │
    /// │                                                                              │
    /// │  OPTION B — Extending the existing MatchHUD:                                │
    /// │    Replace MatchHUD with KillFeedUI on the existing UIDocument GameObject.  │
    /// │    KillFeedUI extends CoreHUD → all base functionality is preserved.        │
    /// │    The UXML must include the kill-feed container element (see .uxml file).  │
    /// └─────────────────────────────────────────────────────────────────────────────┘
    /// </summary>
    [AddComponentMenu("FFA/Kill Feed UI")]
    public class KillFeedUI : CoreHUD
    {
        #region Inspector Fields

        [Header("FFA — Kill Feed")]
        [Tooltip("ScriptableObject event raised by FFA_KillFeedAddon when a player is eliminated. " +
                 "Assign the same KillConfirmedEvent asset that FFA_KillFeedAddon uses.")]
        [SerializeField] private KillConfirmedEvent onKillConfirmed;

        [Tooltip("Maximum number of kill entries visible in the feed at the same time. " +
                 "Older entries are removed when this limit is reached.")]
        [SerializeField] private int maxFeedEntries = 5;

        [Tooltip("How many seconds each kill entry stays visible before fading out.")]
        [SerializeField] private float entryDisplayDuration = 4f;

        [Tooltip("Duration of the opacity fade-out animation in seconds.")]
        [SerializeField] private float entryFadeDuration = 0.4f;

        #endregion

        #region Private State

        // ─── UI Toolkit element references ────────────────────────────────────
        /// <summary>Root container for all kill-feed entries, anchored to the left side of the screen.</summary>
        private VisualElement m_KillFeedContainer;

        // ─── Entry tracking ────────────────────────────────────────────────────
        /// <summary>
        /// Live list of all currently displayed kill-feed entries.
        /// New entries are appended to the bottom; the oldest is removed from the top
        /// when <see cref="maxFeedEntries"/> is exceeded.
        /// </summary>
        private readonly List<KillFeedEntry> m_ActiveEntries = new List<KillFeedEntry>();

        // ─── Match Info Elements ──────────────────────────────────────────────
        private Label m_MatchTimerLabel;
        private Label m_KillCountLabel;

        #endregion

        #region Nested Types

        /// <summary>
        /// Lightweight data class tracking a single kill-feed row:
        /// its VisualElement in the UI tree and its active fade coroutine.
        /// </summary>
        private class KillFeedEntry
        {
            public VisualElement Root;
            public Coroutine     LifetimeCoroutine;
        }

        #endregion

        #region CoreHUD Virtual Hook Overrides

        /// <summary>
        /// Called during OnNetworkSpawn to allow derived classes to perform additional initialization.
        /// Starts the coroutine to wait for MatchManager.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
            StartCoroutine(WaitForMatchManagerAndSubscribe());
        }

        // ──────────────────────────────────────────────────────────────────────
        // QueryHUDElements
        // Called by CoreHUD.CreateHUD() AFTER the UIDocument is initialised.
        // Safe place to cache references to our kill-feed UXML elements.
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Queries and caches the kill-feed container from the UIDocument's visual tree.
        /// Called automatically by <see cref="CoreHUD"/> during network spawn.
        /// </summary>
        /// <param name="root">The root VisualElement of the UIDocument.</param>
        protected override void QueryHUDElements(VisualElement root)
        {
            base.QueryHUDElements(root);

            // The UXML must declare: <VisualElement name="kill-feed-container" .../>
            m_KillFeedContainer = root.Q<VisualElement>("kill-feed-container");

            if (m_KillFeedContainer == null)
            {
                Debug.LogWarning(
                    "[KillFeedUI] Could not find a VisualElement named 'kill-feed-container' " +
                    "in the UIDocument's UXML. Kill feed will not render. " +
                    "Add the element to the UXML or assign the correct KillFeedUI.uxml asset.",
                    this);
            }

            m_MatchTimerLabel = root.Q<Label>("match-timer-label");
            if (m_MatchTimerLabel == null)
            {
                Debug.LogError("[KillFeedUI] Could not find 'match-timer-label' in UXML.");
            }

            m_KillCountLabel = root.Q<Label>("kill-count-label");
            if (m_KillCountLabel == null)
            {
                Debug.LogError("[KillFeedUI] Could not find 'kill-count-label' in UXML.");
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // SetHUDDefaults
        // Called by CoreHUD.CreateHUD() after QueryHUDElements.
        // Safe place to set initial visibility or default values.
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Ensures the kill-feed container starts empty and visible.
        /// </summary>
        protected override void SetHUDDefaults()
        {
            base.SetHUDDefaults();

            if (m_KillFeedContainer != null)
            {
                m_KillFeedContainer.Clear();
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // RegisterAdditionalListeners / UnregisterAdditionalListeners
        // These hooks are called by CoreHUD.RegisterEventListeners() and
        // CoreHUD.UnregisterEventListeners() respectively. This is the correct,
        // non-destructive place to add our own event subscriptions.
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Subscribes to <see cref="KillConfirmedEvent"/> so that every kill
        /// detected by <see cref="FFA_KillFeedAddon"/> causes a new feed entry.
        /// </summary>
        protected override void RegisterAdditionalListeners()
        {
            if (onKillConfirmed != null)
            {
                onKillConfirmed.RegisterListener(HandleKillConfirmed);
            }
            else
            {
                Debug.LogWarning(
                    "[KillFeedUI] 'onKillConfirmed' is not assigned. " +
                    "Kill feed entries will not appear. " +
                    "Assign the KillConfirmedEvent asset in the Inspector.",
                    this);
            }

            // Subscribe to local player's kill count
            if (TryGetComponent<CorePlayerState>(out var playerState))
            {
                playerState.OnKillCountChanged += UpdateKillCountDisplay;
                UpdateKillCountDisplay(playerState.KillCount);
            }
        }

        /// <summary>
        /// Unsubscribes from <see cref="KillConfirmedEvent"/> on despawn
        /// to prevent memory leaks and callbacks on destroyed objects.
        /// </summary>
        protected override void UnregisterAdditionalListeners()
        {
            if (onKillConfirmed != null)
            {
                onKillConfirmed.UnregisterListener(HandleKillConfirmed);
            }

            if (TryGetComponent<CorePlayerState>(out var playerState))
            {
                playerState.OnKillCountChanged -= UpdateKillCountDisplay;
            }

            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnTimerUpdated -= UpdateTimerDisplay;
            }
        }

        #endregion

        #region Match Info Logic

        private IEnumerator WaitForMatchManagerAndSubscribe()
        {
            yield return new WaitUntil(() => MatchManager.Instance != null);
            MatchManager.Instance.OnTimerUpdated += UpdateTimerDisplay;
            UpdateTimerDisplay(MatchManager.Instance.TimeRemaining);
        }

        private void UpdateTimerDisplay(float timeRemaining)
        {
            if (m_MatchTimerLabel == null) return;
            
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            m_MatchTimerLabel.text = $"{minutes:00}:{seconds:00}";
        }

        private void UpdateKillCountDisplay(int kills)
        {
            if (m_KillCountLabel != null)
            {
                m_KillCountLabel.text = $"KILLS : {kills}";
            }
        }

        #endregion

        #region Kill Feed Logic

        /// <summary>
        /// Receives a <see cref="KillConfirmedPayload"/> and creates a new kill-feed entry.
        /// Enforces the <see cref="maxFeedEntries"/> cap by removing the oldest entry first.
        /// </summary>
        /// <param name="payload">
        /// The kill data: killer name, victim name, and their respective ClientIds.
        /// </param>
        private void HandleKillConfirmed(KillConfirmedPayload payload)
        {
            if (m_KillFeedContainer == null) return;

            // ── Enforce capacity limit ────────────────────────────────────────
            if (m_ActiveEntries.Count >= maxFeedEntries)
            {
                RemoveEntry(m_ActiveEntries[0]);
            }

            // ── Build the text ────────────────────────────────────────────────
            string entryText = payload.isSelfElimination
                ? $"{payload.victimName} eliminated themselves"
                : $"{payload.killerName}  ✕  {payload.victimName}";

            // ── Build the VisualElement ───────────────────────────────────────
            var entry = BuildEntryElement(payload);

            // ── Track and display ─────────────────────────────────────────────
            // 'data' must be declared before the coroutine lambda that captures it.
            // Splitting into two statements resolves CS0841 (use before declaration).
            var data = new KillFeedEntry { Root = entry };
            data.LifetimeCoroutine = StartCoroutine(EntryLifetimeRoutine(entry,
                                                     entryDisplayDuration,
                                                     entryFadeDuration,
                                                     () => RemoveEntry(data)));

            m_ActiveEntries.Add(data);
            m_KillFeedContainer.Add(entry);
        }

        /// <summary>
        /// Constructs the VisualElement hierarchy for a single kill-feed row.
        ///
        /// Layout (left-to-right, inside a horizontal flex container):
        ///   [ killer name label ]  [ skull icon label ]  [ victim name label ]
        ///
        /// CSS classes are defined in KillFeedUI.uss and follow the template's
        /// existing <c>notification-item</c> naming convention.
        /// </summary>
        /// <param name="payload">Kill data used to populate the labels.</param>
        /// <returns>The fully configured root VisualElement for this entry.</returns>
        private VisualElement BuildEntryElement(KillConfirmedPayload payload)
        {
            // ── Root row container ────────────────────────────────────────────
            var row = new VisualElement();
            row.AddToClassList("kill-feed-entry");

            if (payload.isSelfElimination)
            {
                // Self-elimination gets a distinct style (grey / muted)
                row.AddToClassList("kill-feed-entry--self");
            }

            // ── Killer name ───────────────────────────────────────────────────
            var killerLabel = new Label(payload.killerName);
            killerLabel.AddToClassList("kill-feed-killer");

            // Highlight if the local player is the killer
            if (payload.killerClientId == Unity.Netcode.NetworkManager.Singleton?.LocalClientId)
            {
                killerLabel.AddToClassList("kill-feed-killer--local");
            }

            // ── Separator icon ────────────────────────────────────────────────
            var separator = new Label(payload.isSelfElimination ? "☠" : "✕");
            separator.AddToClassList("kill-feed-separator");

            // ── Victim name ───────────────────────────────────────────────────
            var victimLabel = new Label(payload.victimName);
            victimLabel.AddToClassList("kill-feed-victim");

            // Highlight if the local player is the victim
            if (payload.victimClientId == Unity.Netcode.NetworkManager.Singleton?.LocalClientId)
            {
                victimLabel.AddToClassList("kill-feed-victim--local");
            }

            // ── Assemble ──────────────────────────────────────────────────────
            if (!payload.isSelfElimination)
            {
                row.Add(killerLabel);
            }
            row.Add(separator);
            row.Add(victimLabel);

            return row;
        }

        /// <summary>
        /// Coroutine that manages the full lifetime of a kill-feed entry:
        ///   1. Waits for <paramref name="displayDuration"/> seconds.
        ///   2. Adds the fade-out USS class (triggers CSS transition).
        ///   3. Waits for the transition to finish.
        ///   4. Invokes <paramref name="onComplete"/> to remove the entry.
        /// </summary>
        /// <param name="entry">The VisualElement to animate and remove.</param>
        /// <param name="displayDuration">Seconds to display before fading.</param>
        /// <param name="fadeDuration">Seconds for the opacity transition.</param>
        /// <param name="onComplete">Callback invoked after the fade completes.</param>
        private IEnumerator EntryLifetimeRoutine(
            VisualElement  entry,
            float          displayDuration,
            float          fadeDuration,
            System.Action  onComplete)
        {
            // Phase 1: display
            yield return new WaitForSeconds(displayDuration);

            // Phase 2: trigger CSS fade-out transition
            if (entry != null)
            {
                entry.AddToClassList("kill-feed-entry--fade-out");
            }

            // Phase 3: wait for the CSS transition to finish
            yield return new WaitForSeconds(fadeDuration);

            // Phase 4: remove from UI tree
            onComplete?.Invoke();
        }

        /// <summary>
        /// Removes a kill-feed entry from the UI tree and the active-entries list.
        /// Stops the entry's coroutine to prevent double-removal if called early.
        /// </summary>
        /// <param name="entry">The entry to remove.</param>
        private void RemoveEntry(KillFeedEntry entry)
        {
            if (entry == null) return;

            // Stop the coroutine if it is still running (e.g. capacity overflow)
            if (entry.LifetimeCoroutine != null)
            {
                StopCoroutine(entry.LifetimeCoroutine);
            }

            // Remove from the visual tree
            if (entry.Root != null && m_KillFeedContainer != null)
            {
                m_KillFeedContainer.Remove(entry.Root);
            }

            // Remove from tracking list
            m_ActiveEntries.Remove(entry);
        }

        #endregion
    }
}
