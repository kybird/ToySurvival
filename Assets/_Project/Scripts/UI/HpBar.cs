using UnityEngine;

public class HpBar : MonoBehaviour
{
    private Transform _bar;
    private float _maxHp = 100;
    private float _currentHp = 100;

    void Awake()
    {
        // Simple structure expectation:
        // HpBar (Empty)
        //   - Background (Sprite)
        //   - Fill (Sprite) -> Assigned to _bar

        // Auto-find 'Fill' child if exists, or create simple visuals if not (for MVP)
        Transform fill = transform.Find("Fill");
        if (fill != null)
        {
            _bar = fill;
        }
        else
        {
            // If manual setup missing, this script might not work fully without prefab setup.
            // But we assume the user will set up the prefab.
        }
    }

    public void Init(float currentHp, float maxHp)
    {
        _currentHp = currentHp;
        _maxHp = maxHp;
        UpdateVisual();
    }

    public void SetHp(float hp, float maxHp)
    {
        _currentHp = hp;
        _maxHp = maxHp;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (_bar == null)
            return;

        float ratio = Mathf.Clamp01(_currentHp / _maxHp);
        _bar.localScale = new Vector3(ratio, 1, 1);

        // Optional: Color change based on HP
        SpriteRenderer sr = _bar.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = Color.Lerp(Color.red, Color.green, ratio);
        }
    }

    void LateUpdate()
    {
        // Billboard effect (if map rotates, but 2D usually doesn't need this unless rotating camera)
        transform.rotation = Quaternion.identity;
    }
}
