using UnityEngine;
using Unity.Netcode;
using System.Collections;
using Blocks.Gameplay.Core;
using System.Collections.Generic;

namespace Blocks.Gameplay.Platformer
{
    /// <summary>
    /// An interaction effect that changes the player's gravity to zero when they enter a trigger zone,
    /// allowing them to float and move around. The gravity is restored when they exit the trigger.
    /// This effect tracks gravity changes per interactor to support multiple players.
    /// </summary>
    public class GravityChangeEffect : MonoBehaviour, IInteractionEffect
    {
        [Header("Effect Settings")]
        [Tooltip("The priority of this effect in the interaction chain. Higher values are executed first.")]
        [SerializeField] private int priority = 0;

        [Tooltip("The gravity value to apply while in the trigger zone. Use 0 for zero gravity (true floating).")]
        [SerializeField] private float gravityValue = 0f;

        // Track original gravity values per interactor to support multiple players
        private readonly Dictionary<GameObject, float> m_OriginalGravityValues = new Dictionary<GameObject, float>();
        // Track interactors currently in the trigger zone
        private readonly HashSet<GameObject> m_InteractorsInZone = new HashSet<GameObject>();

        public int Priority => priority;

        private void OnDisable()
        {
            RestoreAllGravity();
        }

        private void OnDestroy()
        {
            RestoreAllGravity();
        }

        private void RestoreAllGravity()
        {
            foreach (var kvp in m_OriginalGravityValues)
            {
                if (kvp.Key != null && kvp.Key.TryGetComponent<CoreMovement>(out var coreMovement))
                {
                    coreMovement.gravity = kvp.Value;
                }
            }
            m_OriginalGravityValues.Clear();
            m_InteractorsInZone.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            GameObject interactor = other.gameObject;

            // Only apply to objects owned by the client (for network sync)
            if (interactor.TryGetComponent<NetworkObject>(out var netObj) && !netObj.IsOwner)
            {
                return;
            }

            if (!interactor.TryGetComponent<CoreMovement>(out var coreMovement))
            {
                return;
            }

            // If already in zone, skip
            if (m_InteractorsInZone.Contains(interactor))
            {
                return;
            }

            // Store the original gravity value if not already stored
            if (!m_OriginalGravityValues.ContainsKey(interactor))
            {
                m_OriginalGravityValues[interactor] = coreMovement.gravity;
            }

            // Set gravity to zero
            coreMovement.gravity = gravityValue;
            m_InteractorsInZone.Add(interactor);
        }

        private void OnTriggerExit(Collider other)
        {
            GameObject interactor = other.gameObject;

            // Only restore for objects owned by the client (for network sync)
            if (interactor.TryGetComponent<NetworkObject>(out var netObj) && !netObj.IsOwner)
            {
                return;
            }

            if (!m_InteractorsInZone.Contains(interactor))
            {
                return;
            }

            // Restore original gravity
            if (m_OriginalGravityValues.TryGetValue(interactor, out var originalGravity))
            {
                if (interactor != null && interactor.TryGetComponent<CoreMovement>(out var coreMovement))
                {
                    coreMovement.gravity = originalGravity;
                }
                m_OriginalGravityValues.Remove(interactor);
            }

            m_InteractorsInZone.Remove(interactor);
        }

        public IEnumerator ApplyEffect(GameObject interactor, GameObject interactable)
        {
            // This effect is handled by trigger enter/exit, so ApplyEffect is minimal
            // But we can still apply it here if called by ModularInteractable
            if (!interactor.TryGetComponent<CoreMovement>(out var coreMovement))
            {
                yield break;
            }

            // Store the original gravity value if not already stored
            if (!m_OriginalGravityValues.ContainsKey(interactor))
            {
                m_OriginalGravityValues[interactor] = coreMovement.gravity;
            }

            // Set gravity to zero
            coreMovement.gravity = gravityValue;
            m_InteractorsInZone.Add(interactor);

            yield return null;
        }

        public void CancelEffect(GameObject interactor)
        {
            if (!m_InteractorsInZone.Contains(interactor))
            {
                return;
            }

            // Restore original gravity
            if (m_OriginalGravityValues.TryGetValue(interactor, out var originalGravity))
            {
                if (interactor != null && interactor.TryGetComponent<CoreMovement>(out var coreMovement))
                {
                    coreMovement.gravity = originalGravity;
                }
                m_OriginalGravityValues.Remove(interactor);
            }

            m_InteractorsInZone.Remove(interactor);
        }
    }
}

