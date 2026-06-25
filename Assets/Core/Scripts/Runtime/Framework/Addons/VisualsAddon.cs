using UnityEngine;
using Unity.Netcode.Components;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Represents a set of materials that can be applied to different parts of a player model.
    /// Used for player visual customization based on ClientID.
    /// </summary>
    [System.Serializable]
    public struct PlayerMaterialSet
    {
        public string name;
        public Material body;
        public Material arms;
        public Material legs;
    }

    /// <summary>
    /// Manages player visual elements including material customization, ragdoll physics, and elimination effects.
    /// This addon handles visual state changes based on player life states and applies client-specific materials.
    /// </summary>
    public class VisualsAddon : MonoBehaviour, IPlayerAddon
    {
        #region Fields & Properties

        [Header("Component Dependencies")]
        [Tooltip("A direct reference to the player's animator.")]
        [SerializeField] private Animator playerAnimator;
        [Tooltip("Reference to the component controller for enabling/disabling player components.")]
        [SerializeField] private ComponentController componentController;

        [Header("Visual Customization")]
        [Tooltip("The Renderer (MeshRenderer or SkinnedMeshRenderer) to apply materials to.")]
        [SerializeField] private Renderer targetRenderer;
        [Tooltip("Define the 4 sets of colors here.")]
        [SerializeField] private List<PlayerMaterialSet> materialSets;

        [Header("Visual Effects")]
        [Tooltip("Visual effect prefab instantiated when the player is eliminated.")]
        [SerializeField] private GameObject eliminatedVFX;
        [Tooltip("Sound definition played when the player is eliminated.")]
        [SerializeField] private SoundDef soundDefPlayerEliminated;

        [Header("Ragdoll Settings")]
        [Tooltip("When enabled, the player model will turn into a ragdoll upon elimination.")]
        [SerializeField] private bool useRagdollOnElimination = true;
        [Tooltip("The root GameObject containing all the ragdoll's Rigidbodies and Colliders.")]
        [SerializeField] private GameObject ragdollRoot;

        private Rigidbody[] m_RagdollRigidbodies;
        private Collider[] m_RagdollColliders;
        private CorePlayerManager m_PlayerManager;

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the VisualsAddon with required component references and sets up ragdoll physics.
        /// </summary>
        /// <param name="playerManager">The <see cref="CorePlayerManager"/> instance managing this player.</param>
        public void Initialize(CorePlayerManager playerManager)
        {
            m_PlayerManager = playerManager;

            if (playerAnimator == null)
            {
                playerAnimator = GetComponentInChildren<Animator>();
            }

            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }

            if (useRagdollOnElimination && ragdollRoot != null)
            {
                m_RagdollRigidbodies = ragdollRoot.GetComponentsInChildren<Rigidbody>();
                m_RagdollColliders = ragdollRoot.GetComponentsInChildren<Collider>();
                
                // Only disable physics, keep colliders active for hit detection
                foreach (var rb in m_RagdollRigidbodies)
                {
                    rb.isKinematic = true;
                }
            }
        }

        /// <summary>
        /// Called when the player spawns. Applies the appropriate material set based on the player's ClientID.
        /// </summary>
        public void OnPlayerSpawn()
        {
            ApplyMaterialSet();
        }

        /// <summary>
        /// Called when the player despawns. Currently performs no cleanup operations.
        /// </summary>
        public void OnPlayerDespawn() { }

        #endregion

        #region Private Methods

        /// <summary>
        /// Selects a material set based on ClientID and applies it to the renderer.
        /// Uses modulo operation to cycle through available material sets for each unique client.
        /// </summary>
        private void ApplyMaterialSet()
        {
            if (targetRenderer == null || materialSets == null || materialSets.Count == 0)
            {
                return;
            }

            // Use modulo to distribute material sets evenly across clients
            ulong clientId = m_PlayerManager.OwnerClientId;
            int index = (int)(clientId % (ulong)materialSets.Count);

            PlayerMaterialSet selectedSet = materialSets[index];

            Material[] newMaterials = new Material[3];
            newMaterials[0] = selectedSet.body;
            newMaterials[1] = selectedSet.arms;
            newMaterials[2] = selectedSet.legs;

            targetRenderer.materials = newMaterials;
        }

        /// <summary>
        /// Handles player life state transitions, managing ragdoll physics, animator state, and visual effects.
        /// </summary>
        /// <param name="previousState">The player's previous life state.</param>
        /// <param name="newState">The player's new life state.</param>
        public void OnLifeStateChanged(PlayerLifeState previousState, PlayerLifeState newState)
        {
            bool isEliminated = (newState == PlayerLifeState.Eliminated);
            bool isActive = !isEliminated;

            if (useRagdollOnElimination && ragdollRoot != null)
            {
                if (isActive)
                {
                    // Reset ragdoll transforms to initial pose when respawning
                    ragdollRoot.transform.localPosition = Vector3.zero;
                    ragdollRoot.transform.localRotation = Quaternion.identity;
                    foreach (var rb in m_RagdollRigidbodies)
                    {
                        rb.transform.localPosition = Vector3.zero;
                        rb.transform.localRotation = Quaternion.identity;
                        if (!rb.isKinematic)
                        {
                            rb.linearVelocity = Vector3.zero;
                            rb.angularVelocity = Vector3.zero;
                        }
                    }

                    // Rebind animator to synchronize with reset ragdoll pose
                    if (playerAnimator != null)
                    {
                        playerAnimator.enabled = true;
                        playerAnimator.Rebind();
                        playerAnimator.Update(0f);
                    }
                }
                else
                {
                    // Disable animator to allow ragdoll physics to take control
                    if (playerAnimator != null) playerAnimator.enabled = false;
                }

                SetRagdollState(isEliminated);
            }
            else
            {
                // When not using ragdoll, directly control component enabled state
                if (componentController != null && m_PlayerManager.HasAuthority)
                {
                    componentController.SetEnabled(!isEliminated);
                }
            }

            // Re-enable components after ragdoll respawn completes
            if (componentController != null && newState == PlayerLifeState.Respawned && useRagdollOnElimination && m_PlayerManager.HasAuthority)
            {
                componentController.SetEnabled(true);
            }

            // Play elimination effects only on first transition to eliminated state
            if (isEliminated && previousState != PlayerLifeState.Eliminated)
            {
                PlayEliminatedVFX();
            }
        }

        /// <summary>
        /// Toggles ragdoll physics by setting all rigidbodies to kinematic or dynamic.
        /// </summary>
        /// <param name="isActive">True to enable ragdoll physics (dynamic), false to disable (kinematic).</param>
        private void SetRagdollState(bool isActive)
        {
            if (m_RagdollRigidbodies == null) return;

            foreach (var rb in m_RagdollRigidbodies)
            {
                rb.isKinematic = !isActive;
            }
        }

        /// <summary>
        /// Plays elimination visual and audio effects at the player's current position.
        /// </summary>
        private void PlayEliminatedVFX()
        {
            if (soundDefPlayerEliminated != null)
            {
                CoreDirector.RequestAudio(soundDefPlayerEliminated)
                    .WithPosition(transform.position)
                    .Play();
            }

            if (eliminatedVFX != null)
            {
                GameObject vfxInstance = Instantiate(eliminatedVFX, transform.position, Quaternion.identity);
                Destroy(vfxInstance, 1f);
            }
        }

        #endregion
    }
}
