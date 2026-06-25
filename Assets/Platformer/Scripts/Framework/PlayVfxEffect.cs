using UnityEngine;
using Unity.Netcode;
using UnityEngine.VFX;
using System.Collections;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Platformer
{
    #region Enums

    /// <summary>
    /// Defines the source position for the VFX effect.
    /// </summary>
    public enum VfxPositionSource
    {
        /// <summary>
        /// Use the interactor's position and rotation.
        /// </summary>
        Interactor,
        /// <summary>
        /// Use the interactable's position and rotation.
        /// </summary>
        Interactable
    }

    #endregion

    /// <summary>
    /// An interaction effect that instantiates and plays a visual effect prefab (either a Particle System or a VFX Graph)
    /// at either the interactor's or interactable object's position. The effect is played on all clients via an RPC for synchronized visuals.
    /// </summary>
    public class PlayVfxEffect : NetworkBehaviour, IInteractionEffect
    {
        #region Fields & Properties

        [Header("Effect Settings")]
        [Tooltip("The VFX or Particle System prefab to instantiate and play upon interaction.")]
        [SerializeField] private GameObject effectPrefab;
        [Tooltip("The priority of this effect in the interaction chain. Higher values are executed first.")]
        [SerializeField] private int priority = 0;
        [Tooltip("The duration in seconds before the instantiated effect GameObject is automatically destroyed.")]
        [SerializeField] private float effectDuration = 2f;
        [Tooltip("Whether to use the interactor's or interactable's position and rotation for the VFX.")]
        [SerializeField] private VfxPositionSource vfxPositionSource = VfxPositionSource.Interactable;
        [Tooltip("Optional positional offset to apply to the effect's spawn position.")]
        [SerializeField] private Vector3 offset;

        public int Priority => priority;

        #endregion

        #region Public Methods

        /// <summary>
        /// Applies the VFX effect by determining the spawn position and triggering the networked effect playback.
        /// </summary>
        /// <param name="interactor">The GameObject initiating the interaction.</param>
        /// <param name="interactable">The GameObject being interacted with.</param>
        /// <returns>An IEnumerator for coroutine execution.</returns>
        public IEnumerator ApplyEffect(GameObject interactor, GameObject interactable)
        {
            if (effectPrefab == null)
            {
                yield break;
            }

            Transform sourceTransform = vfxPositionSource == VfxPositionSource.Interactor
                ? interactor.transform
                : interactable.transform;

            // Calculate spawn position by applying the offset to the source transform's position
            Vector3 spawnPosition = sourceTransform.position + offset;
            PlayEffectClientRpc(spawnPosition, sourceTransform.rotation);
            yield return null;
        }

        /// <summary>
        /// Cancels the effect. Currently not implemented as VFX effects play to completion.
        /// </summary>
        /// <param name="interactor">The GameObject that initiated the interaction.</param>
        public void CancelEffect(GameObject interactor) { }

        #endregion

        #region Private Methods

        /// <summary>
        /// Instantiates and plays the VFX effect on all clients at the specified position and rotation.
        /// Automatically detects and handles both Visual Effect Graphs and Particle Systems.
        /// </summary>
        /// <param name="position">The world position where the effect should spawn.</param>
        /// <param name="rotation">The rotation of the spawned effect.</param>
        [Rpc(SendTo.Everyone)]
        private void PlayEffectClientRpc(Vector3 position, Quaternion rotation)
        {
            if (effectPrefab == null) return;

            GameObject instance = Instantiate(effectPrefab, position, rotation);

            // Check for Visual Effect Graph component first, then fall back to Particle System
            if (instance.TryGetComponent<VisualEffect>(out var vfx))
            {
                vfx.Play();
            }
            else if (instance.TryGetComponent<ParticleSystem>(out var ps))
            {
                ps.Play();
            }

            // Automatically clean up the effect GameObject after the specified duration
            Destroy(instance, effectDuration);
        }

        #endregion
    }
}
