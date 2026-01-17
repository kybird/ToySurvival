using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Protocol;
using Network;

public class LevelUpUI : MonoBehaviour
{
    public static LevelUpUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject _panelContainer;
    [SerializeField] private Transform _cardsContainer;
    [SerializeField] private LevelUpOptionCard _cardPrefab;
    [SerializeField] private TextMeshProUGUI _timerText;
    [SerializeField] private CanvasGroup _canvasGroup;

    private List<LevelUpOptionCard> _spawnedCards = new List<LevelUpOptionCard>();
    private float _currentTimeout;
    private bool _isActive;

    public bool IsActive => _isActive;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _panelContainer.SetActive(false);
        if (_canvasGroup == null) _canvasGroup = _panelContainer.GetComponent<CanvasGroup>();
    }

    public void Show(List<LevelUpOption> options, float timeout)
    {
        if (_isActive) return;
        _isActive = true;
        _currentTimeout = timeout;


        foreach (var card in _spawnedCards)
        {
            if (card != null) Destroy(card.gameObject);
        }
        _spawnedCards.Clear();

        if (options.Count > 0)
        {
            for (int i = 0; i < options.Count; i++)
            {
                var card = Instantiate(_cardPrefab, _cardsContainer);
                int index = i;
                card.Setup(options[i], i, (id) => OnOptionSelected(index));
                _spawnedCards.Add(card);
            }
        }

        _panelContainer.SetActive(true);
        StartCoroutine(AnimateIn());
        StartCoroutine(TimerRoutine());
    }

    public void Hide()
    {
        _isActive = false;
        StopAllCoroutines();
        
        _panelContainer.SetActive(false);
    }

    private void OnOptionSelected(int optionIndex)
    {
        SendSelection(optionIndex);
        Hide();
    }

    private void SendSelection(int optionIndex)
    {
        C_SelectLevelUp packet = new C_SelectLevelUp();
        packet.OptionIndex = optionIndex;
        NetworkManager.Instance.Send(packet);
    }

    private IEnumerator TimerRoutine()
    {
        while (_currentTimeout > 0)
        {
            _currentTimeout -= Time.unscaledDeltaTime;
            if (_timerText != null)
                _timerText.text = Mathf.CeilToInt(_currentTimeout).ToString();
            
            yield return null;
        }
        
        OnOptionSelected(0);
    }

    private IEnumerator AnimateIn()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0;
            _panelContainer.transform.localScale = Vector3.one * 0.8f;

            float t = 0;
            while (t < 1)
            {
                t += Time.unscaledDeltaTime * 4;
                _canvasGroup.alpha = t;
                _panelContainer.transform.localScale = Vector3.Lerp(Vector3.one * 0.8f, Vector3.one, t);
                yield return null;
            }
            _canvasGroup.alpha = 1;
            _panelContainer.transform.localScale = Vector3.one;
        }
    }
}
