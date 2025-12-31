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
    public Text statusText; // 연결 상태 표시용 텍스트 (Unity UI Text)

    void Start()
    {
        if (loginButton != null)
        {
            loginButton.onClick.AddListener(OnClickLogin);
            loginButton.interactable = false; // 시작 시 비활성화
        }
            
        // 연출: 페이드 인
        if (mainCanvasGroup != null)
        {
            mainCanvasGroup.alpha = 0;
            StartCoroutine(FadeInUI());
        }

        // 네트워크 상태 변화 리스너 등록
        NetworkManager.Instance.OnConnected = HandleOnConnected;
        NetworkManager.Instance.OnDisconnected = HandleOnDisconnected;
        
        UpdateStatusText();

        // 씬 시작 시 자동 연결 시도
        OnClickConnect();
    }

    void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnConnected -= HandleOnConnected;
            NetworkManager.Instance.OnDisconnected -= HandleOnDisconnected;
        }
    }

    void HandleOnConnected()
    {
        UpdateStatusText();
    }

    void HandleOnDisconnected()
    {
        UpdateStatusText();
    }

    void UpdateStatusText()
    {
        bool connected = NetworkManager.Instance.IsConnected;

        if (statusText != null)
        {
            if (connected)
            {
                statusText.text = "Server: Connected";
                statusText.color = Color.green;
            }
            else
            {
                statusText.text = "Server: Disconnected (Retrying...)";
                statusText.color = Color.red;
            }
        }

        if (loginButton != null)
        {
            loginButton.interactable = connected; // 연결 상태에 따라 버튼 활성화 제어
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
        if (NetworkManager.Instance.IsConnected == false)
        {
            Debug.LogWarning("Not connected to server. Login aborted.");
            UpdateStatusText();
            return;
        }

        string user = usernameInput != null ? usernameInput.text : "TestUser";
        string pass = passwordInput != null ? passwordInput.text : "Password123";

        Debug.Log($"Sending C_Login for User: {user}, Password: {pass.Substring(0, Mathf.Min(pass.Length, 3))}...");
        
        C_Login req = new C_Login()
        {
            Username = user,
            Password = pass
        };
        NetworkManager.Instance.Send(req);
    }
}
