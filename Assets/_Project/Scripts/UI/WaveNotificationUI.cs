using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WaveNotificationUI : MonoBehaviour
{
    public static WaveNotificationUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private Image _timerBar;

    [Header("Settings")]
    [SerializeField] private float _fadeInDuration = 0.5f;
    [SerializeField] private float _fadeOutDuration = 0.5f;

    private Coroutine _displayCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Initialize state - hidden
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }
    }

    public void Show(string title, float duration)
        {
            if (_displayCoroutine != null)
            {
                StopCoroutine(_displayCoroutine);
            }

            _displayCoroutine = StartCoroutine(DisplayRoutine(title, duration));
        }

        private IEnumerator DisplayRoutine(string title, float duration)
        {
            if (_titleText != null)
            {
                _titleText.text = title;
            }

            if (_timerBar != null)
            {
                _timerBar.fillAmount = 1f;
            }

            if (_canvasGroup != null)
            {
                float elapsed = 0f;
                while (elapsed < _fadeInDuration)
                {
                    elapsed += Time.deltaTime;
                    _canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / _fadeInDuration);
                    yield return null;
                }
                _canvasGroup.alpha = 1f;
            }

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                
                if (_timerBar != null)
                {
                    float progress = Mathf.Clamp01(1f - (timer / duration));
                    _timerBar.fillAmount = progress;
                }

                yield return null;
            }

            if (_timerBar != null)
            {
                _timerBar.fillAmount = 0f;
            }

            if (_canvasGroup != null)
            {
                float elapsed = 0f;
                while (elapsed < _fadeOutDuration)
                {
                    elapsed += Time.deltaTime;
                    _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / _fadeOutDuration);
                    yield return null;
                }
                _canvasGroup.alpha = 0f;
            }

            _displayCoroutine = null;
    }
}
