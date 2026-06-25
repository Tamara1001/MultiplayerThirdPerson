using UnityEngine;
using Unity.Netcode;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// A generic hit processor for any object with a Rigidbody.
    /// It listens for hits and applies the impact force.
    /// This should be added to any physics-enabled prefabs (like crates, barrels, etc.).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PhysicsObjectHitProcessor : HitProcessor
    {
        #region Fields & Properties

        [Tooltip("Sound to play when this object is hit.")]
        [SerializeField] private SoundDef soundDefTargetHit;

        private Rigidbody m_Rigidbody;

        #endregion

        #region Unity Methods

        private void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Handles the hit by applying impact force to the rigidbody and playing impact effects.
        /// </summary>
        /// <param name="info">Information about the hit including impact force and hit point.</param>
        protected override void HandleHit(HitInfo info)
        {
            if (m_Rigidbody != null && info.impactForce != Vector3.zero)
            {
                m_Rigidbody.AddForceAtPosition(info.impactForce, info.hitPoint, ForceMode.Impulse);
            }

            PlayImpactEffectRpc(info.hitPoint);
        }

        #endregion

        #region Private Methods

        [Rpc(SendTo.Everyone)]
        private void PlayImpactEffectRpc(Vector3 position)
        {
            if (!soundDefTargetHit) return;

            CoreDirector.RequestAudio(soundDefTargetHit)
                .WithPosition(position)
                .Play();
        }

        #endregion
    }
}
