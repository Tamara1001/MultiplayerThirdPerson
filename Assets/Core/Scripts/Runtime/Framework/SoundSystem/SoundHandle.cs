namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// SoundHandle is stored within the SoundInfo class following a request to allocate a SoundEmitter.
    /// This allows the SoundEmitter to be tracked and validated.
    /// For example, a SoundEmitter may have finished and had been reallocated by another CreateEmitter() call.
    /// In this situation, we ignore any such requests that use invalid SoundHandle.
    /// </summary>
    public class SoundHandle
    {
        #region Fields & Properties

        /// <summary>
        /// SoundEmitter that is used to play the SoundAudioObjects.
        /// </summary>
        public readonly SoundEmitter Emitter;

        private readonly int m_Seq;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new SoundHandle with no emitter reference.
        /// </summary>
        public SoundHandle()
        {
            Emitter = null;
        }

        /// <summary>
        /// Initializes a new SoundHandle with a reference to the specified SoundEmitter.
        /// </summary>
        /// <param name="soundEmitter">The SoundEmitter to reference and track.</param>
        public SoundHandle(SoundEmitter soundEmitter)
        {
            Emitter = soundEmitter;
            m_Seq = soundEmitter?.seqId ?? -1;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Validates that this handle is still attached to the same SoundEmitter instance.
        /// </summary>
        /// <returns>True if the handle is valid and references the same SoundEmitter, false otherwise.</returns>
        public bool IsValid()
        {
            // Are we still attached to the same SoundEmitter?
            return Emitter != null && Emitter.seqId == m_Seq;
        }

        #endregion
    }
}
