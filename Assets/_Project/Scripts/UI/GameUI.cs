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

    [Header("Level Up UI")]
    public GameObject levelUpPanel;

    private Protocol.S_LevelUpOption _currentOptions;
    private bool _isLevelUpShowing = false;

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
        if (gameEndPanel == null)
            return;

        gameEndPanel.SetActive(true);

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
    private void OnExitButtonClicked()
    {
        Debug.Log("[GameUI] Exit button clicked. Returning to Lobby...");

        // 게임 상태 초기화
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerEvent(StateEvent.LeaveRoom);
        }

        // 로비 씬으로 이동 (씬 이름은 프로젝트에 맞게 조정)
        // SceneManager.LoadScene("LobbyScene");
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

    public void ShowLevelUpOptions(Protocol.S_LevelUpOption res)
    {
        _currentOptions = res;
        _isLevelUpShowing = true;

        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(true);
            // 실 구현 시에는 여기서 버튼들을 동적으로 생성하거나 텍스트를 채워야 합니다.
        }

        Debug.Log("[GameUI] Level Up UI Activated. Check OnGUI for options if panel is missing.");
    }

    public void SelectLevelUpOption(int index)
    {
        if (_currentOptions == null || index < 0 || index >= _currentOptions.Options.Count)
            return;

        Debug.Log($"[GameUI] Selecting Option {index}: {_currentOptions.Options[index].Name}");

        Protocol.C_SelectLevelUp selectPkt = new Protocol.C_SelectLevelUp();
        selectPkt.OptionIndex = index;
        NetworkManager.Instance.Send(selectPkt);

        _isLevelUpShowing = false;
        if (levelUpPanel != null)
            levelUpPanel.SetActive(false);
    }

    private void OnGUI()
    {
        if (!_isLevelUpShowing || _currentOptions == null)
            return;

        // 중앙에 레벨업 선택창 그리기
        float width = 400;
        float height = 300;
        float x = (Screen.width - width) / 2;
        float y = (Screen.height - height) / 2;

        GUI.Box(new Rect(x, y, width, height), "LEVEL UP!");

        for (int i = 0; i < _currentOptions.Options.Count; i++)
        {
            var opt = _currentOptions.Options[i];
            string btnText = $"[{i}] {opt.Name}\n{opt.Desc}";
            if (GUI.Button(new Rect(x + 20, y + 40 + i * 80, width - 40, 70), btnText))
            {
                SelectLevelUpOption(i);
            }
        }
    }
}
