using TMPro;
using UnityEngine;

public class DamageText : MonoBehaviour
{
    private TextMeshPro _text;
    private float _moveSpeed = 1.0f;
    private float _fadeDuration = 0.5f;
    private float _lifeTime = 1.0f;
    private float _elapsed = 0.0f;
    private Color _startColor;

    void Awake()
    {
        _text = GetComponent<TextMeshPro>();
        if (_text == null)
            _text = gameObject.AddComponent<TextMeshPro>();

        _text.alignment = TextAlignmentOptions.Center;
        _text.fontSize = 4;
        _startColor = Color.white;
        _text.color = _startColor;
    }

    public void Setup(int damage)
    {
        _text.text = damage.ToString();
        _text.color = Color.red; // Default damage color
        _startColor = Color.red;
    }

    void Update()
    {
        _elapsed += Time.deltaTime;

        // Move up
        transform.position += Vector3.up * _moveSpeed * Time.deltaTime;

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
