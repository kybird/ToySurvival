using Protocol;
using TMPro; // TMP 사용 권장 (없을 경우 일반 Text로 대체 가능)
using UnityEngine;
using UnityEngine.UI;

public class LoginUI : MonoBehaviour
{
    [Header("Network Settings")]
    public string ip = "127.0.0.1";
    public int port = 9001;

    [Header("UI References")]
    public InputField usernameInput;
    public InputField passwordInput;
    public Button loginButton;
    public Image backgroundImage;
    public CanvasGroup mainCanvasGroup;
    public Text statusText; // 연결 상태 표시용 텍스트 (Unity UI Text)

    void Start()
    {
        // Fallback: loginButton이 Inspector에 연결되지 않았으면 동적으로 찾기
        if (loginButton == null)
        {
            loginButton = GetComponentInChildren<Button>();
            Debug.Log($"[LoginUI] loginButton was NULL, dynamically found: {(loginButton != null ? "SUCCESS" : "FAILED")}");
        }

        // Fallback: statusText가 Inspector에 연결되지 않았으면 동적으로 찾기
        if (statusText == null)
        {
            statusText = GetComponentInChildren<Text>();
            if (statusText == null)
            {
                // "Status"나 "Server"가 포함된 이름의 Text 찾기
                Text[] allTexts = GetComponentsInChildren<Text>(true);
                foreach (var txt in allTexts)
                {
                    if (txt.name.Contains("Status") || txt.name.Contains("Server") || txt.name.Contains("status"))
                    {
                        statusText = txt;
                        Debug.Log($"[LoginUI] statusText was NULL, dynamically found by name: {txt.name}");
                        break;
                    }
                }
                if (statusText == null)
                {
                    Debug.LogWarning("[LoginUI] statusText not found! Status display will not work.");
                }
            }
            else
            {
                Debug.Log("[LoginUI] statusText was NULL, dynamically found by GetComponentInChildren: SUCCESS");
            }
        }

        if (loginButton != null)
        {
            loginButton.onClick.AddListener(OnClickLogin);
            loginButton.interactable = false; // 시작 시 비활성화
        }
        else
        {
            Debug.LogError("[LoginUI] CRITICAL: loginButton not found in children!");
        }

        // 연출: 페이드 인
        if (mainCanvasGroup != null)
        {
            mainCanvasGroup.alpha = 0;
            StartCoroutine(FadeInUI());
        }

        // 네트워크 상태 변화 리스너 등록 (event subscription)
        NetworkManager.Instance.OnConnected += HandleOnConnected;
        NetworkManager.Instance.OnDisconnected += HandleOnDisconnected;

        UpdateStatusText();

        // 씬 시작 시 자동 연결 시도
        OnClickConnect();
    }

    void OnDestroy()
    {
        // [Fix] Unsubscribe from events to prevent MissingReferenceException
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

    public void UpdateStatusText()
    {
        bool connected = NetworkManager.Instance.IsConnected;

        // Debug 로그 추가
        Debug.Log($"[LoginUI] UpdateStatusText() called on '{gameObject.name}' in scene '{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}'. IsConnected={connected}");

        // Fallback: FindObjectOfType으로 찾은 LoginUI일 경우 loginButton이 null일 수 있음
        // GetComponentInChildren도 실패하면 씬 전체에서 Button을 찾음
        if (loginButton == null)
        {
            loginButton = GetComponentInChildren<Button>();
            if (loginButton == null)
            {
                // 씬 전체에서 "LoginButton"이라는 이름의 Button을 찾음
                GameObject btnObj = GameObject.Find("LoginButton");
                if (btnObj != null)
                {
                    loginButton = btnObj.GetComponent<Button>();
                    Debug.Log($"[LoginUI] UpdateStatusText: Found button by name: {(loginButton != null ? "SUCCESS" : "FAILED")}, Button on '{btnObj.name}'");
                }
                else
                {
                    // 최후 수단: 모든 Button 컴포넌트 찾기 (비효율적이지만 확실함)
                    Button[] allButtons = GameObject.FindObjectsOfType<Button>(true);
                    if (allButtons != null && allButtons.Length > 0)
                    {
                        foreach (var btn in allButtons)
                        {
                            if (btn.name.Contains("Login") || btn.name.Contains("login"))
                            {
                                loginButton = btn;
                                Debug.Log($"[LoginUI] UpdateStatusText: Found button by search: {btn.name}");
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                Debug.Log($"[LoginUI] UpdateStatusText: loginButton was NULL, dynamically found by GetComponentInChildren: SUCCESS");
            }
        }

        Debug.Log($"[LoginUI] UpdateStatusText called. IsConnected={connected}, loginButton={(loginButton != null ? "exists" : "NULL")}");

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
            Debug.Log($"[LoginUI] loginButton.interactable set to {connected}");
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
            Debug.LogError(
                "[LoginUI] Connection Check Failed! NetworkManager.IsConnected returned false."
            );
            // Poll check may have detected closed connection just now.
            UpdateStatusText();
            return;
        }

        string user = usernameInput != null ? usernameInput.text : "TestUser";
        string pass = passwordInput != null ? passwordInput.text : "Password123";

        Debug.Log(
            $"Sending C_Login for User: {user}, Password: {pass.Substring(0, Mathf.Min(pass.Length, 3))}..."
        );

        C_Login req = new C_Login() { Username = user, Password = pass };
        NetworkManager.Instance.Send(req);
    }
}
