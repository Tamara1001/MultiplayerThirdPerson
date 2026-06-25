using UnityEngine;
using Unity.Netcode;
using UnityEngine.VFX;
using System.Collections;
using Blocks.Gameplay.Core;
using Unity.Cinemachine;

namespace Blocks.Gameplay.Platformer
{
    /// <summary>
    /// An interaction effect that teleports the interacting object to a specified destination with customizable
    /// visual and audio effects. This effect supports timing control for when the actual teleport occurs,
    /// allowing for VFX/SFX to play before and after the teleportation. Effects are synchronized across all clients.
    /// </summary>
    public class TeleportEffect : NetworkBehaviour, IInteractionEffect
    {
        #region Fields & Properties

        [Header("Effect Settings")]
        [Tooltip("The priority of this effect. Higher values execute first. It's recommended to give teleports a high priority to ensure they happen before other effects like despawning.")]
        [SerializeField] private int priority = 10;
        [Tooltip("The Transform representing the destination point for the teleport.")]
        [SerializeField] private Transform destination;

        [Header("Teleport Start Effects")]
        [Tooltip("Visual effect prefab to play at the start position when teleport begins.")]
        [SerializeField] private GameObject teleportStartVFX;
        [Tooltip("Sound effect to play when teleport begins.")]
        [SerializeField] private SoundDef teleportStartSFX;
        [Tooltip("How long to wait after playing start effects before actually teleporting the player.")]
        [SerializeField] private float teleportDelay = 1.0f;

        [Header("Teleport End Effects")]
        [Tooltip("Visual effect prefab to play at the destination when teleport completes.")]
        [SerializeField] private GameObject teleportEndVFX;
        [Tooltip("Sound effect to play when teleport completes at destination.")]
        [SerializeField] private SoundDef teleportEndSFX;

        [Header("VFX & Audio Settings")]
        [Tooltip("How long VFX effects should stay visible before being destroyed.")]
        [SerializeField] private float vfxDuration = 2.0f;
        [Tooltip("Volume multiplier for sound effects (0-1).")]
        [SerializeField] [Range(0f, 1f)] private float sfxVolume = 1.0f;

        [Header("Movement Control")]
        [Tooltip("If true, disables player movement input during teleportation sequence.")]
        [SerializeField] private bool disableMovementDuringTeleport = true;

        public int Priority => priority;

        #endregion

        #region Public Methods

        public IEnumerator ApplyEffect(GameObject interactor, GameObject interactable)
        {
            if (destination == null)
            {
                Debug.LogWarning($"TeleportEffect on '{interactable.name}' is missing a destination transform.", interactable);
                yield break;
            }

            Vector3 startPosition = interactor.transform.position;
            Vector3 endPosition = destination.position;

            CorePlayerManager playerManager = null;
            if (disableMovementDuringTeleport)
            {
                playerManager = interactor.GetComponent<CorePlayerManager>();
                if (playerManager != null)
                {
                    playerManager.SetMovementInputEnabled(false);
                }
            }

            PlayTeleportStartEffectsRpc(startPosition);
            CoreDirector.RequestCameraShake()
                .WithImpulseDefinition(CinemachineImpulseDefinition.ImpulseShapes.Explosion,
                    CinemachineImpulseDefinition.ImpulseTypes.Dissipating,
                    0.2f)
                .WithVelocity(0.2f)
                .AtPosition(transform.position)
                .Execute();

            if (teleportDelay > 0f)
            {
                yield return new WaitForSeconds(teleportDelay);
            }

            PerformTeleport(interactor, endPosition);

            PlayTeleportEndEffectsRpc(endPosition);
            CoreDirector.RequestCameraShake()
                .WithImpulseDefinition(CinemachineImpulseDefinition.ImpulseShapes.Explosion,
                    CinemachineImpulseDefinition.ImpulseTypes.Propagating,
                    0.2f)
                .WithVelocity(0.2f)
                .AtPosition(endPosition)
                .Execute();

            if (disableMovementDuringTeleport && playerManager != null)
            {
                playerManager.SetMovementInputEnabled(true);
            }

            yield return null;
        }

        public void CancelEffect(GameObject interactor)
        {
            if (disableMovementDuringTeleport)
            {
                var playerManager = interactor.GetComponent<CorePlayerManager>();
                if (playerManager != null)
                {
                    playerManager.SetMovementInputEnabled(true);
                }
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// RPC that plays the teleport start effects on all connected clients for synchronized visuals and audio.
        /// </summary>
        [Rpc(SendTo.Everyone)]
        private void PlayTeleportStartEffectsRpc(Vector3 position)
        {
            if (teleportStartVFX != null)
            {
                GameObject vfxInstance = Instantiate(teleportStartVFX, position, Quaternion.identity);

                // Intelligently handle both Visual Effect Graphs and traditional Particle Systems.
                if (vfxInstance.TryGetComponent<VisualEffect>(out var vfx))
                {
                    vfx.Play();
                }
                else if (vfxInstance.TryGetComponent<ParticleSystem>(out var ps))
                {
                    ps.Play();
                }

                Destroy(vfxInstance, vfxDuration);
            }

            if (teleportStartSFX != null)
            {
                CoreDirector.RequestAudio(teleportStartSFX)
                    .WithPosition(position)
                    .Play(sfxVolume);
            }
        }

        /// <summary>
        /// RPC that plays the teleport end effects on all connected clients for synchronized visuals and audio.
        /// </summary>
        [Rpc(SendTo.Everyone)]
        private void PlayTeleportEndEffectsRpc(Vector3 position)
        {
            if (teleportEndVFX != null)
            {
                GameObject vfxInstance = Instantiate(teleportEndVFX, position, Quaternion.identity);

                // Intelligently handle both Visual Effect Graphs and traditional Particle Systems.
                if (vfxInstance.TryGetComponent<VisualEffect>(out var vfx))
                {
                    vfx.Play();
                }
                else if (vfxInstance.TryGetComponent<ParticleSystem>(out var ps))
                {
                    ps.Play();
                }

                Destroy(vfxInstance, vfxDuration);
            }

            if (teleportEndSFX != null)
            {
                CoreDirector.RequestAudio(teleportEndSFX)
                    .WithPosition(position)
                    .Play(sfxVolume);
            }
        }

        /// <summary>
        /// Performs the actual teleportation of the interactor to the destination.
        /// </summary>
        /// <param name="interactor">The GameObject to teleport.</param>
        /// <param name="targetPosition">The destination position.</param>
        private void PerformTeleport(GameObject interactor, Vector3 targetPosition)
        {
            // Use CoreMovement's dedicated SetPosition method for CharacterControllers to avoid physics issues.
            if (interactor.TryGetComponent<CoreMovement>(out var coreMovement))
            {
                coreMovement.SetPosition(targetPosition);
            }
            else
            {
                // Fallback for objects that don't use CoreMovement (e.g., simple Rigidbodies).
                // Note: This might not be suitable for all physics objects without additional logic.
                Debug.LogWarning($"Interactor '{interactor.name}' does not have a CoreMovement component. Performing a direct transform change.", interactor);
                interactor.transform.position = targetPosition;
            }
        }

        #endregion
    }
}
