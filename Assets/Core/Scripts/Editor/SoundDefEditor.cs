using UnityEditor;
using UnityEngine;
using Blocks.Gameplay.Core;
using UnityEngine.UIElements;

/// <summary>
/// Custom editor for <see cref="SoundDef"/> that provides in-editor audio preview functionality.
/// Allows designers to test sound playback directly in the inspector without entering play mode.
/// </summary>
[CustomEditor(typeof(SoundDef))]
public class SoundDefEditor : Editor
{
    #region Fields & Properties

    // Static fields are shared across all inspector instances to maintain playback state
    private static int s_RepeatCount;
    private static bool s_PlayStatus;
    private static SoundDef s_SoundDef;
    private static SoundMixer s_SoundMixer;
    private static SoundEmitter s_SoundEmitter;
    private static SoundGameObject.AudioComponentData[] s_AudioComponentData;

    #endregion

    #region Unity Methods

    /// <summary>
    /// Initializes the inspector GUI and sets up audio preview infrastructure.
    /// </summary>
    public override VisualElement CreateInspectorGUI()
    {
        s_SoundDef = (SoundDef)target;
        InitAudio();
        return null;
    }

    public void OnDestroy()
    {
        StopPlayback();

        if (s_SoundMixer != null)
        {
            if (s_SoundEmitter != null)
            {
                s_SoundEmitter.Release();
                CleanupTestGameObjects();
                s_SoundEmitter = null;
            }
        }
    }

    /// <summary>
    /// Draws the custom inspector with playback controls and standard property fields.
    /// </summary>
    public override void OnInspectorGUI()
    {
        s_SoundDef = (SoundDef)target;
        var oldEnabled = GUI.enabled;
        GUI.enabled = true;

        if (s_PlayStatus && GUILayout.Button("Stop [" + s_RepeatCount + "]"))
        {
            StopPlayback();
        }
        else if (s_PlayStatus == false && GUILayout.Button("Play >"))
        {
            if (s_SoundDef.unityAudioMixer != null)
            {
                s_SoundMixer = new SoundMixer(s_SoundDef.unityAudioMixer);
                s_PlayStatus = true;
                s_SoundEmitter.SetSoundDef(s_SoundDef);
                s_SoundEmitter.Activate();
                s_RepeatCount = Random.Range(s_SoundDef.repeatInfo.repeatMin, s_SoundDef.repeatInfo.repeatMax);

                SoundEmitter.SoundDefOverrideData soundDefOverrideInfo = s_SoundEmitter.SoundDefOverrideInfo;

                soundDefOverrideInfo.VolumeScale = s_SoundDef.volumeScale;
                soundDefOverrideInfo.BasePitchInCents = s_SoundDef.basePitchInCents;
                soundDefOverrideInfo.BaseLowPassCutoff = s_SoundDef.baseLowPassCutoff;

                foreach (var soundGameObject in s_SoundEmitter.ActiveSoundGameObjects)
                {
                    soundGameObject.EnableAudioFilterComponents(s_SoundDef);
                    soundGameObject.Enable(s_SoundEmitter);
                }

                // Subscribe to editor update loop to monitor playback and handle repeats
                EditorApplication.update += Update;
                s_SoundEmitter.volume = s_SoundDef.editorVolume;
                s_SoundEmitter.Play(null, s_SoundMixer.MixerGroups);
            }
        }

        GUI.enabled = oldEnabled;
        DrawPropertiesExcluding(serializedObject, new[] { "m_Script" });
        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Forces the inspector to repaint continuously during playback to update the play button UI.
    /// </summary>
    public override bool RequiresConstantRepaint()
    {
        return true;
    }

    #endregion

    #region Private Methods

    private static void StopPlayback()
    {
        if (!s_PlayStatus)
        {
            return;
        }

        s_PlayStatus = false;
        s_SoundEmitter.Stop();
        EditorApplication.update -= Update;
    }

    static void CleanupTestGameObjects()
    {
        if (s_AudioComponentData == null)
        {
            return;
        }

        foreach (SoundGameObject.AudioComponentData audioData in s_AudioComponentData)
        {
            if (audioData.ASource != null && audioData.ASource.gameObject != null)
            {
                DestroyImmediate(audioData.ASource.gameObject);
            }
        }

        s_AudioComponentData = null;
    }

    private void InitAudio()
    {
        if (s_SoundEmitter == null)
        {
            s_AudioComponentData = new SoundGameObject.AudioComponentData[s_SoundDef.playCount];
            s_SoundEmitter = new SoundEmitter(null);

            for (int i = 0; i < s_SoundDef.playCount; i++)
            {
                // Create temporary GameObjects hidden from hierarchy and scene for audio preview
                var gameObject = new GameObject("testSource") { hideFlags = HideFlags.HideAndDontSave };
                s_AudioComponentData[i] = new SoundGameObject.AudioComponentData() { ASource = gameObject.AddComponent<AudioSource>(), LowPassFilter = gameObject.AddComponent<AudioLowPassFilter>(), HighPassFilter = gameObject.AddComponent<AudioHighPassFilter>(), DistortionFilter = gameObject.AddComponent<AudioDistortionFilter>() };
                SoundGameObject soundGameObject = new SoundGameObject(gameObject, s_AudioComponentData[i]);
                s_SoundEmitter.ActiveSoundGameObjects.Add(soundGameObject);
            }

            s_SoundEmitter.reserved = SoundEmitter.ReservedInfo.ReservedEmitterAndAudioSources;
        }
    }

    private static void Update()
    {
        int playCount = 0;
        foreach (var soundGameObject in s_SoundEmitter.ActiveSoundGameObjects)
        {
            if (soundGameObject.IsPlaying())
            {
                playCount++;
                soundGameObject.CheckForAudioSourceLoop();
            }
        }

        // When all audio sources finish playing, either repeat or stop based on repeat count
        if (playCount == 0)
        {
            Debug.Log("Repeat Count:" + s_SoundEmitter.GetRepeatCount());

            if (s_RepeatCount > 1)
            {
                s_RepeatCount--;
                s_SoundEmitter.Play(null, s_SoundMixer.MixerGroups);
            }
            else
            {
                StopPlayback();
                EditorUtility.SetDirty(EditorWindow.focusedWindow);
            }
        }
    }

    #endregion
}
