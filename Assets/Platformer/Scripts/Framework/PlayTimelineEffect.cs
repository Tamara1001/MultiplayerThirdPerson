using UnityEngine;
using Unity.Netcode;
using System.Collections;
using Blocks.Gameplay.Core;
using UnityEngine.Playables;

namespace Blocks.Gameplay.Platformer
{
    /// <summary>
    /// An interaction effect that plays a Unity Timeline via PlayableDirector.
    /// The timeline is played on all clients via RPC for synchronized playback.
    /// </summary>
    public class PlayTimelineEffect : NetworkBehaviour, IInteractionEffect
    {
        #region Enums

        /// <summary>
        /// Defines how the effect should wait before proceeding to the next effect.
        /// </summary>
        public enum WaitMode
        {
            /// <summary>
            /// Don't wait, proceed immediately to the next effect.
            /// </summary>
            None,
            /// <summary>
            /// Wait until the timeline completes before proceeding.
            /// </summary>
            WaitForCompletion,
            /// <summary>
            /// Wait for a specified duration before proceeding.
            /// </summary>
            WaitForTime
        }

        #endregion

        #region Fields & Properties

        [Header("Timeline Settings")]
        [Tooltip("The PlayableDirector component that controls the timeline to play.")]
        [SerializeField] private PlayableDirector playableDirector;

        [Tooltip("The priority of this effect in the interaction chain. Higher values are executed first.")]
        [SerializeField] private int priority = 0;

        [Tooltip("Defines how long to wait before proceeding to the next effect.")]
        [SerializeField] private WaitMode waitMode = WaitMode.None;

        [Tooltip("The time in seconds to wait before proceeding to the next effect (only used when waitMode is WaitForTime).")]
        [SerializeField] private float waitTime = 0f;

        public int Priority => priority;

        #endregion

        #region Public Methods

        /// <summary>
        /// Applies the timeline effect by playing the assigned PlayableDirector on all clients.
        /// Waits according to the configured wait mode before allowing the next effect to proceed.
        /// </summary>
        /// <param name="interactor">The GameObject that triggered the interaction.</param>
        /// <param name="interactable">The GameObject being interacted with.</param>
        /// <returns>An IEnumerator for coroutine execution.</returns>
        public IEnumerator ApplyEffect(GameObject interactor, GameObject interactable)
        {
            if (playableDirector == null)
            {
                Debug.LogWarning("PlayableDirector is not assigned.", this);
                yield break;
            }

            PlayTimelineRpc();

            switch (waitMode)
            {
                case WaitMode.WaitForCompletion:
                    yield return new WaitWhile(() =>
                        playableDirector.state == PlayState.Playing);
                    break;

                case WaitMode.WaitForTime:
                    yield return new WaitForSeconds(waitTime);
                    break;

                case WaitMode.None:
                default:
                    yield return null;
                    break;
            }
        }

        /// <summary>
        /// Cancels the timeline playback if it is currently playing.
        /// </summary>
        /// <param name="interactor">The GameObject that initiated the cancellation.</param>
        public void CancelEffect(GameObject interactor)
        {
            if (playableDirector != null && playableDirector.state == PlayState.Playing)
            {
                playableDirector.Stop();
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// RPC that plays the timeline on all connected clients for synchronized playback.
        /// </summary>
        [Rpc(SendTo.Everyone)]
        private void PlayTimelineRpc()
        {
            if (playableDirector != null)
            {
                playableDirector.Play();
            }
        }

        #endregion
    }
}

