using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using System.Collections;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Core
{
    public class MatchHUD : CoreHUD
    {
        private Label m_TimerLabel;
        private Label m_KillCountLabel;
        private VisualElement m_WinnerScreen;
        private Label m_WinnerLabel;

        protected override void Initialize()
        {
            base.Initialize();

            StartCoroutine(WaitForMatchManagerAndSubscribe());

            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            var playerState = localPlayer != null ? localPlayer.GetComponent<CorePlayerState>() : null;
            if (playerState != null)
            {
                playerState.OnKillCountChanged += UpdateKillCountDisplay;
                UpdateKillCountDisplay(playerState.KillCount); // valor inicial, por si ya hay kills
            }

            if (m_WinnerScreen != null) m_WinnerScreen.style.display = DisplayStyle.None;
        }

        private IEnumerator WaitForMatchManagerAndSubscribe()
        {
            yield return new WaitUntil(() => MatchManager.Instance != null);

            MatchManager.Instance.OnTimerUpdated += UpdateTimerDisplay;
            MatchManager.Instance.OnMatchEndedEvent += ShowWinnerScreen;

            UpdateTimerDisplay(MatchManager.Instance.TimeRemaining); // valor inicial, así arranca mostrando 05:00 ya
        }

        protected override void QueryHUDElements(VisualElement root)
        {
            base.QueryHUDElements(root);
            m_TimerLabel = root.Q<Label>("match-timer-label");
            m_KillCountLabel = root.Q<Label>("kill-count-label");
            m_WinnerScreen = root.Q<VisualElement>("winner-screen");
            m_WinnerLabel = root.Q<Label>("winner-label");
        }

        private void UpdateTimerDisplay(float timeRemaining)
        {
            if (m_TimerLabel == null) return;
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            m_TimerLabel.text = $"{minutes:00}:{seconds:00}";
        }

        private void UpdateKillCountDisplay(int kills)
        {
            if (m_KillCountLabel != null) m_KillCountLabel.text = $"Kills: {kills}";
        }

        private void ShowWinnerScreen(ulong winnerId)
        {
            if (m_WinnerScreen == null) return;

            NetworkManager.Singleton.ConnectedClients.TryGetValue(winnerId, out var client);
            var winnerState = client?.PlayerObject != null ? client.PlayerObject.GetComponent<CorePlayerState>() : null;
            string winnerName = winnerState != null ? winnerState.PlayerName : $"Player{winnerId}";

            if (m_WinnerLabel != null) m_WinnerLabel.text = $"¡Ganó {winnerName}!";
            m_WinnerScreen.style.display = DisplayStyle.Flex;
            StartCoroutine(HideWinnerScreenAfterDelay());
        }

        private IEnumerator HideWinnerScreenAfterDelay()
        {
            yield return new WaitForSeconds(4.5f);
            m_WinnerScreen.style.display = DisplayStyle.None;
        }
    }
}