using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Manages the playback of audio through a collection of SoundGameObjects.
    /// A SoundEmitter controls multiple audio sources to play a single SoundDef, handling
    /// clip selection, volume, pitch, spatial audio, and repetition. It can be reserved
    /// to maintain state between playbacks (e.g., for sequential clip selection).
    /// SoundEmitters are pooled and managed by the SoundSystem for efficient audio playback.
    /// </summary>
    [Serializable]
    public class SoundEmitter
    {
        #region Enums

        /// <summary>
        /// Defines the reservation state of a SoundEmitter, controlling its lifecycle and reusability.
        /// </summary>
        public enum ReservedInfo
        {
            /// <summary>
            /// The emitter will be freed for reuse when playback completes.
            /// </summary>
            FreeAfterPlaybackCompletes,
            /// <summary>
            /// The emitter is reserved but its audio sources can be used by other emitters.
            /// </summary>
            ReservedEmitter,
            /// <summary>
            /// Both the emitter and its audio sources are reserved exclusively.
            /// </summary>
            ReservedEmitterAndAudioSources
        }

        #endregion

        #region Nested Classes

        /// <summary>
        /// Contains override data for SoundDef parameters that can be modified per-instance.
        /// This allows runtime customization of pitch, volume, and filter settings without
        /// modifying the original SoundDef asset.
        /// </summary>
        public class SoundDefOverrideData
        {
            /// <summary>
            /// Copied from SoundDef to allow user to override if required.
            /// </summary>
            public float BasePitchInCents;

            /// <summary>
            /// Copied from SoundDef.
            /// </summary>
            public float VolumeScale;

            /// <summary>
            /// Copied from SoundDef.
            /// </summary>
            public float BaseLowPassCutoff;

            /// <summary>
            /// Initializes a new instance with default values.
            /// </summary>
            public SoundDefOverrideData()
            {
                BaseLowPassCutoff = 0;
                BasePitchInCents = 0;
                VolumeScale = 1;
            }

            /// <summary>
            /// Initializes a new instance with values copied from a SoundDef.
            /// </summary>
            /// <param name="soundDef">The SoundDef to copy values from.</param>
            public SoundDefOverrideData(SoundDef soundDef)
            {
                BasePitchInCents = soundDef.basePitchInCents;
                VolumeScale = soundDef.volumeScale;
                BaseLowPassCutoff = soundDef.baseLowPassCutoff;
            }
        }

        #endregion

        #region Fields & Properties

        /// <summary>
        /// True if the emitter has been allocated from the pool.
        /// </summary>
        public bool allocated;

        /// <summary>
        /// Reserved type for this emitter. Reserve if you want to use RandomNotLast or Sequential clip selection.
        /// </summary>
        public ReservedInfo reserved;

        /// <summary>
        /// List of SoundGameObjects that are active for this SoundDef (using the SoundDef.PlayCount).
        /// </summary>
        public List<SoundGameObject> ActiveSoundGameObjects;

        /// <summary>
        /// Last Random clip index. Required to allow for "Play random, but don't play last".
        /// </summary>
        public uint randomClipIndex;

        /// <summary>
        /// If SoundDef clip index = User, the user can set this value to use as the clip index.
        /// </summary>
        public uint userClipIndex;

        /// <summary>
        /// This index is used if the SoundDef clip index = Sequential.
        /// </summary>
        public uint sequentialClipIndex;

        /// <summary>
        /// Base emitter volume (final volume = Volume * SoundDefOverrideInfo.VolumeScale * RandomVol(min/max) * KeyOffFadeOutVol).
        /// </summary>
        public float volume;

        /// <summary>
        /// Override data for SoundDef parameters.
        /// </summary>
        public SoundDefOverrideData SoundDefOverrideInfo;

        /// <summary>
        /// Time (in seconds) to fade out the SoundGameObject when it is requested to stop.
        /// </summary>
        public Interpolator FadeOutTime;

        /// <summary>
        /// ID of this SoundEmitter. Used to validate user calls to play/stop etc.
        /// </summary>
        public int seqId;

        /// <summary>
        /// True if there are active SoundGameObjects or the SoundEmitter is reserved.
        /// </summary>
        public bool active;

        /// <summary>
        /// Pool of SoundGameObjects that can be allocated for this emitter.
        /// </summary>
        private SoundGameObjectPool m_SoundGameObjectPool;

        /// <summary>
        /// SoundDef that is being processed for this Emitter.
        /// </summary>
        private SoundDef m_SoundDef;

        /// <summary>
        /// Current repeat count (1 > SoundDef RandomRepeat(min/max)) 0 will play once (as will 1).
        /// </summary>
        private int m_RepeatCount;

        private Transform m_ListenerTransform;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new SoundEmitter with the specified SoundGameObjectPool.
        /// </summary>
        /// <param name="soundGameObjectPool">Pool of SoundGameObjects that can be allocated for this emitter.</param>
        public SoundEmitter(SoundGameObjectPool soundGameObjectPool)
        {
            Init(soundGameObjectPool);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the SoundEmitter with default values and sets up the pool reference.
        /// </summary>
        /// <param name="soundGameObjectPool">The pool of SoundGameObjects to use for audio playback.</param>
        public void Init(SoundGameObjectPool soundGameObjectPool)
        {
            allocated = false;
            active = false;

            ActiveSoundGameObjects = new List<SoundGameObject>();
            volume = 1.0f;
            sequentialClipIndex = 0;
            userClipIndex = 0;
            m_RepeatCount = 0;
            reserved = ReservedInfo.FreeAfterPlaybackCompletes;
            m_SoundGameObjectPool = soundGameObjectPool;
            SoundDefOverrideInfo = new SoundDefOverrideData();
        }

        /// <summary>
        /// Copies override data from a SoundDef to this emitter's override settings.
        /// </summary>
        /// <param name="soundDef">The SoundDef to copy values from.</param>
        public void CopyOverrideDataFromSoundDef(SoundDef soundDef)
        {
            SoundDefOverrideInfo.BasePitchInCents = soundDef.basePitchInCents;
            SoundDefOverrideInfo.VolumeScale = soundDef.volumeScale;
            SoundDefOverrideInfo.BaseLowPassCutoff = soundDef.baseLowPassCutoff;
        }

        /// <summary>
        /// Unreserves this emitter and stops all associated SoundGameObjects.
        /// </summary>
        public void ForceStop()
        {
            reserved = ReservedInfo.FreeAfterPlaybackCompletes;
            // Stop all SoundGameObjects and stops emitter
            Release();
        }

        /// <summary>
        /// Releases the SoundEmitter, stopping all audio and freeing it for reuse unless reserved.
        /// </summary>
        public void Release()
        {
            // Stop emitter and potentially stop SoundGameObjects if not reserved
            Stop();
            // Free emitter unless reserved
            if (reserved == ReservedInfo.FreeAfterPlaybackCompletes)
            {
                allocated = false;
            }
        }

        /// <summary>
        /// Stops all audio playback. SoundGameObjects are released unless reserved.
        /// </summary>
        public void Stop()
        {
            if (reserved != ReservedInfo.ReservedEmitterAndAudioSources)
            {
                // Release SoundGameObjects, allowing other emitters to use them
                ReleaseSoundGameObjects();
            }
            else
            {
                // SoundGameObjects are reserved for this Emitter, so just stop the AudioSource but keep
                foreach (SoundGameObject soundGameObject in ActiveSoundGameObjects)
                {
                    soundGameObject.StopAudioSource();
                }
            }

            active = false;
        }

        /// <summary>
        /// Sets the volume for this emitter.
        /// </summary>
        /// <param name="targetVolume">The target volume level.</param>
        public void SetVolume(float targetVolume)
        {
            this.volume = targetVolume;
        }

        /// <summary>
        /// Gets the SoundDef for this emitter.
        /// </summary>
        /// <returns>The SoundDef for this emitter</returns>
        public SoundDef GetSoundDef()
        {
            return m_SoundDef;
        }

        /// <summary>
        /// Starts playing audio through all active SoundGameObjects.
        /// </summary>
        /// <param name="listenerTransform">The audio listener's transform for spatial calculations.</param>
        /// <param name="mixerGroups">Array of AudioMixerGroups for routing audio output.</param>
        public void Play(Transform listenerTransform, AudioMixerGroup[] mixerGroups)
        {
            if (m_SoundDef == null || m_SoundDef.clips == null || m_SoundDef.clips.Count == 0)
            {
                return;
            }

            m_ListenerTransform = listenerTransform;

            float spatialBlend;
#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
            {
                // Disable SpatialBlend if playing in the inspector window to ensure audio is playing from all speakers
                spatialBlend = 0;
            }
            else
#endif
            {
                // Playing in editor, so use the SoundDef.SpatialBlend
                spatialBlend = m_SoundDef.distanceInfo.spatialBlend;
            }

            foreach (SoundGameObject soundGameObject in ActiveSoundGameObjects)
            {
                soundGameObject.TriggerAudioSource(m_ListenerTransform, m_SoundDef, this, spatialBlend, mixerGroups);
            }
        }

        /// <summary>
        /// Updates the emitter state, handling fade-out and repeats.
        /// </summary>
        /// <param name="mixerGroups">Array of AudioMixerGroups for audio routing.</param>
        /// <returns>True if the emitter is still active, false if it should be removed from the active list.</returns>
        public bool Update(AudioMixerGroup[] mixerGroups)
        {
            if (!active)
            {
                return false;
            }

            if (FadeOutTime.GetValue() == 0.0f)
            {
                // Emitter fadeout is complete. So stop and optionally release the emitter (unless reserved) and optionally stop all audio sources
                Release();
                return false;
            }

            int activeAudioSourceCounter = 0;
            foreach (SoundGameObject soundGameObject in ActiveSoundGameObjects)
            {
                bool fatal = false;
                soundGameObject.Update(this, m_RepeatCount, ref activeAudioSourceCounter, ref fatal);
                // AudioSource's GameObject has been destroyed (likely attached to a parent GameObject that has been destroyed). Stop all sounds and recover
                if (fatal)
                {
                    ValidateSoundGameObjects();
                    // Unreserve and stop this SoundEmitter
                    ForceStop();
                    return false;
                }
            }

            // An AudioSource is still playing?
            if (activeAudioSourceCounter > 0)
            {
                return true;
            }

            // All audio sources have finished playing for this emitter. Handle Repeat
            if (m_RepeatCount > 1)
            {
                m_RepeatCount--;
                // Play the emitter again
                Play(m_ListenerTransform, mixerGroups);
                return true;
            }

            // SoundEmitter has finished. Release SoundEmitter for reuse if not reserved.
            Release();
            return false;
        }

        /// <summary>
        /// Starts fading out the emitter over the specified duration.
        /// </summary>
        /// <param name="fadeOutTime">Duration of the fade-out in seconds. 0 for immediate stop.</param>
        public void FadeOut(float fadeOutTime)
        {
            if (fadeOutTime == 0.0f)
            {
                FadeOutTime.SetValue(0.0f);
            }
            else
            {
                FadeOutTime.SetValue(1.0f);
                FadeOutTime.MoveTo(0.0f, fadeOutTime);
            }
        }

        /// <summary>
        /// Sets the SoundDef for this emitter to play.
        /// </summary>
        /// <param name="soundDef">The SoundDef containing audio configuration.</param>
        /// <returns>True if the SoundDef was set successfully, false if null.</returns>
        public bool SetSoundDef(SoundDef soundDef)
        {
            if (soundDef == null)
            {
                return false;
            }

            m_SoundDef = soundDef;
            return true;
        }

        /// <summary>
        /// Activates the emitter and sets up repeat count from the SoundDef.
        /// </summary>
        public void Activate()
        {
            m_RepeatCount = Random.Range(m_SoundDef.repeatInfo.repeatMin, m_SoundDef.repeatInfo.repeatMax);
            active = true;
        }

        /// <summary>
        /// Gets the current repeat count for this emitter.
        /// </summary>
        /// <returns>The number of times this emitter will repeat playback.</returns>
        public int GetRepeatCount()
        {
            return m_RepeatCount;
        }

        /// <summary>
        /// Allocates SoundGameObjects if we've either got none already allocated and reserved.
        /// If we do have any here already (where a Play() has followed a Play()), release the existing and start again.
        /// </summary>
        /// <param name="parent">Parent transform for the audio sources.</param>
        /// <param name="position">Position for audio placement.</param>
        /// <returns>True if SoundGameObjects were successfully prepared, false otherwise.</returns>
        public bool PrepareSoundGameObjects(Transform parent, Vector3 position)
        {
            // No SoundGameObjects yet allocated.
            if (ActiveSoundGameObjects.Count == 0 || reserved != ReservedInfo.ReservedEmitterAndAudioSources)
            {
                ReleaseSoundGameObjects();
                // Create n SoundGameObjects (AudioSources)
                if (AllocateSoundGameObjects(m_SoundGameObjectPool, m_SoundDef.playCount) == false)
                {
                    // Failed to create n SoundGameObjects
                    return false;
                }
            }

            // Fatal. SoundGameObject has been destroyed by a parent. Clean up and unreserve Emitter
            if (ValidateSoundGameObjects() == false)
            {
                ReleaseSoundGameObjects();
                ForceStop();
                return false;
            }

            // ActiveSoundGameObjects may still have reserved SoundGameObjects, if Reserved = ReservedInfo.ReserveEmitterAndAudioSources
            foreach (var soundGameObject in ActiveSoundGameObjects)
            {
                soundGameObject.Enable(this);
                if (parent != null)
                {
                    // Position is used as Transform.LocalPosition. So, an offset from the parent.
                    soundGameObject.SetParent(parent, position);
                }
                else
                {
                    soundGameObject.SetPosition(position);
                }
            }

            return true;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Checks if the SoundGameObjects still exist.
        /// If they have been attached to another GameObject, and their Parent GameObject has been destroyed, we need to create another SoundGameObject.
        /// </summary>
        /// <returns>True if all SoundGameObjects are valid, false if any needed to be replaced.</returns>
        private bool ValidateSoundGameObjects()
        {
            bool isValid = true;
            for (int i = 0; i < ActiveSoundGameObjects.Count; i++)
            {
                SoundGameObject soundGameObject = ActiveSoundGameObjects[i];
                if (!soundGameObject.IsValid())
                {
                    // Create another SoundGameObject so that there's the same number in the pool
                    SoundGameObject newSoundGameObject = m_SoundGameObjectPool.Create();
                    // Replace the old with the new in the pool
                    m_SoundGameObjectPool.Replace(soundGameObject, newSoundGameObject);
                    ActiveSoundGameObjects[i] = newSoundGameObject;
                    isValid = false;
                }
            }

            return isValid;
        }

        /// <summary>
        /// Releases all active SoundGameObjects and returns them to the pool unless they are reserved.
        /// </summary>
        private void ReleaseSoundGameObjects()
        {
            if (reserved != ReservedInfo.ReservedEmitterAndAudioSources)
            {
                foreach (SoundGameObject soundGameObject in ActiveSoundGameObjects)
                {
                    // Releasing a SoundGameObject moves it back under the SoundGameObjectPool parent GameObject
                    soundGameObject.Release(this);
                    // Link soundGameObject back into the SoundObjectPool linked list (it becomes the next to be selected)
                    UpdateSoundObjectPoolLinkedList(soundGameObject);
                }

                ActiveSoundGameObjects.Clear();
            }
        }

        /// <summary>
        /// Updates the linked list structure of the SoundGameObjectPool when returning a SoundGameObject.
        /// </summary>
        /// <param name="newSoundGameObject">The SoundGameObject to relink into the pool.</param>
        private void UpdateSoundObjectPoolLinkedList(SoundGameObject newSoundGameObject)
        {
            SoundGameObject nextSoundGameObject = m_SoundGameObjectPool.NextSoundGameObject;
            // Make this the next available SoundGameObject in the pool
            m_SoundGameObjectPool.NextSoundGameObject = newSoundGameObject;
            // Link the previously next SoundGameObject to follow be selected after newSoundGameObject
            newSoundGameObject.NextAvailable = nextSoundGameObject;
            m_SoundGameObjectPool.AvailableSoundObjectCount++;
        }

        /// <summary>
        /// Allocates the specified number of SoundGameObjects from the pool for this emitter.
        /// </summary>
        /// <param name="soundGameObjectPool">The pool to allocate from.</param>
        /// <param name="count">Number of SoundGameObjects to allocate.</param>
        /// <returns>True if allocation was successful, false if insufficient objects available.</returns>
        private bool AllocateSoundGameObjects(SoundGameObjectPool soundGameObjectPool, int count)
        {
            // Are there enough free SoundGameObjects available to use for this emitter?
            if (soundGameObjectPool.AvailableSoundObjectCount < count)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                SoundGameObject soundGameObject = soundGameObjectPool.NextSoundGameObject;
                soundGameObjectPool.NextSoundGameObject = soundGameObject.NextAvailable;

                soundGameObject.SetActive(true);
                soundGameObjectPool.AvailableSoundObjectCount--;
                ActiveSoundGameObjects.Add(soundGameObject);
            }

            return true;
        }

        #endregion
    }
}
