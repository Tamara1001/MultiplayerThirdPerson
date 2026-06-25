using UnityEngine;
using Unity.Netcode;
using System.Collections;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// An interaction effect that plays a sound effect when triggered.
    /// The sound is played on all clients via an RPC for synchronized audio.
    /// Supports both positional and attached sound playback.
    /// </summary>
    public class PlaySoundEffect : NetworkBehaviour, IInteractionEffect
    {
        #region Fields & Properties

        [Header("Sound Configuration")]
        [Tooltip("The sound definition to play when this effect is triggered.")]
        [SerializeField] private SoundDef soundToPlay;

        [Tooltip("The priority of this effect in the interaction chain. Higher values are executed first.")]
        [SerializeField] private int priority = 0;

        [Tooltip("Volume multiplier for this sound (0-1).")]
        [SerializeField] [Range(0f, 1f)] private float volume = 1.0f;

        [Header("Playback Settings")]
        [Tooltip("If true, attaches the sound to the interactable's transform so it follows the object. If false, plays at a fixed world position.")]
        [SerializeField] private bool attachToObject = false;

        [Tooltip("If true, reserves the sound emitter for sequential or RandomNotLast playback modes.")]
        [SerializeField] private bool reserveEmitter = false;

        [Tooltip("If true, only allow a single instance of this sound definition to play. Stops all sound emitters that are also playing this sound definition.")]
        [SerializeField] private bool forceSolo = false;

        public int Priority => priority;

        #endregion

        #region Public Methods

        /// <summary>
        /// Applies the sound effect by triggering playback on all clients.
        /// </summary>
        /// <param name="interactor">The GameObject that triggered the interaction.</param>
        /// <param name="interactable">The GameObject being interacted with.</param>
        /// <returns>A coroutine that completes after initiating the sound.</returns>
        public IEnumerator ApplyEffect(GameObject interactor, GameObject interactable)
        {
            if (soundToPlay == null)
            {
                Debug.LogWarning($"PlaySoundEffect on '{interactable.name}' has no SoundDef assigned.", interactable);
                yield break;
            }

            PlaySoundOnAllClientsRpc(interactable.transform.position);

            yield return null;
        }

        /// <summary>
        /// Cancels the effect. This is a no-op for sound effects as they are fire-and-forget.
        /// </summary>
        /// <param name="interactor">The GameObject that triggered the interaction.</param>
        public void CancelEffect(GameObject interactor)
        {
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// RPC that plays the sound on all connected clients for synchronized audio playback.
        /// </summary>
        [Rpc(SendTo.Everyone)]
        private void PlaySoundOnAllClientsRpc(Vector3 position)
        {
            if (soundToPlay == null) return;

            var soundRequest = CoreDirector.RequestAudio(soundToPlay);

            if (forceSolo)
            {
                soundRequest.ForceSolo();
            }

            if (attachToObject)
            {
                soundRequest.AttachedTo(transform);
            }
            else
            {
                soundRequest.WithPosition(position);
            }

            if (reserveEmitter)
            {
                soundRequest.AsReserved(SoundEmitter.ReservedInfo.ReservedEmitter);
            }

            soundRequest.Play(volume);
        }

        #endregion
    }
}
