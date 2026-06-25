using UnityEngine;
using Blocks.Gameplay.Core;

/// <summary>
/// A builder class for creating and configuring sound playback requests
/// </summary>
public class SoundRequestBuilder
{
    private readonly SoundSystem m_SoundSystem;
    private readonly SoundDef m_SoundDef;

    private Transform m_Transform;
    private Vector3 m_Position;
    private SoundEmitter.SoundDefOverrideData m_Overrides;
    private SoundEmitter.ReservedInfo m_ReservedInfo = SoundEmitter.ReservedInfo.FreeAfterPlaybackCompletes;

    public SoundRequestBuilder(SoundSystem soundSystem, SoundDef soundDef)
    {
        m_SoundSystem = soundSystem;
        m_SoundDef = soundDef;
    }

    /// <summary>
    /// Attaches the sound to a Transform. The sound will follow this transform.
    /// This is mutually exclusive with WithPosition().
    /// </summary>
    public SoundRequestBuilder AttachedTo(Transform transform)
    {
        m_Transform = transform;
        m_Position = Vector3.zero;
        return this;
    }

    /// <summary>
    /// Sets the sound to play at a specific world-space position.
    /// This is mutually exclusive with AttachedTo().
    /// </summary>
    public SoundRequestBuilder WithPosition(Vector3 position)
    {
        m_Position = position;
        m_Transform = null;
        return this;
    }

    /// <summary>
    /// Applies custom override data to the sound definition for this playback instance.
    /// </summary>
    public SoundRequestBuilder WithOverrides(SoundEmitter.SoundDefOverrideData overrideData)
    {
        m_Overrides = overrideData;
        return this;
    }

    /// <summary>
    /// Sets the reservation type for the sound emitter, allowing for stateful playback
    /// (e.g., for sequential or random-not-last clip selection).
    /// </summary>
    public SoundRequestBuilder AsReserved(SoundEmitter.ReservedInfo reservedInfo)
    {
        m_ReservedInfo = reservedInfo;
        return this;
    }

    /// <summary>
    /// Finalizes the request and plays the sound.
    /// </summary>
    /// <param name="volume">The volume to play the sound at (0.0 to 1.0).</param>
    /// <returns>A SoundInfo object containing the handle and details of the played sound.</returns>
    public SoundSystem.SoundInfo Play(float volume = 1.0f)
    {
        if (m_SoundSystem == null || m_SoundDef == null)
        {
            // This handles the case where AudioManager wasn't ready and returned a dummy builder.
            return null;
        }

        var soundInfo = new SoundSystem.SoundInfo
        {
            SoundDefinition = m_SoundDef,
            SoundTransform = m_Transform,
            Position = m_Position,
            SoundDefOverrideInfo = m_Overrides,
            Reserved = m_ReservedInfo
        };

        return m_SoundSystem.Play(soundInfo, volume);
    }

    /// <summary>
    /// Stops all sound emitters that are also playing the same sound definition
    /// </summary>
    /// <returns>True if any sound emitter has been stopped, false otherwise.</returns>
    public bool ForceSolo()
    {
        if (m_SoundSystem == null || m_SoundDef == null)
        {
            return false;
        }
        return m_SoundSystem.Stop(m_SoundDef);
    }
}
