using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로딩 UI를 관리하는 싱글톤 매니저.
/// 씬 전환 시 로딩 화면을 표시합니다.
/// </summary>
public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Text messageText;
    [SerializeField] private Slider progressBar;

    private bool _isCreatedDynamically = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 동적 생성 시 UI 생성
            if (loadingPanel == null)
            {
                CreateLoadingUI();
            }
            
            Hide();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 로딩 UI를 동적으로 생성합니다 (프리팹 없이 코드로 생성).
    /// </summary>
    private void CreateLoadingUI()
    {
        _isCreatedDynamically = true;

        // Canvas 생성
        GameObject canvasGO = new GameObject("LoadingCanvas");
        canvasGO.transform.SetParent(transform);
        
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // 최상단

        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        // 패널 (배경)
        loadingPanel = new GameObject("LoadingPanel");
        loadingPanel.transform.SetParent(canvasGO.transform, false);
        
        RectTransform panelRect = loadingPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImage = loadingPanel.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.85f);

        // 메시지 텍스트
        GameObject textGO = new GameObject("MessageText");
        textGO.transform.SetParent(loadingPanel.transform, false);
        
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.55f);
        textRect.anchorMax = new Vector2(0.5f, 0.55f);
        textRect.sizeDelta = new Vector2(600, 50);

        messageText = textGO.AddComponent<Text>();
        messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        messageText.fontSize = 32;
        messageText.alignment = TextAnchor.MiddleCenter;
        messageText.color = Color.white;
        messageText.text = "Loading...";

        // 프로그레스 바
        GameObject sliderGO = new GameObject("ProgressBar");
        sliderGO.transform.SetParent(loadingPanel.transform, false);
        
        RectTransform sliderRect = sliderGO.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.45f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.45f);
        sliderRect.sizeDelta = new Vector2(400, 20);

        progressBar = sliderGO.AddComponent<Slider>();
        progressBar.interactable = false;
        progressBar.minValue = 0f;
        progressBar.maxValue = 1f;

        // 슬라이더 배경
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderGO.transform, false);
        RectTransform bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // 슬라이더 Fill Area
        GameObject fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(5, 5);
        fillAreaRect.offsetMax = new Vector2(-5, -5);

        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        RectTransform fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImage = fillGO.AddComponent<Image>();
        fillImage.color = new Color(0.2f, 0.6f, 1f, 1f);

        progressBar.fillRect = fillRect;
    }

    /// <summary>
    /// 로딩 UI를 표시합니다.
    /// </summary>
    public void Show(string message = "Loading...")
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }

        if (messageText != null)
        {
            messageText.text = message;
        }

        if (progressBar != null)
        {
            progressBar.value = 0f;
        }
    }

    /// <summary>
    /// 로딩 UI를 숨깁니다.
    /// </summary>
    public void Hide()
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 로딩 진행률을 설정합니다.
    /// </summary>
    public void SetProgress(float progress)
    {
        if (progressBar != null)
        {
            progressBar.value = Mathf.Clamp01(progress);
        }
    }

    /// <summary>
    /// 메시지를 업데이트합니다.
    /// </summary>
    public void SetMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }
    }
}
