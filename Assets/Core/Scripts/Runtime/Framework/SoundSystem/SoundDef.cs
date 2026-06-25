using System;
using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Defines the configuration for a sound that can be played through the audio system.
    /// This ScriptableObject contains all parameters needed to play audio clips with variations
    /// in pitch, volume, filters, and spatial properties.
    /// </summary>
    [CreateAssetMenu(fileName = "Sound", menuName = "SoundDef/Create", order = 10000)]
    public class SoundDef : ScriptableObject
    {
        #region Enums

        /// <summary>
        /// Defines the playback mode for selecting audio clips from the clips list.
        /// </summary>
        public enum PlaybackTypes
        {
            Random,
            RandomNotLast,
            Sequential,
            User
        }

        #endregion

        #region Fields & Properties

        [Header("Sound Definition")]
        [Tooltip("The mixer group this sound will be routed to.")]
        public SoundMixer.SoundMixerGroup mixerGroup;
        [Tooltip("Base pitch offset in cents (100 cents = 1 semitone).")]
        public float basePitchInCents = 0;
        [Tooltip("Overall volume scale multiplier.")]
        public float volumeScale = 1;
        [Tooltip("Base low pass filter cutoff frequency.")]
        public float baseLowPassCutoff = 0;
        [Tooltip("Method for selecting audio clips from the clips list.")]
        public PlaybackTypes playbackType;
        [Tooltip("Number of simultaneous audio sources to play.")]
        public int playCount = 1;
        [Tooltip("List of audio clips to choose from.")]
        public List<AudioClip> clips;
        [Tooltip("Repeat and loop configuration.")]
        public Repeat repeatInfo;
        [Tooltip("Start and stop timing configuration.")]
        public StartStop startStopInfo;
        [Tooltip("Pitch and volume randomization settings.")]
        public PitchAndVolume pitchAndVolumeInfo;
        [Tooltip("Distance and spatialization settings.")]
        public Distance distanceInfo;
        [Tooltip("Low pass filter configuration.")]
        public Filter lowPassFilter;
        [Tooltip("High pass filter configuration.")]
        public Filter highPassFilter;
        [Tooltip("Distortion effect configuration.")]
        public Distortion distortionFilter;

#if UNITY_EDITOR
        [Header("Editor Only")]
        [Tooltip("Volume for preview playback in the editor.")]
        public float editorVolume = 1;
        [Tooltip("Audio mixer for editor preview.")]
        public AudioMixer unityAudioMixer;
#endif

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Validates and clamps all parameter ranges to ensure they are within acceptable bounds.
        /// This prevents configuration errors that could cause audio system issues.
        /// </summary>
        public void OnValidate()
        {
            pitchAndVolumeInfo.volumeMin = pitchAndVolumeInfo.volumeMin > pitchAndVolumeInfo.volumeMax ? pitchAndVolumeInfo.volumeMax : pitchAndVolumeInfo.volumeMin;
            distanceInfo.volumeDistMin = distanceInfo.volumeDistMin > distanceInfo.volumeDistMax ? distanceInfo.volumeDistMax : distanceInfo.volumeDistMin;
            pitchAndVolumeInfo.pitchMin = pitchAndVolumeInfo.pitchMin > pitchAndVolumeInfo.pitchMax ? pitchAndVolumeInfo.pitchMax : pitchAndVolumeInfo.pitchMin;
            startStopInfo.delayMin = startStopInfo.delayMin > startStopInfo.delayMax ? startStopInfo.delayMax : startStopInfo.delayMin;
            repeatInfo.repeatMin = repeatInfo.repeatMin > repeatInfo.repeatMax ? repeatInfo.repeatMax : repeatInfo.repeatMin;
            distanceInfo.panMin = distanceInfo.panMin > distanceInfo.panMax ? distanceInfo.panMax : distanceInfo.panMin;
            lowPassFilter.cutoffMin = lowPassFilter.cutoffMin > lowPassFilter.cutoffMax ? lowPassFilter.cutoffMax : lowPassFilter.cutoffMin;
            highPassFilter.cutoffMin = highPassFilter.cutoffMin > highPassFilter.cutoffMax ? highPassFilter.cutoffMax : highPassFilter.cutoffMin;
            distortionFilter.distortionMin = distortionFilter.distortionMin > distortionFilter.distortionMax ? distortionFilter.distortionMax : distortionFilter.distortionMin;
            startStopInfo.startOffsetPercentMin = startStopInfo.startOffsetPercentMin > startStopInfo.startOffsetPercentMax ? startStopInfo.startOffsetPercentMax : startStopInfo.startOffsetPercentMin;
        }

        #endregion

        #region Data Structures

        /// <summary>
        /// Configuration for audio filter effects (low pass and high pass).
        /// </summary>
        [Serializable]
        public class Filter
        {
            [Tooltip("Whether to enable this filter component.")]
            public bool enableComponent;
            [Tooltip("Minimum cutoff frequency for randomization.")]
            [Range(0, 22000)] public float cutoffMin = 22000.0f;
            [Tooltip("Maximum cutoff frequency for randomization.")]
            [Range(0, 22000)] public float cutoffMax = 22000.0f;
        }

        /// <summary>
        /// Configuration for distortion audio effect.
        /// </summary>
        [Serializable]
        public class Distortion
        {
            [Tooltip("Whether to enable the distortion effect.")]
            public bool enableComponent;
            [Tooltip("Minimum distortion level for randomization.")]
            [Range(0.0f, 1.0f)] public float distortionMin = 0f;
            [Tooltip("Maximum distortion level for randomization.")]
            [Range(0.0f, 1.0f)] public float distortionMax = 0f;
        }

        /// <summary>
        /// Configuration for start and stop timing of audio playback.
        /// </summary>
        [Serializable]
        public class StartStop
        {
            [Tooltip("Minimum delay before starting playback.")]
            [Range(0, 10.0f)] public float delayMin = 0.0f;
            [Tooltip("Maximum delay before starting playback.")]
            [Range(0, 10.0f)] public float delayMax = 0.0f;
            [Tooltip("Minimum percentage offset into the audio clip to start playback.")]
            [Range(0, 100)] public int startOffsetPercentMin = 0;
            [Tooltip("Maximum percentage offset into the audio clip to start playback.")]
            [Range(0, 100)] public int startOffsetPercentMax = 0;
            [Tooltip("Time in seconds to stop playback after starting (0 = play to end).")]
            [Range(0, 60)] public float stopDelay = 0.0f;
        }

        /// <summary>
        /// Configuration for pitch and volume randomization.
        /// </summary>
        [Serializable]
        public class PitchAndVolume
        {
            [Tooltip("Minimum volume level in decibels.")]
            [Range(-60.0f, 0.0f)] public float volumeMin = -6.0f;
            [Tooltip("Maximum volume level in decibels.")]
            [Range(-60.0f, 0.0f)] public float volumeMax = -6.0f;
            [Tooltip("Minimum pitch offset in cents.")]
            [Range(-8000, 8000.0f)] public float pitchMin = 0.0f;
            [Tooltip("Maximum pitch offset in cents.")]
            [Range(-8000, 8000.0f)] public float pitchMax = 0.0f;
        }

        /// <summary>
        /// Configuration for repeat and loop behavior.
        /// </summary>
        [Serializable]
        public class Repeat
        {
            [Tooltip("Number of times to loop the audio (0 = infinite, 1 = play once).")]
            [Range(0, 10)] public int loopCount = 1;
            [Tooltip("Minimum number of times to repeat the entire sound.")]
            [Range(1, 20)] public int repeatMin = 1;
            [Tooltip("Maximum number of times to repeat the entire sound.")]
            [Range(1, 20)] public int repeatMax = 1;
        }

        /// <summary>
        /// Configuration for 3D spatialization and distance-based effects.
        /// </summary>
        [Serializable]
        public class Distance
        {
            [Tooltip("Minimum distance for volume attenuation.")]
            [Range(0.1f, 100.0f)] public float volumeDistMin = 1.5f;
            [Tooltip("Maximum distance for volume attenuation.")]
            [Range(0.1f, 100.0f)] public float volumeDistMax = 30.0f;
            [Tooltip("How much this audio source is affected by 3D spatialization (0 = 2D, 1 = 3D).")]
            [Range(0.0f, 1.0f)] public float spatialBlend = 1.0f;
            [Tooltip("Doppler effect intensity (0 = no doppler, 1 = full doppler).")]
            [Range(0.0f, 1.0f)] public float dopplerScale = 0.0f;
            [Tooltip("How volume attenuates over distance.")]
            public AudioRolloffMode volumeRolloffMode = AudioRolloffMode.Linear;

            [Header("Low Pass Filter Distance")]
            [Tooltip("Curve type for low pass filter rolloff.")]
            public Interpolator.CurveType lpfRollOffCurveType;
            [Tooltip("Minimum cutoff frequency for the low pass filter.")]
            [Range(0, 22000)] public float lpfMinCutoff;
            [Tooltip("Maximum distance for low pass filter effect.")]
            [Range(0f, 100.0f)] public float lpfMaxDistance;

            [Header("High Pass Filter Distance")]
            [Tooltip("Curve type for high pass filter rolloff.")]
            public Interpolator.CurveType hpfRollOffCurveType;
            [Tooltip("Minimum cutoff frequency for the high pass filter.")]
            [Range(0, 22000)] public float hpfMinCutoff;
            [Tooltip("Maximum distance for high pass filter effect.")]
            [Range(0f, 100.0f)] public float hpfMaxDistance;

            [Header("Spatial Blend Distance")]
            [Tooltip("Curve type for spatial blend rolloff.")]
            public Interpolator.CurveType spatialBlendCurveType;
            [Tooltip("Maximum distance for spatial blend effect.")]
            [Range(0f, 100.0f)] public float spatialBlendMaxDistance;

            [Header("Stereo Pan")]
            [Tooltip("Minimum stereo pan value (-1 = left, 0 = center, 1 = right).")]
            [Range(-1.0f, 1.0f)] public float panMin = 0.0f;
            [Tooltip("Maximum stereo pan value (-1 = left, 0 = center, 1 = right).")]
            [Range(-1.0f, 1.0f)] public float panMax = 0.0f;
        }

        #endregion
    }
}
