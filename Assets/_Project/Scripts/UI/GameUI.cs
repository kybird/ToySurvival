using System.Collections;
using Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    [Header("Game End Panel")]
    public GameObject gameEndPanel;
    public Text titleText;
    public Text subtitleText;
    public Button exitButton;

    [Header("Notification")]
    public Text notificationText;

    // [Header("Level Up UI")] - Removed: Maintained by LevelUpUI.cs
    // public GameObject levelUpPanel;

    // private Protocol.S_LevelUpOption _currentOptions;
    // private bool _isLevelUpShowing = false;

    // ... (LevelUp Fallback methods removed for clarity)

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        if (gameEndPanel != null)
            gameEndPanel.SetActive(false);
        if (notificationText != null)
            notificationText.text = "";

        // Exit Button 이벤트 연결
        if (exitButton != null)
        {
            exitButton.onClick.AddListener(OnExitButtonClicked);
        }
    }

    /// <summary>
    /// 나만 죽고 팀원이 살아있을 때 (부활 대기)
    /// </summary>
    public void ShowPlayerDowned()
    {
        if (gameEndPanel == null)
            return;

        gameEndPanel.SetActive(true);

        if (titleText != null)
        {
            titleText.text = "부활 대기 중...";
            titleText.color = Color.yellow;
        }

        if (subtitleText != null)
        {
            subtitleText.text = "팀원이 부활시켜줄 수 있습니다.";
        }

        if (exitButton != null)
        {
            exitButton.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 플레이어가 부활했을 때 패널 숨기기
    /// </summary>
    public void HidePlayerDowned()
    {
        if (gameEndPanel != null)
        {
            gameEndPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 모든 플레이어가 죽었을 때 (게임 실패)
    /// </summary>
    public void ShowGameOver(bool isWin, long survivedTimeMs)
    {
        Debug.Log($"[GameUI] ShowGameOver Called. IsWin: {isWin}, Survived: {survivedTimeMs}ms");

        if (gameEndPanel == null)
        {
            Debug.LogError(
                "[GameUI] CRITICAL ERROR: gameEndPanel is not assigned in the Inspector!"
            );
            return;
        }

        gameEndPanel.SetActive(true);
        Debug.Log("[GameUI] gameEndPanel SetActive(true) called.");

        if (isWin)
        {
            // 승리
            if (titleText != null)
            {
                titleText.text = "🎉 축하합니다! 🎉";
                titleText.color = Color.yellow;
            }

            if (subtitleText != null)
            {
                float seconds = survivedTimeMs / 1000f;
                subtitleText.text = $"생존 시간: {seconds:F1}초";
            }
        }
        else
        {
            // 패배
            if (titleText != null)
            {
                titleText.text = "💀 게임 실패 💀";
                titleText.color = Color.red;
            }

            if (subtitleText != null)
            {
                float seconds = survivedTimeMs / 1000f;
                subtitleText.text = $"생존 시간: {seconds:F1}초";
            }
        }

        if (exitButton != null)
        {
            exitButton.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 나가기 버튼 클릭 시 로비로 복귀
    /// </summary>
    /// <summary>
    /// 나가기 버튼 클릭 시 로비로 복귀
    /// </summary>
    private void OnExitButtonClicked()
    {
        Debug.Log("[GameUI] Exit button clicked. Returning to Lobby...");

        // 1. 서버에 퇴장 알림 (패킷 전송을 멈추게 함)
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            Protocol.C_LeaveRoom leavePkt = new Protocol.C_LeaveRoom();
            NetworkManager.Instance.Send(leavePkt);
        }

        // 2. 게임 상태 초기화 및 씬 전환
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerEvent(StateEvent.LeaveRoom);
        }
    }

    public void ShowNotification(string message, Color color)
    {
        if (notificationText == null)
            return;

        StopAllCoroutines();
        StartCoroutine(CoShowNotification(message, color));
    }

    private IEnumerator CoShowNotification(string message, Color color)
    {
        notificationText.text = message;
        notificationText.color = color;
        notificationText.gameObject.SetActive(true);

        yield return new WaitForSeconds(2.0f);

        notificationText.gameObject.SetActive(false);
    }

    /* LevelUp Logic moved to LevelUpUI.cs
    public void ShowLevelUpOptions(Protocol.S_LevelUpOption res)
    {
        // Legacy Fallback removed
    }

    public void SelectLevelUpOption(int index)
    {
        // Legacy Fallback removed
    }

    private void OnGUI()
    {
        // Legacy Fallback removed
    }
    */
}
