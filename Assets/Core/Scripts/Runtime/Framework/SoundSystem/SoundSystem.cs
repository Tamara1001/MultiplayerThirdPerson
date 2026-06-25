using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Manages the entire audio system, including SoundEmitter pools, playback requests, and mixer management.
    /// The SoundSystem coordinates between SoundEmitters, SoundGameObjectPools, and the Unity AudioMixer
    /// to provide a comprehensive audio solution. It handles both one-off "fire and forget" sounds
    /// and reserved emitters for stateful playback scenarios.
    /// </summary>
    public class SoundSystem
    {
        #region Nested Classes

        /// <summary>
        /// Contains all information needed to play a sound, including the SoundDef, positioning,
        /// override parameters, and the resulting SoundHandle for tracking playback.
        /// </summary>
        public class SoundInfo
        {
            public SoundDef SoundDefinition;
            public Transform SoundTransform;
            /// <summary>
            /// Information is optionally copied from the SoundDef, allowing the user to tweak the values prior to playing.
            /// </summary>
            public SoundEmitter.SoundDefOverrideData SoundDefOverrideInfo;
            public SoundEmitter.ReservedInfo Reserved;
            public Vector3 Position;
            public SoundHandle Handle;

            /// <summary>
            /// Initializes a new SoundInfo with default values.
            /// </summary>
            public SoundInfo()
            {
                SoundDefinition = null;
                SoundTransform = null;
                SoundDefOverrideInfo = null;
                Reserved = SoundEmitter.ReservedInfo.FreeAfterPlaybackCompletes;
                Position = Vector3.zero;
            }
        }

        #endregion

        #region Fields & Properties

        private SoundGameObjectPool m_SoundGameObjects;
        /// <summary>
        /// Contains the sound emitters that can be (or are) allocated.
        /// </summary>
        private List<SoundEmitter> m_SoundEmitterPool;
        /// <summary>
        /// Contains the sound emitters that are actually active (playing or reserved).
        /// </summary>
        private List<SoundEmitter> m_SoundEmitterActiveList;

        private int m_SequenceId;
        private readonly Interpolator m_MasterVolume = new Interpolator(1.0f, Interpolator.CurveType.SmoothStep);
        private SoundMixer m_SoundMixer;
        private Transform m_ListenerTransform;
        private Dictionary<SoundDef, SoundInfo> m_ReservedSoundInfo;

        #endregion

        #region Public Methods

        /// <summary>
        /// Create pool of emitters.
        /// An SoundEmitter is required to be allocated by the user for playback of a SoundDef.
        /// A SoundEmitter then triggers n SoundGameObjects (each containing an AudioSource) using the settings within the SoundDef class.
        /// Emitters can be reserved so that their internal information (clip index if playing sequentially through a list, for example) is maintained.
        /// If not reserved, the emitter will be freed when all of its child SoundGameObjects have stopped, allowing it to be reallocated by another request.
        /// </summary>
        /// <param name="listenerTransform">The audio listener's transform for spatial calculations.</param>
        /// <param name="maxSoundEmitters">Maximum number of SoundEmitters to create in the pool.</param>
        /// <param name="soundGameObjectPool">The pool of SoundGameObjects to use.</param>
        /// <param name="mixer">The Unity AudioMixer for audio routing.</param>
        public void Init(Transform listenerTransform, int maxSoundEmitters, SoundGameObjectPool soundGameObjectPool, AudioMixer mixer)
        {
            m_SoundGameObjects = soundGameObjectPool;

            m_ListenerTransform = listenerTransform;
            // Initialise mixer groups
            m_SoundMixer = new SoundMixer(mixer);
            m_SequenceId = 0;
            m_ReservedSoundInfo = new Dictionary<SoundDef, SoundInfo>();

            m_SoundEmitterPool = new List<SoundEmitter>();
            for (var i = 0; i < maxSoundEmitters; i++)
            {
                var emitter = CreateSoundEmitterForPool(m_SoundGameObjects);
                m_SoundEmitterPool.Add(emitter);
            }

            // This holds the list of currently active emitters
            m_SoundEmitterActiveList = new List<SoundEmitter>();
        }

        /// <summary>
        /// Stops the SoundGameObjects within a SoundEmitter, optionally fading out their volume over n seconds.
        /// If fadeOutTime = 0, optionally release the SoundEmitter so that it is available to be allocated again.
        /// The SoundEmitter is not freed if it has been reserved. This allows for the SoundDef to be played again, with clip selection information preserved.
        /// </summary>
        /// <param name="soundInfo">The SoundInfo containing the SoundHandle to stop.</param>
        /// <param name="fadeOutTime">Duration of fade-out in seconds. 0 for immediate stop.</param>
        /// <returns>True if the stop was successful, false if the handle was invalid.</returns>
        public bool Stop(SoundInfo soundInfo, float fadeOutTime)
        {
            if (soundInfo == null || soundInfo.Handle == null || !soundInfo.Handle.IsValid())
            {
                return false;
            }

            SoundHandle soundHandle = soundInfo.Handle;
            if (fadeOutTime == 0)
            {
                soundHandle.Emitter.Release();
            }
            else
            {
                soundHandle.Emitter.FadeOut(fadeOutTime);
            }

            return true;
        }

        /// <summary>
        /// Stops any SoundEmitter that is playing the requested SoundDef and release its SoundGameObjects back to the allocation pool
        /// </summary>
        /// <param name="soundDef">The Sound Definition to stop.</param>
        /// <returns>True if the stop was successful, false if the handle was invalid or if no matching SoundDefs were found.</returns>
        public bool Stop(SoundDef soundDef)
        {
            bool stoppedEmitter = false;
            for (int i=m_SoundEmitterActiveList.Count-1; i>=0; i--)
            {
                if (m_SoundEmitterActiveList[i].GetSoundDef() == soundDef)
                {
                    m_SoundEmitterActiveList[i].ForceStop();    // Stop emitter and release all SoundGameObjects
                    m_SoundEmitterActiveList.RemoveAt(i);
                    stoppedEmitter = true;
                }
            }
            return stoppedEmitter;
        }

        /// <summary>
        /// Stops everything instantly.
        /// Releases all SoundEmitters and their SoundGameObjects and adds them back to the allocation pool.
        /// </summary>
        public void StopAll()
        {
            foreach (var soundEmitter in m_SoundEmitterActiveList)
            {
                // Release all emitters and SoundGameObjects
                soundEmitter.ForceStop();
            }

            m_SoundEmitterActiveList.Clear();
        }

        /// <summary>
        /// Stops a SoundEmitter immediately and removes it from the active list.
        /// </summary>
        /// <param name="soundInfo">The SoundInfo containing the SoundHandle to stop.</param>
        /// <returns>True if the stop was successful, false if the handle was invalid.</returns>
        public bool Stop(SoundInfo soundInfo)
        {
            if (soundInfo == null || soundInfo.Handle == null || !soundInfo.Handle.IsValid())
            {
                return false;
            }

            SoundHandle soundHandle = soundInfo.Handle;
            soundHandle.Emitter.ForceStop();
            for (int i = 0; i < m_SoundEmitterActiveList.Count; i++)
            {
                if (m_SoundEmitterActiveList[i] == soundHandle.Emitter)
                {
                    m_SoundEmitterActiveList.RemoveAt(i);
                    break;
                }
            }

            return true;
        }

        /// <summary>
        /// Updates the entire sound system, processing active emitters and updating mixer volumes.
        /// </summary>
        /// <param name="muteSound">If true, all audio will be muted.</param>
        public void UpdateSoundSystem(bool muteSound = false)
        {
            if (m_SoundEmitterActiveList.Count == 0)
            {
                // Nothing playing
                return;
            }

            float masterVolume = (muteSound ? 0 : m_MasterVolume.GetValue());
            m_SoundMixer.Update(masterVolume);
            UpdateActiveSoundEmitters();
        }

        /// <summary>
        /// Plays a sound using a pre-configured SoundInfo object. This serves as the endpoint for the SoundRequestBuilder.
        /// </summary>
        /// <param name="soundInfo">The fully configured sound request.</param>
        /// <param name="volume">The volume to play the sound at (defaults to 1.0f).</param>
        /// <returns>The same SoundInfo object, now containing a valid SoundHandle if playback was successful.</returns>
        public SoundInfo Play(SoundInfo soundInfo, float volume = 1.0f)
        {
            if (soundInfo.SoundDefinition == null)
            {
                return null;
            }

            if (soundInfo.Reserved == SoundEmitter.ReservedInfo.FreeAfterPlaybackCompletes)
            {
                return AllocateAndPlayEmitter(soundInfo, volume);
            }
            else
            {
                if (!m_ReservedSoundInfo.TryGetValue(soundInfo.SoundDefinition, out SoundInfo reservedSoundInfo))
                {
                    reservedSoundInfo = new SoundInfo() { SoundDefinition = soundInfo.SoundDefinition, Reserved = soundInfo.Reserved };
                    m_ReservedSoundInfo[soundInfo.SoundDefinition] = reservedSoundInfo;
                }

                // Update the reserved info with the latest request details
                reservedSoundInfo.Position = soundInfo.Position;
                reservedSoundInfo.SoundTransform = soundInfo.SoundTransform;
                reservedSoundInfo.SoundDefOverrideInfo = soundInfo.SoundDefOverrideInfo;

                PlayEmitter(reservedSoundInfo, volume);
                return reservedSoundInfo;
            }
        }

        /// <summary>
        /// Handles creating and Playing a SoundEmitter, requiring the SoundDef, parent Transform and optional Volume parameters.
        /// A new SoundEmitter is always created when using this method.
        /// It is therefore useful for playing once-off "fire and forget" SFX, where no other setup is required prior to calling.
        /// The SoundInfo class is returned, so that performing other tasks (SetVolume, Stop or Release) can be performed if necessary.
        /// </summary>
        /// <param name="soundDef">The SoundDef to play.</param>
        /// <param name="transform">The transform to attach the sound to.</param>
        /// <param name="volume">The volume level (0-1).</param>
        /// <returns>SoundInfo containing the handle for further control.</returns>
        public SoundInfo Play(SoundDef soundDef, Transform transform, float volume = 1)
        {
            SoundInfo soundInfo = new SoundInfo() { SoundDefinition = soundDef, SoundTransform = transform, Position = Vector3.zero, };
            return AllocateAndPlayEmitter(soundInfo, volume);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Handles allocating a SoundEmitter (if necessary) and playing it, using the information from within the SoundInfo class.
        /// If soundInfo.SoundHandle is already allocated and valid, the same handle will continue to be used.
        /// Passing the same soundInfo to subsequent PlayEmitter calls allows for tracking of the Random_But_Not_Last and Sequential AudioClip selection modes.
        /// Passing the optional normalized volume value will affect the overall volume of the SoundEmitter.
        /// </summary>
        /// <param name="soundInfo">The SoundInfo containing playback configuration.</param>
        /// <param name="volume">The volume level (0-1).</param>
        /// <returns>True if playback was successful, false otherwise.</returns>
        private bool PlayEmitter(SoundInfo soundInfo, float volume = 1)
        {
            return AllocateAndPlayEmitter(soundInfo, volume) != null;
        }

        /// <summary>
        /// Creates a new SoundEmitter for the pool with default settings.
        /// </summary>
        /// <param name="soundGameObjectPool">The pool of SoundGameObjects to associate with the emitter.</param>
        /// <returns>A new SoundEmitter instance.</returns>
        private SoundEmitter CreateSoundEmitterForPool(SoundGameObjectPool soundGameObjectPool)
        {
            var emitter = new SoundEmitter(soundGameObjectPool);
            emitter.FadeOutTime = new Interpolator(1.0f, Interpolator.CurveType.Linear);
            return emitter;
        }

        /// <summary>
        /// Gets an available SoundEmitter from the pool or creates a new one if none are available.
        /// </summary>
        /// <param name="soundGameObjectPool">The pool of SoundGameObjects to associate with the emitter.</param>
        /// <returns>An allocated SoundEmitter ready for use.</returns>
        private SoundEmitter GetSoundEmitterFromPool(SoundGameObjectPool soundGameObjectPool)
        {
            foreach (var emitter in m_SoundEmitterPool)
            {
                if (!emitter.allocated)
                {
                    emitter.seqId = m_SequenceId++;
                    emitter.Init(soundGameObjectPool);
                    emitter.allocated = true;
                    return emitter;
                }
            }

            // No available SoundEmitters. Create another and add it to the pool
            SoundEmitter soundEmitter = CreateSoundEmitterForPool(soundGameObjectPool);
            soundEmitter.allocated = true;
            m_SoundEmitterPool.Add(soundEmitter);
            return soundEmitter;
        }

        /// <summary>
        /// Allocates a SoundEmitter and plays it using the provided SoundInfo configuration.
        /// </summary>
        /// <param name="soundInfo">The SoundInfo containing all playback parameters.</param>
        /// <param name="volume">The volume level (0-1).</param>
        /// <returns>The updated SoundInfo with a valid SoundHandle, or null if playback failed.</returns>
        private SoundInfo AllocateAndPlayEmitter(SoundInfo soundInfo, float volume)
        {
            if (soundInfo == null || soundInfo.SoundDefinition == null)
            {
                return null;
            }

            if (soundInfo.Handle == null || soundInfo.Handle.Emitter.allocated == false)
            {
                // Allocates a SoundEmitter and set the soundDef and Reserve information
                soundInfo.Handle = AllocateSoundEmitter(soundInfo);
            }

            SoundHandle soundHandle = soundInfo.Handle;
            if (!soundHandle.IsValid())
            {
                Debug.Log("Invalid Handle");
                return null;
            }

            SoundEmitter soundEmitter = soundHandle.Emitter;

            if (soundInfo.SoundDefOverrideInfo != null)
            {
                soundEmitter.SoundDefOverrideInfo = soundInfo.SoundDefOverrideInfo;
            }
            else
            {
                soundEmitter.CopyOverrideDataFromSoundDef(soundInfo.SoundDefinition);
            }

            if (Play(soundEmitter, soundInfo.Position, soundInfo.SoundTransform))
            {
                soundEmitter.SetVolume(volume);
            }

            return soundInfo;
        }

        /// <summary>
        /// Allocates a SoundEmitter from the pool and configures it with the provided SoundInfo.
        /// </summary>
        /// <param name="soundInfo">The SoundInfo containing configuration parameters.</param>
        /// <returns>A new SoundHandle for the allocated emitter.</returns>
        private SoundHandle AllocateSoundEmitter(SoundInfo soundInfo)
        {
            // Get an unused emitter
            SoundEmitter newEmitter = GetSoundEmitterFromPool(m_SoundGameObjects);
            // Reserved info is only stored on allocation, to ensure that reserved Emitters always use the same info
            newEmitter.reserved = soundInfo.Reserved;
            // SoundDef info is only stored on allocation to ensure that reserved Emitters always use the same info
            newEmitter.SetSoundDef(soundInfo.SoundDefinition);
            return new SoundHandle(newEmitter);
        }

        /// <summary>
        /// Starts playback of a SoundEmitter with the specified position and optional parent transform.
        /// </summary>
        /// <param name="soundEmitter">The SoundEmitter to play.</param>
        /// <param name="position">The world position or local offset for audio placement.</param>
        /// <param name="parent">Optional parent transform to attach the audio to.</param>
        /// <returns>True if playback started successfully, false otherwise.</returns>
        private bool Play(SoundEmitter soundEmitter, Vector3 position, Transform parent = null)
        {
            if (soundEmitter == null || soundEmitter.allocated == false || soundEmitter.active && soundEmitter.reserved == SoundEmitter.ReservedInfo.FreeAfterPlaybackCompletes)
            {
                return false;
            }

            soundEmitter.Activate();
            // Allocate SoundGameObjects if not reserved and set their position or transform
            if (soundEmitter.PrepareSoundGameObjects(parent, position) == false)
            {
                // Failed to obtain SoundGameObjects.
                return false;
            }

            m_SoundEmitterActiveList.Add(soundEmitter);
            soundEmitter.Play(m_ListenerTransform, m_SoundMixer.MixerGroups);
            return true;
        }

        /// <summary>
        /// Updates all active SoundEmitters and removes those that have finished playing.
        /// </summary>
        private void UpdateActiveSoundEmitters()
        {
            for (int i = 0; i < m_SoundEmitterActiveList.Count; i++)
            {
                if (m_SoundEmitterActiveList[i].Update(m_SoundMixer.MixerGroups) == false)
                {
                    // Emitter is no longer active. Remove it from active list.
                    m_SoundEmitterActiveList.RemoveAt(i);
                    i--;
                }
            }
        }

        #endregion
    }
}
