using TMPro;
using UnityEngine;

public class DamageText : MonoBehaviour
{
    private TextMeshPro _text;
    private float _moveSpeed = 1.5f;
    private float _fadeDuration = 0.4f;
    private float _lifeTime = 0.8f;
    private float _elapsed = 0.0f;
    private Color _startColor;

    // Scale animation
    private Vector3 _startScale = Vector3.zero;
    private Vector3 _midScale = new Vector3(1.2f, 1.2f, 1.2f);
    private Vector3 _endScale = Vector3.one;

    void Awake()
    {
        _text = GetComponent<TextMeshPro>();
        if (_text == null)
            _text = gameObject.AddComponent<TextMeshPro>();

        _text.alignment = TextAlignmentOptions.Center;
        _text.fontSize = 4;
        _text.outlineWidth = 0.2f;
        _text.outlineColor = Color.black;

        transform.localScale = _startScale;
    }

    public void Setup(int damage, bool isCritical = false)
    {
        _text.text = damage.ToString();

        if (isCritical)
        {
            _text.fontSize = 6;
            _text.color = new Color(1.0f, 0.8f, 0.0f); // Golden/Yellow
            _startColor = _text.color;
            _midScale = new Vector3(2.0f, 2.0f, 2.0f);
            _endScale = new Vector3(1.5f, 1.5f, 1.5f);
            _moveSpeed = 2.5f; // Pops up faster
        }
        else
        {
            _text.fontSize = 4;
            _text.color = Color.white;
            _startColor = Color.white;
        }
    }

    void Update()
    {
        _elapsed += Time.deltaTime;

        // Position movement
        transform.position += Vector3.up * _moveSpeed * Time.deltaTime;

        // Scale animation (0 -> Mid -> End)
        float progress = _elapsed / _lifeTime;
        if (progress < 0.2f) // Pop up phase
        {
            transform.localScale = Vector3.Lerp(_startScale, _midScale, progress / 0.2f);
        }
        else // Settle phase
        {
            transform.localScale = Vector3.Lerp(_midScale, _endScale, (progress - 0.2f) / 0.8f);
        }

        // Fade out
        if (_elapsed > (_lifeTime - _fadeDuration))
        {
            float alpha = 1.0f - ((_elapsed - (_lifeTime - _fadeDuration)) / _fadeDuration);
            Color c = _startColor;
            c.a = alpha;
            _text.color = c;
        }

        if (_elapsed >= _lifeTime)
        {
            Destroy(gameObject);
        }
    }
}
