using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    [Header("Game Over Panel")]
    public GameObject gameOverPanel;
    public Text resultText;
    public Text survivalTimeText;

    [Header("Notification")]
    public Text notificationText;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        if (notificationText != null)
            notificationText.text = "";
    }

    public void ShowGameOver(bool isWin, long survivedTimeMs)
    {
        if (gameOverPanel == null)
            return;

        gameOverPanel.SetActive(true);

        if (resultText != null)
        {
            resultText.text = isWin ? "YOU WIN!" : "GAME OVER";
            resultText.color = isWin ? Color.yellow : Color.red;
        }

        if (survivalTimeText != null)
        {
            float seconds = survivedTimeMs / 1000f;
            survivalTimeText.text = $"Survived Time: {seconds:F1}s";
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
}
