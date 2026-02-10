using UnityEngine;

namespace Visual.Skills
{
    /// <summary>
    /// 플레이어 주변의 오라(마늘 등) 효과를 시각화합니다.
    /// </summary>
    public class AuraVisual : MonoBehaviour
    {
        [Header("Settings")]
        public SpriteRenderer auraRenderer;
        public float pulseSpeed = 2.0f;
        public float minAlpha = 0.2f;
        public float maxAlpha = 0.5f;

        private float _baseScale = 1.0f;

        private void Awake()
        {
            if (auraRenderer == null)
                auraRenderer = GetComponent<SpriteRenderer>();
        }

        public void SetRadius(float radius)
        {
            _baseScale = radius * 2.0f; // 반지름을 스케일로 변환
            transform.localScale = Vector3.one * _baseScale;
        }

        private void Update()
        {
            if (auraRenderer == null)
                return;

            // 펄스 효과 (알파값 변동)
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1.0f) * 0.5f;
            Color c = auraRenderer.color;
            c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
            auraRenderer.color = c;
        }
    }
}
