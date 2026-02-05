using UnityEngine;

public class HpBar : MonoBehaviour
{
    private Transform _bar;
    private float _maxHp = 100;
    private float _currentHp = 100;
    private float _showTimer = 0.0f;
    private const float SHOW_DURATION = 3.0f;

    private GameObject _visualRoot;

    void Awake()
    {
        // 시각적 요소들을 담고 있는 자식 오브젝트를 찾습니다.
        // 프리팹 구조가 HpBar(Root) -> Visuals -> [Background, Fill] 이라고 가정합니다.
        // 만약 자식이 하나뿐이라면 그 자식을 시각적 루트로 삼습니다.
        if (transform.childCount > 0)
        {
            _visualRoot = transform.GetChild(0).gameObject;
        }

        Transform fill = transform.Find("Fill");
        if (fill == null && _visualRoot != null)
        {
            fill = _visualRoot.transform.Find("Fill");
        }

        if (fill != null)
        {
            _bar = fill;
        }

        // 초기에는 숨김 처리
        SetVisible(false);
    }

    public void Init(float currentHp, float maxHp)
    {
        _currentHp = currentHp;
        _maxHp = maxHp;
        UpdateVisual();

        // 초기화 시 숨김
        SetVisible(false);
    }

    public void SetHp(float hp, float maxHp)
    {
        // 데미지를 입었을 때만 3초간 표시
        if (hp < _currentHp)
        {
            _showTimer = SHOW_DURATION;
            SetVisible(true);
        }

        _currentHp = hp;
        _maxHp = maxHp;
        UpdateVisual();
    }

    private void SetVisible(bool visible)
    {
        if (_visualRoot != null)
        {
            _visualRoot.SetActive(visible);
        }
        else
        {
            // 루트의 자식이 없는 경우 모든 child를 토글하거나
            // SpriteRenderer를 끄는 방식 등으로 대응할 수 있습니다.
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(visible);
            }
        }
    }

    private void UpdateVisual()
    {
        if (_bar == null)
            return;

        float ratio = Mathf.Clamp01(_currentHp / _maxHp);
        _bar.localScale = new Vector3(ratio, 1, 1);

        SpriteRenderer sr = _bar.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = Color.Lerp(Color.red, Color.green, ratio);
        }
    }

    void Update()
    {
        if (_showTimer > 0)
        {
            _showTimer -= Time.deltaTime;
            if (_showTimer <= 0)
            {
                SetVisible(false);
            }
        }
    }

    void LateUpdate()
    {
        // 빌보드 효과
        transform.rotation = Quaternion.identity;
    }
}
