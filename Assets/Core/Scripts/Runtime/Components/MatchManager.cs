using UnityEngine;
using Unity.Netcode;
using System.Collections;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Core
{
    public class MatchManager : NetworkBehaviour
    {
        public static MatchManager Instance { get; private set; }

        [SerializeField] private float matchDuration = 300f; // 5 min
        [SerializeField] private float restartDelay = 5f;

        private readonly NetworkVariable<float> m_TimeRemaining = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> m_MatchEnded = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<ulong> m_WinnerId = new NetworkVariable<ulong>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public float TimeRemaining => m_TimeRemaining.Value;
        public bool MatchEnded => m_MatchEnded.Value;
        public ulong WinnerId => m_WinnerId.Value;

        public event System.Action<float> OnTimerUpdated;
        public event System.Action<ulong> OnMatchEndedEvent;


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticInstance()
        {
            Instance = null;
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsOwner)
            {
                m_TimeRemaining.Value = matchDuration;
                m_MatchEnded.Value = false;
            }
            m_TimeRemaining.OnValueChanged += (_, newVal) => OnTimerUpdated?.Invoke(newVal);
            m_MatchEnded.OnValueChanged += HandleMatchEndedChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            m_MatchEnded.OnValueChanged -= HandleMatchEndedChanged;
        }

        private void Update()
        {
            if (!IsOwner || m_MatchEnded.Value) return;

            m_TimeRemaining.Value -= Time.deltaTime;
            if (m_TimeRemaining.Value <= 0f)
            {
                m_TimeRemaining.Value = 0f;
                EndMatch();
            }
        }

        private void EndMatch()
        {
            ulong winner = 0;
            int bestKills = -1;

            foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
            {
                var playerObj = kvp.Value.PlayerObject;
                var state = playerObj != null ? playerObj.GetComponent<CorePlayerState>() : null;
                if (state != null && state.KillCount > bestKills)
                {
                    bestKills = state.KillCount;
                    winner = kvp.Key;
                }
            }

            m_WinnerId.Value = winner;
            m_MatchEnded.Value = true;
            StartCoroutine(RestartAfterDelay());
        }

        private IEnumerator RestartAfterDelay()
        {
            yield return new WaitForSeconds(restartDelay);

            foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
            {
                var playerObj = kvp.Value.PlayerObject;
                var state = playerObj != null ? playerObj.GetComponent<CorePlayerState>() : null;
                state?.ResetKills();
            }

            m_TimeRemaining.Value = matchDuration;
            m_MatchEnded.Value = false;
        }

        private void HandleMatchEndedChanged(bool oldVal, bool newVal)
        {
            if (newVal) OnMatchEndedEvent?.Invoke(m_WinnerId.Value);
        }
    }
}