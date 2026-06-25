using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Random = UnityEngine.Random;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Represents a single audio source with associated audio filter components that can be controlled by a SoundEmitter.
    /// Each SoundGameObject wraps an AudioSource and its related components (filters, effects) to provide
    /// advanced audio playback features including distance-based filtering, spatial audio, and dynamic parameter adjustment.
    /// SoundGameObjects are pooled and can be allocated to different SoundEmitters as needed.
    /// </summary>
    public class SoundGameObject
    {
        #region Enums

        /// <summary>
        /// Defines how the SoundGameObject's position is determined.
        /// </summary>
        private enum PositionType
        {
            /// <summary>
            /// Uses a fixed world position.
            /// </summary>
            Position,
            /// <summary>
            /// Follows a parent transform.
            /// </summary>
            ParentTransform
        }

        #endregion

        #region Nested Classes

        /// <summary>
        /// Contains references to all audio components attached to a SoundGameObject.
        /// This structure groups related audio components for easier management and initialization.
        /// </summary>
        public class AudioComponentData
        {
            public AudioSource ASource;
            public AudioLowPassFilter LowPassFilter;
            public AudioHighPassFilter HighPassFilter;
            public AudioDistortionFilter DistortionFilter;
        }

        #endregion

        #region Fields & Properties

        /// <summary>
        /// Linked list. Points to next available SoundGameObject (null = none available).
        /// </summary>
        public SoundGameObject NextAvailable;

        // Constants
        private const float k_SoundVolCutoff = -60.0f;

        // Audio components
        private AudioSource m_AudioSource;
        private AudioLowPassFilter m_LowPassFilter;
        private AudioHighPassFilter m_HighPassFilter;
        private AudioDistortionFilter m_DistortionFilter;

        /// <summary>
        /// Emitter that is controlling this SoundObject.
        /// </summary>
        private SoundEmitter m_Emitter;

        // State tracking
        private bool m_Active;
        private int m_CurrentLoopCount;
        private int m_LastTimeSamples;
        private float m_Volume;
        private GameObject m_SoundGameObjectPool;
        private GameObject m_Parent;
        private PositionType m_PositionType;

        // Audio processing
        private SoundDef m_SoundDef;
        private Transform m_ListenerTransform;
        private Interpolator m_Curve;
        private bool m_CalculateDistance;
        private float m_LpfCurveNormalizedResult;
        private float m_HpfCurveNormalizedResult;
        private float m_SpatialCurveNormalizedResult;
        private float m_LpfRandomValue;
        private float m_HpfRandomValue;
        private float m_SpatialBlendValue;

#if UNITY_EDITOR
        private static int s_Counter = 0;
        private readonly string m_DebugName;
#endif

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new SoundGameObject with the specified parent and audio component data.
        /// </summary>
        /// <param name="parent">The parent GameObject for organizational purposes.</param>
        /// <param name="audioComponentData">Container with references to all required audio components.</param>
        public SoundGameObject(GameObject parent, AudioComponentData audioComponentData)
        {
#if UNITY_EDITOR
            m_DebugName = audioComponentData.ASource.gameObject.name;
#endif
            InitAudioComponents(parent, audioComponentData);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// To save on CPU processing, we only enable the LowpassFilter, HighpassFilter and Distortion AudioEffect components if required by the SoundDef.
        /// We only process the AudioListener to AudioSource distances if any of these are enabled.
        /// </summary>
        /// <param name="soundDef">The SoundDef to check for required filter components.</param>
        /// <returns>True if distance calculation is needed, false otherwise.</returns>
        public bool EnableAudioFilterComponents(SoundDef soundDef)
        {
            bool calcDistance = false;

            if (m_LowPassFilter != null)
            {
                m_LowPassFilter.enabled = soundDef.lowPassFilter.enableComponent;
                if (m_LowPassFilter.enabled)
                {
                    calcDistance = true;
                }
            }

            if (m_HighPassFilter != null)
            {
                m_HighPassFilter.enabled = soundDef.highPassFilter.enableComponent;
                if (m_HighPassFilter.enabled)
                {
                    calcDistance = true;
                }
            }

            if (m_DistortionFilter)
            {
                m_DistortionFilter.enabled = soundDef.distortionFilter.enableComponent;
            }

#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
            {
                // No spatial blending when playing from the Unity editors inspector window
                return calcDistance;
            }
#endif

            if (soundDef.distanceInfo.spatialBlendCurveType != Interpolator.CurveType.None && soundDef.distanceInfo.spatialBlendMaxDistance != 0)
            {
                calcDistance = true;
            }

            return calcDistance;
        }

        /// <summary>
        /// Enables this SoundGameObject and assigns it to a SoundEmitter.
        /// </summary>
        /// <param name="soundEmitter">The SoundEmitter that will control this SoundGameObject.</param>
        public void Enable(SoundEmitter soundEmitter)
        {
            m_Active = true;
            m_Emitter = soundEmitter;
            m_AudioSource.transform.position = Vector3.zero;
        }

        /// <summary> Releases this SoundGameObject if it belongs to the specified emitter.
        /// </summary>
        /// <param name="emitter">The emitter requesting to release this SoundGameObject.</param>
        public void Release(SoundEmitter emitter)
        {
            if (m_Emitter == emitter)
            {
                StopAudioSource();
                if (m_PositionType == PositionType.ParentTransform)
                {
                    m_AudioSource.transform.parent = m_SoundGameObjectPool.transform;
                }

                m_Active = false;
                m_Emitter = null;
            }
        }

        /// <summary>
        /// Stops the AudioSource playback.
        /// </summary>
        public void StopAudioSource()
        {
            if (m_AudioSource != null)
            {
                m_AudioSource.Stop();
            }
        }

        /// <summary>
        /// Checks if the AudioSource is currently playing.
        /// </summary>
        /// <returns>True if playing, false otherwise.</returns>
        public bool IsPlaying()
        {
            if (m_AudioSource == null)
            {
                return false;
            }

            return m_AudioSource.isPlaying;
        }

        /// <summary>
        /// Sets the active state of this SoundGameObject.
        /// </summary>
        /// <param name="activeFlag">True to activate, false to deactivate.</param>
        public void SetActive(bool activeFlag)
        {
            m_Active = activeFlag;
        }

        /// <summary>
        /// Updates the SoundGameObject state and handles looping and fade-out logic.
        /// </summary>
        /// <param name="soundEmitter">The controlling SoundEmitter.</param>
        /// <param name="repeatCount">Current repeat count from the emitter.</param>
        /// <param name="count">Reference to active audio source counter.</param>
        /// <param name="fatal">Reference to fatal error flag.</param>
        /// <returns>True if still active and playing, false otherwise.</returns>
        public bool Update(SoundEmitter soundEmitter, int repeatCount, ref int count, ref bool fatal)
        {
            fatal = false;
            if (m_Active == false)
            {
                return false;
            }

            // GameObject has been destroyed.
            if (m_AudioSource == null)
            {
                fatal = true;
                return false;
            }

            // AudioSource is still playing? Handle fade out if stopping and update looping audio clip counter.
            if (m_AudioSource.isPlaying)
            {
                UpdateAudioParameters(soundEmitter);
                CheckForAudioSourceLoop();
                count++;
                return true;
            }

            // Finished playing all AudioSources. We can free the SoundGameObject if it's not reserved, and we've no more SoundEmitter repeats to do
            if (soundEmitter.reserved != SoundEmitter.ReservedInfo.ReservedEmitterAndAudioSources)
            {
                if (repeatCount < 2)
                {
                    // Not reserved. Allow another emitter to use this SoundGameObject
                    Release(soundEmitter);
                }
            }

            // No longer playing
            return false;
        }

        /// <summary>
        /// Checks if the AudioSource has looped and updates the loop counter accordingly.
        /// </summary>
        /// <returns>True if a loop occurred this frame, false otherwise.</returns>
        public bool CheckForAudioSourceLoop()
        {
            bool looped = false;
            // Have we looped? If so, decrease loop counter
            if (m_CurrentLoopCount > 1 && m_AudioSource.timeSamples < m_LastTimeSamples)
            {
                m_CurrentLoopCount--;
                looped = true;
                if (m_CurrentLoopCount == 1)
                {
                    m_AudioSource.loop = false;
                }
            }

            m_LastTimeSamples = m_AudioSource.timeSamples;
            return looped;
        }

        /// <summary>
        /// Checks if this SoundGameObject is still valid (its AudioSource hasn't been destroyed).
        /// </summary>
        /// <returns>True if valid, false if the AudioSource has been destroyed.</returns>
        public bool IsValid()
        {
            // SoundGameObject could have had its parent changed and its parent has since been destroyed.
            return (m_AudioSource != null);
        }

        /// <summary>
        /// Configures and starts the AudioSource with all parameters from the SoundDef and SoundEmitter.
        /// </summary>
        /// <param name="listenerTransform">The audio listener's transform for spatial calculations.</param>
        /// <param name="soundDef">The SoundDef containing audio configuration.</param>
        /// <param name="soundEmitter">The controlling SoundEmitter.</param>
        /// <param name="spatialBlend">The spatial blend value to use.</param>
        /// <param name="mixerGroups">Array of AudioMixerGroups for audio routing.</param>
        public void TriggerAudioSource(Transform listenerTransform, SoundDef soundDef, SoundEmitter soundEmitter, float spatialBlend, AudioMixerGroup[] mixerGroups)
        {
            m_SoundDef = soundDef;
            SoundEmitter.SoundDefOverrideData soundDefOverrideData = soundEmitter.SoundDefOverrideInfo;

            m_CalculateDistance = InitDistanceCurves(soundDef);
            if (m_CalculateDistance)
            {
                m_ListenerTransform = listenerTransform;
            }

            // Get clip from list based on index type (random, user, sequential..)
            uint clipIndex = GetClipIndex(soundDef, soundEmitter);
            m_AudioSource.clip = soundDef.clips[(int)clipIndex];

            // Map from cent (100 = 1 semitone) space to linear playback multiplier
            float cents = soundDefOverrideData.BasePitchInCents + (Random.Range(soundDef.pitchAndVolumeInfo.pitchMin, soundDef.pitchAndVolumeInfo.pitchMax));
            m_AudioSource.pitch = Mathf.Pow(2.0f, cents / 1200.0f);

            // Set distance min / max attenuation
            m_AudioSource.minDistance = soundDef.distanceInfo.volumeDistMin;
            m_AudioSource.maxDistance = soundDef.distanceInfo.volumeDistMax;
            m_AudioSource.rolloffMode = soundDef.distanceInfo.volumeRolloffMode;

#if UNITY_EDITOR
            m_AudioSource.gameObject.name = m_DebugName + " (" + soundDef.name + " " + s_Counter + ")";
            s_Counter++;
#endif

            // Set volume using SoundGameObject random value, and soundDef and SoundEmitter volume scale parameters
            float vMin = AmplitudeFromDecibel(soundDef.pitchAndVolumeInfo.volumeMin);
            float vMax = AmplitudeFromDecibel(soundDef.pitchAndVolumeInfo.volumeMax);
            m_Volume = Random.Range(vMin, vMax);

            // Set loop on / off (Update code will count down the number of loops required and stop when necessary)
            // True if loop count!=1 (0 = infinite. 1 = play once. 2 = play twice...)
            m_AudioSource.loop = soundDef.repeatInfo.loopCount != 1;
            m_CurrentLoopCount = soundDef.repeatInfo.loopCount;
            m_LastTimeSamples = 0;

            // Set mixer group output
            if (mixerGroups != null)
            {
                m_AudioSource.outputAudioMixerGroup = mixerGroups[(int)soundDef.mixerGroup];
            }

            // Set start sample offset position
            float startPercentOffset = Random.Range(soundDef.startStopInfo.startOffsetPercentMin, soundDef.startStopInfo.startOffsetPercentMax) / 100.0f;
            m_AudioSource.timeSamples = (int)(m_AudioSource.clip.samples * startPercentOffset);
            m_AudioSource.dopplerLevel = soundDef.distanceInfo.dopplerScale;

            m_LpfRandomValue = soundDefOverrideData.BaseLowPassCutoff + Random.Range(soundDef.lowPassFilter.cutoffMin, soundDef.lowPassFilter.cutoffMax);
            m_HpfRandomValue = Random.Range(soundDef.highPassFilter.cutoffMin, soundDef.highPassFilter.cutoffMax);
            m_SpatialBlendValue = spatialBlend;

            if (m_DistortionFilter != null)
            {
                m_DistortionFilter.distortionLevel = Random.Range(soundDef.distortionFilter.distortionMin, soundDef.distortionFilter.distortionMax);
            }

            // Set stereo pan.
            // This pan is applied before 3D panning calculations are considered.
            // In other words, stereo panning affects the left right balance of the sound before it is spatialised in 3D.
            m_AudioSource.panStereo = Random.Range(soundDef.distanceInfo.panMin, soundDef.distanceInfo.panMax);

            UpdateAudioParameters(soundEmitter);

            // Start sample with delay if required.
            // If we also have a stopDelay time (stop the audioSource after n seconds), use PlayScheduled and SetScheduledEndTime
            float delay = Random.Range(soundDef.startStopInfo.delayMin, soundDef.startStopInfo.delayMax);
            if (soundDef.startStopInfo.stopDelay > 0)
            {
                double startTime = AudioSettings.dspTime;
                startTime += delay;
                double endTime = startTime + soundDef.startStopInfo.stopDelay;
                m_AudioSource.PlayScheduled(startTime);
                m_AudioSource.SetScheduledEndTime(endTime);
            }
            else
            {
                if (delay > 0.0f)
                    m_AudioSource.PlayDelayed(delay);
                else
                    m_AudioSource.Play();
            }
        }

        /// <summary>
        /// Sets the SoundGameObject to a fixed world position.
        /// </summary>
        /// <param name="position">The world position to set.</param>
        public void SetPosition(Vector3 position)
        {
            m_AudioSource.transform.position = position;
            m_PositionType = PositionType.Position;
            m_Parent = m_SoundGameObjectPool;
        }

        /// <summary>
        /// Parents the SoundGameObject to a transform with a local position offset.
        /// </summary>
        /// <param name="parent">The parent transform to attach to.</param>
        /// <param name="localPosition">The local position offset from the parent.</param>
        public void SetParent(Transform parent, Vector3 localPosition)
        {
            m_AudioSource.transform.parent = parent;
            m_AudioSource.transform.localPosition = localPosition;
            m_Parent = parent.gameObject;
            m_PositionType = PositionType.ParentTransform;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Initializes audio component references and sets up the interpolator for distance curves.
        /// </summary>
        /// <param name="parent">The parent GameObject for organizational purposes.</param>
        /// <param name="audioComponentData">Container with references to all required audio components.</param>
        private void InitAudioComponents(GameObject parent, AudioComponentData audioComponentData)
        {
            m_SoundGameObjectPool = parent;
            m_Parent = parent;
            m_AudioSource = audioComponentData.ASource;
            m_LowPassFilter = audioComponentData.LowPassFilter;
            m_HighPassFilter = audioComponentData.HighPassFilter;
            m_DistortionFilter = audioComponentData.DistortionFilter;
            m_AudioSource.playOnAwake = false;
            m_Curve = new Interpolator();
        }

        /// <summary>
        /// Converts a decibel value to linear amplitude.
        /// </summary>
        /// <param name="decibel">The decibel value to convert.</param>
        /// <returns>The equivalent linear amplitude value.</returns>
        private float AmplitudeFromDecibel(float decibel)
        {
            if (decibel <= k_SoundVolCutoff)
            {
                return 0;
            }

            return Mathf.Pow(2.0f, decibel / 6.0f);
        }

        /// <summary>
        /// Gets the current world position of this SoundGameObject based on its position type.
        /// </summary>
        /// <returns>The current world position.</returns>
        private Vector3 GetPosition()
        {
            if (m_PositionType == PositionType.Position)
            {
                return m_AudioSource.transform.position;
            }
            else if (m_PositionType == PositionType.ParentTransform)
            {
                return m_AudioSource.transform.parent.transform.position;
            }

            return Vector3.zero;
        }

        /// <summary>
        /// Initializes distance curve calculations and determines if distance-based processing is needed.
        /// </summary>
        /// <param name="soundDef">The SoundDef to check for distance-based effects.</param>
        /// <returns>True if distance calculation is required, false otherwise.</returns>
        private bool InitDistanceCurves(SoundDef soundDef)
        {
            m_LpfCurveNormalizedResult = 1;
            m_HpfCurveNormalizedResult = 1;
            m_SpatialCurveNormalizedResult = 1;

#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
            {
                // No listener distance calculation required when testing in inspector window
                return false;
            }
#endif
            return EnableAudioFilterComponents(soundDef);
        }

        /// <summary>
        /// Updates distance-based curve calculations for filters and spatial blending.
        /// </summary>
        private void UpdateDistanceCurves()
        {
            if (!m_CalculateDistance)
            {
                return;
            }

            Vector3 position = GetPosition();
            Vector3 listenerPosition = m_ListenerTransform.position;
            float dist = Vector3.Distance(position, listenerPosition);

            SoundDef.Distance distanceInfo = m_SoundDef.distanceInfo;

            if (m_LowPassFilter != null && m_LowPassFilter.enabled)
            {
                float v = Mathf.Clamp(dist, 0, distanceInfo.lpfMaxDistance);
                m_LpfCurveNormalizedResult = 1.0f - m_Curve.GetNormalizedCurveValue(distanceInfo.lpfRollOffCurveType, v / distanceInfo.lpfMaxDistance);
            }

            if (m_HighPassFilter != null && m_HighPassFilter.enabled)
            {
                float v = Mathf.Clamp(dist, 0, distanceInfo.hpfMaxDistance);
                m_HpfCurveNormalizedResult = 1.0f - m_Curve.GetNormalizedCurveValue(distanceInfo.hpfRollOffCurveType, v / distanceInfo.hpfMaxDistance);
            }

            if (distanceInfo.spatialBlendMaxDistance > 0)
            {
                float v = Mathf.Clamp(dist, 0, distanceInfo.spatialBlendMaxDistance);
                m_SpatialCurveNormalizedResult = m_Curve.GetNormalizedCurveValue(distanceInfo.spatialBlendCurveType, v / distanceInfo.spatialBlendMaxDistance);
            }
        }

        /// <summary>
        /// Applies the calculated distance curve values to the audio filters and spatial blend.
        /// </summary>
        private void SetDistanceCurveAudio()
        {
            if (m_LowPassFilter != null && m_LowPassFilter.enabled)
            {
                float cutoff = m_LpfRandomValue * m_LpfCurveNormalizedResult;
                if (m_SoundDef.distanceInfo.lpfRollOffCurveType != Interpolator.CurveType.None && m_SoundDef.distanceInfo.lpfMinCutoff > 0 && cutoff < m_SoundDef.distanceInfo.lpfMinCutoff)
                {
                    cutoff = m_SoundDef.distanceInfo.lpfMinCutoff;
                }

                m_LowPassFilter.cutoffFrequency = cutoff;
            }

            if (m_HighPassFilter != null && m_HighPassFilter.enabled)
            {
                float cutoff = m_HpfRandomValue * m_HpfCurveNormalizedResult;
                if (m_SoundDef.distanceInfo.hpfRollOffCurveType != Interpolator.CurveType.None && m_SoundDef.distanceInfo.hpfMinCutoff > 0 && cutoff < m_SoundDef.distanceInfo.hpfMinCutoff)
                {
                    cutoff = m_SoundDef.distanceInfo.hpfMinCutoff;
                }

                m_HighPassFilter.cutoffFrequency = cutoff;
            }

#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
            {
                // Disable SpatialBlend if playing in the inspector window to ensure audio is playing from all speakers
                m_AudioSource.spatialBlend = 0;
            }
            else
#endif
            {
                m_AudioSource.spatialBlend = m_SpatialBlendValue * m_SpatialCurveNormalizedResult;
            }
        }

        /// <summary>
        /// Updates audio parameters including distance curves and volume.
        /// </summary>
        /// <param name="soundEmitter">The controlling SoundEmitter.</param>
        private void UpdateAudioParameters(SoundEmitter soundEmitter)
        {
            if (m_CalculateDistance)
            {
                UpdateDistanceCurves();
            }

            SetDistanceCurveAudio();

            m_AudioSource.volume = m_Volume * soundEmitter.volume * soundEmitter.SoundDefOverrideInfo.VolumeScale;
        }

        /// <summary>
        /// Determines which audio clip to play based on the SoundDef's playback type.
        /// </summary>
        /// <param name="soundDef">The SoundDef containing clip selection settings.</param>
        /// <param name="soundEmitter">The controlling SoundEmitter with selection state.</param>
        /// <returns>The index of the clip to play.</returns>
        private uint GetClipIndex(SoundDef soundDef, SoundEmitter soundEmitter)
        {
            uint clipIndex = 0;
            if (soundDef.playbackType == SoundDef.PlaybackTypes.User)
            {
                clipIndex = soundEmitter.userClipIndex;
                clipIndex %= (uint)soundDef.clips.Count;
            }
            else if (soundDef.playbackType == SoundDef.PlaybackTypes.RandomNotLast)
            {
                if (soundDef.clips.Count > 2)
                {
                    // More than two clips, so choose one but not the last one
                    clipIndex = GetRandomClipIndexButNotLast(soundEmitter, soundDef);
                }
                else
                {
                    // If we've only 2 to choose from, just do a normal random lookup. Otherwise, it would just toggle between the two.
                    clipIndex = (uint)Random.Range(0, soundDef.clips.Count);
                }
            }
            else if (soundDef.playbackType == SoundDef.PlaybackTypes.Random)
            {
                clipIndex = (uint)Random.Range(0, soundDef.clips.Count);
            }
            else if (soundDef.playbackType == SoundDef.PlaybackTypes.Sequential)
            {
                soundEmitter.sequentialClipIndex++;
                // Ensure it's in range (if swapping between user <> sequential)
                soundEmitter.sequentialClipIndex %= (uint)soundDef.clips.Count;
                clipIndex = soundEmitter.sequentialClipIndex;
            }

            return clipIndex;
        }

        /// <summary>
        /// Selects a random clip index that is different from the last played clip.
        /// </summary>
        /// <param name="soundEmitter">The controlling SoundEmitter with selection state.</param>
        /// <param name="soundDef">The SoundDef containing the clip list.</param>
        /// <returns>A random clip index that wasn't the last one played.</returns>
        private uint GetRandomClipIndexButNotLast(SoundEmitter soundEmitter, SoundDef soundDef)
        {
            // Select next random footstep AudioClip. But don't use the same one twice in a row
            uint randomOffset = (uint)Random.Range(1, soundDef.clips.Count - 1);
            soundEmitter.randomClipIndex += randomOffset;
            soundEmitter.randomClipIndex %= (uint)soundDef.clips.Count;
            return soundEmitter.randomClipIndex;
        }

        #endregion
    }
}
