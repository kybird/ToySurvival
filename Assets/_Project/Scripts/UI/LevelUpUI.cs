using System.Collections;
using System.Collections.Generic;
using Network;
using Protocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelUpUI : MonoBehaviour
{
    public static LevelUpUI Instance { get; private set; }

    [Header("References")]
    [SerializeField]
    private GameObject _panelContainer;

    [SerializeField]
    private Transform _cardsContainer;

    [SerializeField]
    private LevelUpOptionCard _cardPrefab;

    [SerializeField]
    private TextMeshProUGUI _timerText;

    [SerializeField]
    private CanvasGroup _canvasGroup;

    private List<LevelUpOptionCard> _spawnedCards = new List<LevelUpOptionCard>();
    private float _currentTimeout;
    private bool _isActive;
    private GameObject _aoeIndicator; // AoE visual indicator reference

    public bool IsActive => _isActive;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        _panelContainer.SetActive(false);
        if (_canvasGroup == null)
            _canvasGroup = _panelContainer.GetComponent<CanvasGroup>();
    }

    public void Show(List<LevelUpOption> options, float timeout, float slowRadius)
    {
        if (_isActive)
            return;
        _isActive = true;
        _currentTimeout = timeout;

        foreach (var card in _spawnedCards)
        {
            if (card != null)
                Destroy(card.gameObject);
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

        // [New] Draw AoE Indicator for LevelUp Slow Effect
        // 반경은 서버에서 전달받은 slowRadius 사용
        if (ObjectManager.Instance != null)
        {
            var myPlayer = ObjectManager.Instance.GetMyPlayer();
            if (myPlayer != null)
            {
                // 플레이어 위치에 장판 생성
                _aoeIndicator = Utils.AoEUtils.DrawAoE(
                    worldPos: myPlayer.transform.position,
                    radius: slowRadius,
                    color: new Color(0f, 1f, 1f, 0.3f), // Cyan 반투명
                    duration: timeout // UI 지속시간만큼 유지
                );
            }
        }
    }

    public void Hide()
    {
        _isActive = false;
        StopAllCoroutines();

        // Destroy AoE indicator immediately on level-up selection
        if (_aoeIndicator != null)
        {
            Destroy(_aoeIndicator);
            _aoeIndicator = null;
        }

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
                _panelContainer.transform.localScale = Vector3.Lerp(
                    Vector3.one * 0.8f,
                    Vector3.one,
                    t
                );
                yield return null;
            }
            _canvasGroup.alpha = 1;
            _panelContainer.transform.localScale = Vector3.one;
        }
    }
}
