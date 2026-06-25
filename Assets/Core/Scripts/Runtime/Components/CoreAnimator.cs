using UnityEngine;
using Unity.Netcode.Components;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Controls the player's Animator component based on the state of the <see cref="CoreMovement"/> controller.
    /// This component is responsible for setting locomotion parameters (speed, grounded, jump, etc.)
    /// and handling Animation Events to trigger sound effects like footsteps and landing sounds.
    /// It inherits from <see cref="NetworkAnimator"/> to automatically synchronize animation states across the network.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CoreAnimator : NetworkAnimator
    {
        #region Fields & Properties

        [Header("Component Dependencies")]
        [Tooltip("Reference to the CoreMovement component to get movement state information.")]
        [SerializeField] private CoreMovement coreMovement;

        [Header("Sound Effects")]
        [Tooltip("Sound definition for footstep sounds.")]
        [SerializeField] private SoundDef soundDefFootstep;

        private readonly int m_AnimIDSpeed = Animator.StringToHash("Speed");
        private readonly int m_AnimIDGrounded = Animator.StringToHash("Grounded");
        private readonly int m_AnimIDJump = Animator.StringToHash("Jump");
        private readonly int m_AnimIDFreeFall = Animator.StringToHash("FreeFall");
        private readonly int m_AnimIDMotionSpeed = Animator.StringToHash("MotionSpeed");

        #endregion

        #region Unity & Network Lifecycle

        protected override void Awake()
        {
            base.Awake();
            if (coreMovement == null)
            {
                Debug.LogError("[Core Animator] needs a CoreMovement component.");
            }

            if (soundDefFootstep == null)
            {
                Debug.LogError("[Core Animator] Footstep SoundDef is not assigned.");
            }
        }

        private void Update()
        {
            // We only want the owner to send animation state updates.
            // NetworkAnimator will handle propagating these changes to other clients.
            if (!IsOwner || coreMovement == null) return;

            UpdateLocomotionParameters();
        }

        #endregion

        #region Animation Events

        public void OnFootstepWalk(AnimationEvent animationEvent)
        {
            // >=0.5 - We want to be sure that if both walk and run weights are equal, that we only trigger one SFX.
            if (animationEvent.animatorClipInfo.weight >= 0.5f)
            {
                OnFootstep(animationEvent, 0, 0.75f, 0);
            }
        }

        public void OnFootstepRun(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                OnFootstep(animationEvent, 500, 1, 2000);
            }
        }

        /// <summary>
        /// This method is called by an AnimationEvent defined in the walk/run animation clips.
        /// It plays a random footstep sound.
        /// </summary>
        /// <param name="animationEvent">Data from the animation event.</param>
        /// <param name="walkRunPitchCents"></param>
        /// <param name="walkRunVolumeScale"></param>
        /// <param name="filterCutoffOffset"></param>
        public void OnFootstep(AnimationEvent animationEvent, float walkRunPitchCents, float walkRunVolumeScale, float filterCutoffOffset)
        {
            var overrideData = new SoundEmitter.SoundDefOverrideData
            {
                BasePitchInCents = walkRunPitchCents,
                VolumeScale = walkRunVolumeScale,
                BaseLowPassCutoff = filterCutoffOffset
            };

            CoreDirector.RequestAudio(soundDefFootstep)
                .AttachedTo(transform)
                .WithOverrides(overrideData)
                .AsReserved(SoundEmitter.ReservedInfo.ReservedEmitterAndAudioSources)
                .Play();
        }

        /// <summary>
        /// This method is called by an AnimationEvent defined in the landing animation clip.
        /// It plays the landing sound effect.
        /// </summary>
        /// <param name="animationEvent">Data from the animation event.</param>
        public void OnLand(AnimationEvent animationEvent)
        {
            CoreDirector.RequestAudio(soundDefFootstep)
                .AttachedTo(transform)
                .AsReserved(SoundEmitter.ReservedInfo.ReservedEmitterAndAudioSources)
                .Play();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Reads the current state from the CoreMovement component and updates the Animator parameters accordingly.
        /// </summary>
        private void UpdateLocomotionParameters()
        {
            bool isGrounded = coreMovement.IsGrounded;
            float verticalVelocity = coreMovement.VerticalVelocity;

            // Set booleans for grounded, jumping, and falling states.
            Animator.SetBool(m_AnimIDGrounded, isGrounded);
            Animator.SetBool(m_AnimIDJump, !isGrounded && verticalVelocity > 0.1f);
            Animator.SetBool(m_AnimIDFreeFall, !isGrounded && verticalVelocity <= 0.1f);

            // Set floats for speed and input magnitude to drive blend trees.
            Animator.SetFloat(m_AnimIDSpeed, coreMovement.CurrentSpeed);
            Animator.SetFloat(m_AnimIDMotionSpeed, coreMovement.InputMagnitude);
        }

        public void TurnInPlaceStart()
        {
            Animator.applyRootMotion = true;
        }

        public void TurnInPlaceEnd()
        {
            Animator.applyRootMotion = false;
        }

        #endregion
    }
}
