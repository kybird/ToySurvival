using UnityEngine;

namespace Utils
{
    public static class AoETexCache
    {
        private static Texture2D _circleTex;

        public static Texture2D GetCircleTexture()
        {
            if (_circleTex != null)
                return _circleTex;

            const int size = 256;
            const float softEdge = 0.15f;

            _circleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _circleTex.wrapMode = TextureWrapMode.Clamp;

            float r = size * 0.5f;
            float inner = r * (1f - softEdge);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - r + 0.5f;
                float dy = y - r + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float a;
                if (dist <= inner)
                    a = 1f;
                else if (dist >= r)
                    a = 0f;
                else
                    a = 1f - (dist - inner) / (r - inner);

                _circleTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

            _circleTex.Apply();
            return _circleTex;
        }
    }

    public class AoELifetime : MonoBehaviour
    {
        private float _time;
        private float _duration;
        private SpriteRenderer _sr;
        private float _startAlpha;

        public void Init(float duration)
        {
            _duration = duration;
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null)
                _startAlpha = _sr.color.a;
        }

        void Update()
        {
            _time += Time.deltaTime;

            // duration이 0 이하면 무한 지속으로 간주할 수도 있으나, 여기서는 즉시 종료 방지용으로 안전하게 처리
            if (_duration <= 0)
                return;

            float t = _time / _duration;

            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            if (_sr != null)
            {
                Color c = _sr.color;
                c.a = Mathf.Lerp(_startAlpha, 0f, t);
                _sr.color = c;
            }
        }
    }

    public static class AoEUtils
    {
        public static GameObject DrawAoE(
            Vector2 worldPos,
            float radius,
            Color color,
            float duration,
            Transform parent = null
        )
        {
            GameObject go = new GameObject("AoE_Indicator");
            if (parent != null)
                go.transform.SetParent(parent);

            go.transform.position = worldPos;

            var sr = go.AddComponent<SpriteRenderer>();
            // Note: 프로젝트에 GroundEffect 레이어가 없다면 Default 레이어로 렌더링될 수 있습니다.
            // 필요시 Tag나 Layer를 동적으로 확인하여 설정할 수 있습니다.
            sr.sortingLayerName = "GroundEffect";
            sr.sortingOrder = 0; // 바닥에 깔리도록

            sr.sprite = Sprite.Create(
                AoETexCache.GetCircleTexture(),
                new Rect(0, 0, 256, 256),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 100
            );
            sr.color = color;

            // 반경 = 판정 반경 * 2 (지름)
            go.transform.localScale = Vector3.one * (radius * 2f);

            if (duration > 0f)
            {
                go.AddComponent<AoELifetime>().Init(duration);
            }

            return go;
        }
    }
}
