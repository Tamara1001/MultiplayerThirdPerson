using UnityEngine;
using Unity.Netcode;
using Blocks.Gameplay.Core;
using UnityEngine.Playables;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// A hit processor for target practice objects that plays a Timeline animation when hit.
    /// Optionally plays a sound and spawns VFX.
    /// </summary>
    public class TargetPracticeHitProcessor : HitProcessor
    {
        #region Fields & Properties

        [Header("Timeline Settings")]
        [Tooltip("The PlayableDirector that controls the timeline to play when hit.")]
        [SerializeField] private PlayableDirector playableDirector;

        [Header("Audio Settings")]
        [Tooltip("Optional sound to play when hit.")]
        [SerializeField] private SoundDef hitSound;

        [Header("VFX Settings")]
        [Tooltip("Optional VFX prefab to spawn when hit.")]
        [SerializeField] private GameObject hitVfxPrefab;

        [Tooltip("Duration before the VFX is destroyed.")]
        [SerializeField] private float vfxDuration = 2f;

        #endregion

        #region Protected Methods

        /// <summary>
        /// Handles the hit event by triggering the visual and audio effects across all clients.
        /// </summary>
        /// <param name="info">Contains information about the hit, including the hit point location.</param>
        protected override void HandleHit(HitInfo info)
        {
            PlayHitEffectsRpc(info.hitPoint);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Network RPC that plays all hit effects (timeline animation, sound, and VFX) on all clients.
        /// Resets and replays the timeline from the beginning to ensure the animation plays every time.
        /// </summary>
        /// <param name="hitPoint">The world position where the hit occurred, used for positioning audio and VFX.</param>
        [Rpc(SendTo.Everyone)]
        private void PlayHitEffectsRpc(Vector3 hitPoint)
        {
            if (playableDirector != null)
            {
                // Reset timeline to beginning and replay to ensure animation triggers on each hit
                playableDirector.Stop();
                playableDirector.time = 0;
                playableDirector.Play();
            }

            if (hitSound != null)
            {
                CoreDirector.RequestAudio(hitSound)
                    .WithPosition(hitPoint)
                    .Play();
            }

            if (hitVfxPrefab != null)
            {
                CoreDirector.CreatePrefabEffect(hitVfxPrefab)
                    .WithPosition(hitPoint)
                    .WithDuration(vfxDuration)
                    .Create();
            }
        }

        #endregion
    }
}

