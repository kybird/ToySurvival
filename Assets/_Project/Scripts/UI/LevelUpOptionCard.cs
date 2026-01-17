using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using Protocol;

public class LevelUpOptionCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private static Sprite GetDefaultSprite()
    {
        return Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
    }

    private static void EnsureImageHasSprite(Image image)
    {
        if (image == null)
            return;

        if (image.sprite == null)
            image.sprite = GetDefaultSprite();
    }

    private static void EnsureImageIsVisible(Image image)
    {
        if (image == null)
            return;

        if (image.color.a <= 0.001f)
        {
            var c = image.color;
            c.a = 1f;
            image.color = c;
        }
    }

    private static void EnsureTextIsVisible(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        if (text.color.a <= 0.001f)
        {
            var c = text.color;
            c.a = 1f;
            text.color = c;
        }
    }

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private Image _iconImage;
    [SerializeField] private GameObject _newBadge;
    [SerializeField] private Image _backgroundImage;

    [Header("Styling")]
    [SerializeField] private Color _upgradeColor = new Color(0.15f, 0.15f, 0.25f, 0.95f);
    [SerializeField] private Color _newSkillColor = new Color(0.35f, 0.25f, 0.15f, 0.95f);
    [SerializeField] private float _hoverScale = 1.05f;
    [SerializeField] private float _animSpeed = 15f;

    private int _optionId;
    private System.Action<int> _onSelected;
    private Vector3 _originalScale;
    private Coroutine _scaleCoroutine;
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _originalScale = transform.localScale;
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (_backgroundImage == null)
            _backgroundImage = GetComponent<Image>();
    }

    public void Setup(LevelUpOption option, int index, System.Action<int> onSelected)
    {
        _optionId = option.OptionId;
        _onSelected = onSelected;

        EnsureTextIsVisible(_nameText);
        EnsureTextIsVisible(_descriptionText);
        EnsureImageHasSprite(_backgroundImage);
        EnsureImageHasSprite(_iconImage);
        EnsureImageIsVisible(_backgroundImage);
        EnsureImageIsVisible(_iconImage);

        if (_nameText != null) _nameText.text = option.Name;
        if (_descriptionText != null) _descriptionText.text = option.Desc;

        if (_newBadge != null)
            _newBadge.SetActive(option.IsNew);

        if (_backgroundImage != null)
            _backgroundImage.color = option.IsNew ? _newSkillColor : _upgradeColor;

        if (_canvasGroup != null) _canvasGroup.alpha = 1f;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
        _scaleCoroutine = StartCoroutine(ScaleTo(_originalScale * _hoverScale));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
        _scaleCoroutine = StartCoroutine(ScaleTo(_originalScale));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _onSelected?.Invoke(_optionId);
    }

    private IEnumerator ScaleTo(Vector3 target)
    {
        while (Vector3.Distance(transform.localScale, target) > 0.001f)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, target, Time.unscaledDeltaTime * _animSpeed);
            yield return null;
        }
        transform.localScale = target;
    }
}
