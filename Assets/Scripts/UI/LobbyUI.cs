using UnityEngine;
using UnityEngine.UI;
using Protocol;

public class LobbyUI : MonoBehaviour
{
    [Header("UI References")]
    public Button enterRoomButton;
    public Text statusText;
    public CanvasGroup lobbyCanvasGroup;

    void Start()
    {
        if (enterRoomButton != null)
            enterRoomButton.onClick.AddListener(OnClickEnterRoom);

        if (statusText != null)
            statusText.text = "Logged In Successfully. Waiting for Room...";
            
        // 연출: 페이드 인
        if (lobbyCanvasGroup != null)
        {
            lobbyCanvasGroup.alpha = 0;
            StartCoroutine(FadeInUI());
        }
    }

    System.Collections.IEnumerator FadeInUI()
    {
        float duration = 0.8f;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            lobbyCanvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
    }

    public void OnClickEnterRoom()
    {
        Debug.Log("Waiting for EnterRoom packet from server...");
    }
}
