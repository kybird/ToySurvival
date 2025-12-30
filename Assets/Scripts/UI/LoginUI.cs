using UnityEngine;
using UnityEngine.UI;
using Protocol;
using TMPro; // TMP 사용 권장 (없을 경우 일반 Text로 대체 가능)

public class LoginUI : MonoBehaviour
{
    [Header("Network Settings")]
    public string ip = "127.0.0.1";
    public int port = 9000;

    [Header("UI References")]
    public InputField usernameInput;
    public InputField passwordInput;
    public Button loginButton;
    public Image backgroundImage;
    public CanvasGroup mainCanvasGroup;

    void Start()
    {
        if (loginButton != null)
            loginButton.onClick.AddListener(OnClickLogin);
            
        // 연출: 페이드 인
        if (mainCanvasGroup != null)
        {
            mainCanvasGroup.alpha = 0;
            StartCoroutine(FadeInUI());
        }
    }

    System.Collections.IEnumerator FadeInUI()
    {
        float duration = 1.0f;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            mainCanvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
    }

    public void OnClickConnect()
    {
        Debug.Log($"Connecting to {ip}:{port}...");
        NetworkManager.Instance.Connect(ip, port);
    }

    public void OnClickLogin()
    {
        string user = usernameInput != null ? usernameInput.text : "TestUser";
        string pass = passwordInput != null ? passwordInput.text : "Password123"; // passwordInput 필드 사용

        Debug.Log($"Sending LoginRequest for User: {user}, Password: {pass.Substring(0, Mathf.Min(pass.Length, 3))}..."); // 안내 문구 보강
        
        LoginRequest req = new LoginRequest()
        {
            Username = user,
            Password = pass
        };
        NetworkManager.Instance.Send(req);
    }
}
