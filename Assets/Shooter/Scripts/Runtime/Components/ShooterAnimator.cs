using UnityEngine;
using Blocks.Gameplay.Core;
using Unity.Netcode.Components;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Manages animations for shooter characters, including locomotion, aiming, shooting, and reloading.
    /// Syncs animation state across the network using <see cref="NetworkAnimator"/>.
    /// Handles animation events for footsteps and weapon sounds.
    /// </summary>
    public class ShooterAnimator : NetworkAnimator
    {
        #region Fields & Properties

        [Header("Component Dependencies")]
        [Tooltip("Reference to the CoreMovement component for locomotion data.")]
        [SerializeField] private CoreMovement coreMovement;
        [Tooltip("Reference to the AimController component for aiming state.")]
        [SerializeField] private AimController aimController;

        [Header("Listening to Events")]
        [Tooltip("Event triggered when the primary action (shoot) is pressed.")]
        [SerializeField] private GameEvent onPrimaryActionPressedEvent;
        [Tooltip("Event triggered when the weapon is changed.")]
        [SerializeField] private WeaponSwapEvent onWeaponChanged;
        [Tooltip("Event triggered when reload begins, carries reload duration.")]
        [SerializeField] private FloatEvent onReloadStartedEvent;
        [Tooltip("Event triggered when reload completes.")]
        [SerializeField] private GameEvent onReloadCompletedEvent;

        [Header("Sound Effects")]
        [Tooltip("Sound definition for footstep audio.")]
        [SerializeField] private SoundDef soundDefFootstep;
        [Tooltip("Array of sound definitions for weapon reload audio, indexed by weapon type.")]
        [SerializeField] private SoundDef[] soundDefWeaponReload;

        [Header("Animation Speed Modifiers")]
        [Tooltip("Multiplier for motion speed when aiming and walking.")]
        [SerializeField] private float aimingWalkMotionSpeedModifier = 1.0f;
        [Tooltip("Multiplier for motion speed when aiming and sprinting.")]
        [SerializeField] private float aimingSprintMotionSpeedModifier = 1.0f;

        private readonly int m_AnimIDSpeed = Animator.StringToHash("Speed");
        private readonly int m_AnimIDGrounded = Animator.StringToHash("Grounded");
        private readonly int m_AnimIDJump = Animator.StringToHash("Jump");
        private readonly int m_AnimIDFreeFall = Animator.StringToHash("FreeFall");
        private readonly int m_AnimIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        private readonly int m_AnimIDIsAiming = Animator.StringToHash("IsAiming");
        private readonly int m_AnimIDShoot = Animator.StringToHash("Shoot");
        private readonly int m_AnimIDIsReloading = Animator.StringToHash("IsReloading");
        private readonly int m_AnimIDReloadSpeed = Animator.StringToHash("ReloadSpeed");
        private readonly int m_AnimIDIsSwitchingWeapon = Animator.StringToHash("IsSwitchingWeapon");
        private readonly int m_AnimIDStrafeX = Animator.StringToHash("StrafeX");
        private readonly int m_AnimIDStrafeY = Animator.StringToHash("StrafeY");

        private IWeapon m_LastWeaponEquipped;

        #endregion

        #region Unity Methods

        protected override void Awake()
        {
            base.Awake();
            if (coreMovement == null) coreMovement = GetComponentInParent<CoreMovement>();
            if (aimController == null) aimController = GetComponentInParent<AimController>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsOwner)
            {
                onPrimaryActionPressedEvent.RegisterListener(HandleFireAnimation);
                onReloadStartedEvent.RegisterListener(HandleReloadStarted);
                onReloadCompletedEvent.RegisterListener(HandleReloadCompleted);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                onPrimaryActionPressedEvent.UnregisterListener(HandleFireAnimation);
                onReloadStartedEvent.UnregisterListener(HandleReloadStarted);
                onReloadCompletedEvent.UnregisterListener(HandleReloadCompleted);
            }
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned || coreMovement == null) return;

            if (IsOwner)
            {
                UpdateLocomotionParameters();
            }

            UpdateWeaponStateParameters();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Animation event callback triggered at the end of the reload animation.
        /// Ensures the IsReloading parameter is set to false.
        /// </summary>
        public void OnReloadEnd()
        {
            Animator.SetBool(m_AnimIDIsReloading, false);
        }

        /// <summary>
        /// Animation event callback triggered at the end of the weapon switch animation.
        /// Clears the IsSwitchingWeapon parameter.
        /// </summary>
        public void OnWeaponSwitchEnd()
        {
            Animator.SetBool(m_AnimIDIsSwitchingWeapon, false);
        }

        /// <summary>
        /// Animation event callback for walking footstep sounds.
        /// Only triggers if the walk animation weight is at least 0.5 to prevent duplicate sounds.
        /// </summary>
        /// <param name="animationEvent">Animation event data containing clip info and weight.</param>
        public void OnFootstepWalk(AnimationEvent animationEvent)
        {
            // >=0.5 ensures that if both walk and run weights are equal, only one SFX triggers
            if (animationEvent.animatorClipInfo.weight >= 0.5f)
            {
                OnFootstep(animationEvent, 0, 0.75f, 0);
            }
        }

        /// <summary>
        /// Animation event callback for running footstep sounds.
        /// Only triggers if the run animation weight exceeds 0.5 to prevent duplicate sounds.
        /// </summary>
        /// <param name="animationEvent">Animation event data containing clip info and weight.</param>
        public void OnFootstepRun(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                OnFootstep(animationEvent, 500, 1, 2000);
            }
        }

        /// <summary>
        /// Plays a footstep sound with the specified audio parameters.
        /// Uses reserved audio sources to ensure footsteps always have available emitters.
        /// </summary>
        /// <param name="animationEvent">Animation event data (unused but required by Unity animation events).</param>
        /// <param name="walkRunPitchCents">Pitch adjustment in cents (100 cents = 1 semitone).</param>
        /// <param name="walkRunVolumeScale">Volume multiplier for the footstep sound.</param>
        /// <param name="filterCutoffOffset">Low-pass filter cutoff frequency offset.</param>
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
        /// Animation event callback for landing sounds.
        /// Plays a footstep sound when the character lands on the ground.
        /// </summary>
        /// <param name="animationEvent">Animation event data (unused but required by Unity animation events).</param>
        public void OnLand(AnimationEvent animationEvent)
        {
            CoreDirector.RequestAudio(soundDefFootstep)
                .AttachedTo(transform)
                .AsReserved(SoundEmitter.ReservedInfo.ReservedEmitterAndAudioSources)
                .Play();
        }

        /// <summary>
        /// Animation event callback for weapon reload sounds.
        /// Plays the appropriate reload sound based on the currently equipped weapon index.
        /// </summary>
        /// <param name="animationEvent">Animation event data (unused but required by Unity animation events).</param>
        public void OnWeaponReload(AnimationEvent animationEvent)
        {
            bool gotWeapon = aimController.TryGetWeaponIndex(out var weaponIndex);

            // Each weapon type has its own reload sound in the array
            if (gotWeapon && weaponIndex < soundDefWeaponReload.Length)
            {
                CoreDirector.RequestAudio(soundDefWeaponReload[weaponIndex])
                    .AttachedTo(transform)
                    .Play(0.5f);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Handles the reload started event by setting animation parameters.
        /// </summary>
        /// <param name="timer">The duration of the reload animation.</param>
        private void HandleReloadStarted(float timer)
        {
            Animator.SetFloat(m_AnimIDReloadSpeed, 1f);
            Animator.SetBool(m_AnimIDIsReloading, true);
        }

        /// <summary>
        /// Handles the reload completed event by clearing the reloading animation state.
        /// </summary>
        private void HandleReloadCompleted()
        {
            Animator.SetBool(m_AnimIDIsReloading, false);
        }

        /// <summary>
        /// Handles the fire animation trigger when the primary action is pressed.
        /// Only executed on the owner's instance to prevent duplicate animations.
        /// </summary>
        private void HandleFireAnimation()
        {
            if (IsOwner) Animator.SetTrigger(m_AnimIDShoot);
        }

        /// <summary>
        /// Updates locomotion-related animation parameters based on <see cref="CoreMovement"/> state.
        /// Handles grounded state, jumping, free falling, speed, and input magnitude.
        /// </summary>
        private void UpdateLocomotionParameters()
        {
            bool isGrounded = coreMovement.IsGrounded;
            float verticalVelocity = coreMovement.VerticalVelocity;

            Animator.SetBool(m_AnimIDGrounded, isGrounded);

            // Use 0.1f threshold to distinguish between jumping (upward) and falling (downward)
            Animator.SetBool(m_AnimIDJump, !isGrounded && verticalVelocity > 0.1f);
            Animator.SetBool(m_AnimIDFreeFall, !isGrounded && verticalVelocity <= 0.1f);

            Animator.SetFloat(m_AnimIDSpeed, coreMovement.CurrentSpeed);

            float speedModifier = 1.0f;
            if (aimController != null && aimController.IsAiming)
            {
                speedModifier = coreMovement.IsSprinting ? aimingSprintMotionSpeedModifier : aimingWalkMotionSpeedModifier;
            }

            Animator.SetFloat(m_AnimIDMotionSpeed, coreMovement.InputMagnitude * speedModifier);
        }

        /// <summary>
        /// Updates weapon and aiming-related animation parameters.
        /// Sets aiming state and strafe blend values for directional movement while aiming.
        /// </summary>
        private void UpdateWeaponStateParameters()
        {
            if (aimController != null)
            {
                // Sync Aim State
                bool isAiming = aimController.IsAiming;
                Animator.SetBool(m_AnimIDIsAiming, isAiming);

                // Strafe values only update for owner, relying on NetworkAnimator to sync to others.
                // This prevents '0' input on remote clients from overwriting the strafe blend.
                if (IsOwner)
                {
                    if (isAiming)
                    {
                        // Smoothly blend strafe animations based on movement input direction
                        Vector2 moveInput = coreMovement.MoveInput;
                        Animator.SetFloat(m_AnimIDStrafeX, moveInput.x, 0.1f, Time.deltaTime);
                        Animator.SetFloat(m_AnimIDStrafeY, moveInput.y, 0.1f, Time.deltaTime);
                    }
                    else
                    {
                        // Reset strafe parameters when not aiming
                        Animator.SetFloat(m_AnimIDStrafeX, 0f, 0.1f, Time.deltaTime);
                        Animator.SetFloat(m_AnimIDStrafeY, 0f, 0.1f, Time.deltaTime);
                    }
                }
            }
        }

        #endregion
    }
}
