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

        private static Texture2D _rectTex;

        public static Texture2D GetRectTexture()
        {
            if (_rectTex != null)
                return _rectTex;

            const int size = 64;
            _rectTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _rectTex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                _rectTex.SetPixel(x, y, Color.white);
            }

            _rectTex.Apply();
            return _rectTex;
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
            sr.sortingLayerName = "GroundEffect";
            sr.sortingOrder = 0;

            var tex = AoETexCache.GetCircleTexture();
            sr.sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: tex.width
            );
            sr.color = color;

            go.transform.localScale = Vector3.one * (radius * 2f);

            if (duration > 0f)
            {
                go.AddComponent<AoELifetime>().Init(duration);
            }

            return go;
        }

        public static GameObject DrawArcAoE(
            Vector2 worldPos,
            float radius,
            float arcDegrees,
            float rotationDegrees,
            Color color,
            float duration,
            Transform parent = null
        )
        {
            GameObject go = new GameObject("ArcAoE_Indicator");
            if (parent != null)
                go.transform.SetParent(parent);

            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.Euler(0, 0, rotationDegrees);

            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();

            // Setup Mesh
            Mesh mesh = new Mesh();
            int segments = 20;
            Vector3[] vertices = new Vector3[segments + 2];
            int[] triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            float angleStep = arcDegrees / segments;
            float startAngle = -arcDegrees / 2f;

            for (int i = 0; i <= segments; i++)
            {
                float angle = (startAngle + (i * angleStep)) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0
                );

                if (i < segments)
                {
                    triangles[i * 3 + 0] = 0;
                    triangles[i * 3 + 1] = i + 2;
                    triangles[i * 3 + 2] = i + 1;
                }
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mf.mesh = mesh;

            // Simple Material
            mr.material = new Material(Shader.Find("Sprites/Default"));
            mr.material.color = color;
            mr.sortingLayerName = "GroundEffect";

            if (duration > 0f)
            {
                go.AddComponent<AoELifetime>().Init(duration);
            }

            return go;
        }

        public static GameObject DrawRectAoE(
            Vector2 worldPos,
            float width,
            float height,
            float rotationDegrees,
            Color color,
            float duration,
            Transform parent = null
        )
        {
            GameObject go = new GameObject("RectAoE_Indicator");
            if (parent != null)
                go.transform.SetParent(parent);

            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.Euler(0, 0, rotationDegrees);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "GroundEffect";
            sr.sortingOrder = 0;

            var tex = AoETexCache.GetRectTexture();
            sr.sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: tex.width
            );
            sr.color = color;

            // SpriteRenderer 스케일 설정 (기본 1x1 텍스처 기준)
            go.transform.localScale = new Vector3(height, width, 1);

            if (duration > 0f)
            {
                go.AddComponent<AoELifetime>().Init(duration);
            }

            return go;
        }
    }
}
