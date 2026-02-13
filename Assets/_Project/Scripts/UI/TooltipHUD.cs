using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TooltipHUD : MonoBehaviour
{
    public static TooltipHUD Instance { get; private set; }

    private RectTransform _rect;
    private TextMeshProUGUI _text;
    private Image _background;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            SetupUI();
            Hide();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void SetupUI()
    {
        _rect = GetComponent<RectTransform>();
        if (_rect == null)
            _rect = gameObject.AddComponent<RectTransform>();

        _rect.sizeDelta = new Vector2(200, 60);
        _rect.pivot = new Vector2(0, 1);

        _background = GetComponent<Image>();
        if (_background == null)
        {
            _background = gameObject.AddComponent<Image>();
            _background.color = new Color(0, 0, 0, 0.8f);
        }

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(transform, false);
        _text = textObj.AddComponent<TextMeshProUGUI>();
        _text.fontSize = 14;
        _text.alignment = TextAlignmentOptions.Center;
        _text.color = Color.white;

        RectTransform textRt = _text.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;
    }

    public void Show(string content, Vector2 position)
    {
        gameObject.SetActive(true);
        _text.text = content;
        transform.position = position + new Vector2(10, -10);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (gameObject.activeSelf)
        {
            transform.position = Input.mousePosition + new Vector3(15, -15, 0);
        }
    }
}
