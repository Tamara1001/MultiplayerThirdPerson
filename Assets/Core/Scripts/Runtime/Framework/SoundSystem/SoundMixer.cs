using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Manages Unity AudioMixer settings and provides centralized control over audio mixing groups.
    /// This class handles the mapping between SoundMixerGroup enums and actual AudioMixerGroup references,
    /// and manages volume levels for different audio categories (Music, SFX, Menu, Footsteps).
    /// It also provides utility functions for converting between amplitude and decibel values.
    /// </summary>
    public class SoundMixer
    {
        #region Enums

        /// <summary>
        /// Defines the available audio mixer groups for categorizing different types of sounds.
        /// </summary>
        public enum SoundMixerGroup
        {
            Music,
            Sfx,
            Footsteps,
            Menu
        }

        #endregion

        #region Fields & Properties

        /// <summary>
        /// Array of AudioMixerGroups corresponding to the SoundMixerGroup enum values.
        /// </summary>
        public AudioMixerGroup[] MixerGroups;

        private AudioMixer m_AudioMixer;
        private float m_SoundAmplitudeCutoff;

        // Volume constants
        private const float k_SoundSfxVol = 1;
        private const float k_SoundMenuVol = 1;
        private const float k_SoundMusicVol = 1;
        private const float k_SoundMasterVol = 1;
        private const float k_SoundVolumeCutoff = -60f;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new SoundMixer with the specified AudioMixer.
        /// </summary>
        /// <param name="audioMixer">The Unity AudioMixer to manage.</param>
        public SoundMixer(AudioMixer audioMixer)
        {
            Init(audioMixer);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates all mixer group volume levels with the specified master volume multiplier.
        /// </summary>
        /// <param name="masterVolume">The master volume multiplier to apply to all audio.</param>
        public void Update(float masterVolume)
        {
            m_AudioMixer.SetFloat("MasterVolume", DecibelFromAmplitude(Mathf.Clamp(k_SoundMasterVol, 0.0f, 1.0f) * masterVolume));
            m_AudioMixer.SetFloat("MusicVolume", DecibelFromAmplitude(Mathf.Clamp(k_SoundMusicVol, 0.0f, 1.0f)));
            m_AudioMixer.SetFloat("SFXVolume", DecibelFromAmplitude(Mathf.Clamp(k_SoundSfxVol, 0.0f, 1.0f)));
            m_AudioMixer.SetFloat("MenuVolume", DecibelFromAmplitude(Mathf.Clamp(k_SoundMenuVol, 0.0f, 1.0f)));
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Initializes the SoundMixer by setting up amplitude cutoff and mapping mixer groups.
        /// </summary>
        /// <param name="audioMixer">The Unity AudioMixer to initialize with.</param>
        private void Init(AudioMixer audioMixer)
        {
            m_SoundAmplitudeCutoff = Mathf.Pow(2.0f, k_SoundVolumeCutoff / 6.0f);
            m_AudioMixer = audioMixer;

            // Set up mixer groups
            int len = Enum.GetNames(typeof(SoundMixerGroup)).Length;
            MixerGroups = new AudioMixerGroup[len];
            MixerGroups[(int)SoundMixerGroup.Menu] = audioMixer.FindMatchingGroups("Menu")[0];
            MixerGroups[(int)SoundMixerGroup.Music] = audioMixer.FindMatchingGroups("Music")[0];
            MixerGroups[(int)SoundMixerGroup.Sfx] = audioMixer.FindMatchingGroups("SFX")[0];
            MixerGroups[(int)SoundMixerGroup.Footsteps] = audioMixer.FindMatchingGroups("Footsteps")[0];
        }

        /// <summary>
        /// Converts a linear amplitude value to decibels.
        /// </summary>
        /// <param name="amplitude">The amplitude value to convert.</param>
        /// <returns>The equivalent decibel value.</returns>
        private float DecibelFromAmplitude(float amplitude)
        {
            if (amplitude < m_SoundAmplitudeCutoff)
            {
                return -60.0f;
            }

            return 6.0f * Mathf.Log(amplitude) / Mathf.Log(2.0f);
        }

        #endregion
    }
}
