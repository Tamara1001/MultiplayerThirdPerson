using UnityEngine;
using Unity.Netcode;
using Unity.Cinemachine;
using System.Collections;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Shooter
{
    public class ShooterHitProcessor : HitProcessor
    {
        #region Fields & Properties

        [Header("Component Dependencies")]
        [SerializeField] private CoreStatsHandler coreStats;
        [SerializeField] private CorePlayerState corePlayerState;
        [SerializeField] private ShooterAnimator shooterAnimator;

        [Header("Damage Modifiers")]
        [SerializeField] private float headshotMultiplier = 2.0f;
        [SerializeField] private float armorDamageReduction = 0.3f;
        [SerializeField] private bool hasArmor = false;

        [Header("Hit Feedback")]
        [SerializeField] private bool showDamageNumbers = true;

        [Header("Sound Effects")]
        [SerializeField] private SoundDef soundDefPlayerHit;

        private readonly int m_AnimIDPlayerHit = Animator.StringToHash("PlayerHit");

        #endregion

        #region Unity Methods

        private void Awake()
        {
            if (coreStats == null)
            {
                coreStats = GetComponent<CoreStatsHandler>();
            }
            if (corePlayerState == null)
            {
                corePlayerState = GetComponent<CorePlayerState>();
            }
            if (shooterAnimator == null)
            {
                shooterAnimator = GetComponentInChildren<ShooterAnimator>();
            }
        }

        #endregion

        #region Protected Methods

        protected override void HandleHit(HitInfo info)
        {
            if (info.attackerId == OwnerClientId)
            {
                return;
            }

            if (corePlayerState != null && !corePlayerState.IsActive)
            {
                return;
            }

            float finalDamage = CalculateDamage(info);
            coreStats.ModifyStat(StatKeys.Health, -finalDamage, info.attackerId, ModificationSource.Damage);

            if (shooterAnimator != null && shooterAnimator.Animator != null)
            {
                shooterAnimator.Animator.ResetTrigger(m_AnimIDPlayerHit);
                shooterAnimator.Animator.SetTrigger(m_AnimIDPlayerHit);
            }

            // Apply impact shake effect using explosion impulse with short duration
            CoreDirector.RequestCameraShake()
                .WithImpulseDefinition(CinemachineImpulseDefinition.ImpulseShapes.Explosion,
                    CinemachineImpulseDefinition.ImpulseTypes.Dissipating,
                    0.2f)
                .WithVelocity(0.02f)
                .Execute();

            SendHitFeedbackToAllRpc(info.attackerId, finalDamage, info.hitPoint);
        }

        #endregion

        #region Private Methods

        private float CalculateDamage(HitInfo info)
        {
            float damage = info.amount;
            if (hasArmor)
            {
                damage *= (1f - armorDamageReduction);
            }

            // Check if hit point is above character's head height to determine headshot
            // Uses a 1.5 unit offset above the character's position as the headshot threshold
            if (info.hitPoint.y > transform.position.y + 1.5f)
            {
                damage *= headshotMultiplier;
            }
            return damage;
        }

        private void CreateHitFeedback(Vector3 hitPoint, float actualDamage, bool isAttacker)
        {
            if (showDamageNumbers)
            {
                CreateDamageNumberVisual(hitPoint, actualDamage, isAttacker);
            }
        }

        private void CreateDamageNumberVisual(Vector3 position, float damage, bool isAttacker)
        {
            GameObject damageNumberObj = new GameObject("DamageNumber") { transform = { position = position + Vector3.up * 0.5f } };
            TextMesh textMesh = damageNumberObj.AddComponent<TextMesh>();
            textMesh.text = $"-{damage.ToString("F0")}";
            textMesh.fontSize = 48;
            textMesh.characterSize = 0.05f;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = Color.red;

            StartCoroutine(AnimateDamageNumber(damageNumberObj.transform, textMesh));
        }

        private IEnumerator AnimateDamageNumber(Transform numberTransform, TextMesh textMesh)
        {
            float duration = 1.5f;
            float elapsed = 0f;
            Vector3 startPos = numberTransform.position;
            Color startColor = textMesh.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                numberTransform.position = startPos + Vector3.up * progress;
                textMesh.color = new Color(startColor.r, startColor.g, startColor.b, 1f - progress);

                if (Camera.main != null)
                {
                    numberTransform.LookAt(Camera.main.transform);
                    numberTransform.Rotate(0, 180, 0);
                }
                yield return null;
            }
            Destroy(numberTransform.gameObject);
        }

        [Rpc(SendTo.Everyone)]
        private void SendHitFeedbackToAllRpc(ulong attackerId, float damageDealt, Vector3 hitPoint)
        {
            bool isLocalAttacker = NetworkManager.Singleton.LocalClientId == attackerId;
            CreateHitFeedback(hitPoint, damageDealt, isLocalAttacker);
            if (soundDefPlayerHit != null)
            {
                CoreDirector.RequestAudio(soundDefPlayerHit).WithPosition(hitPoint).Play();
            }
        }

        #endregion
    }
}
