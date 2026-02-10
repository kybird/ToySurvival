using System.Collections.Generic;
using UnityEngine;

namespace Visual.Skills
{
    /// <summary>
    /// 플레이어 주변을 공전하는 무기(성서 등)의 비주얼을 관리합니다.
    /// </summary>
    public class OrbitVisual : MonoBehaviour
    {
        [Header("Settings")]
        public GameObject itemPrefab; // 공전할 아이템 프리팹
        public float radius = 2.0f;
        public float rotationSpeed = 180.0f; // 도/초

        private List<GameObject> _spawnedItems = new List<GameObject>();
        private int _currentCount = 0;
        private float _currentAngle = 0f;

        public void SetCount(int count)
        {
            if (_currentCount == count)
                return;

            _currentCount = count;
            UpdateVisuals();
        }

        private void Update()
        {
            _currentAngle += rotationSpeed * Time.deltaTime;
            if (_currentAngle >= 360f)
                _currentAngle -= 360f;

            UpdatePositions();
        }

        private void UpdateVisuals()
        {
            // 기존 아이템 제거
            foreach (var item in _spawnedItems)
            {
                if (item != null)
                    Destroy(item);
            }
            _spawnedItems.Clear();

            if (itemPrefab == null)
            {
                // 프리팹이 없으면 큐브로 대체 (디버그용)
                for (int i = 0; i < _currentCount; i++)
                {
                    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.SetParent(transform);
                    go.transform.localScale = Vector3.one * 0.4f;
                    _spawnedItems.Add(go);
                }
            }
            else
            {
                for (int i = 0; i < _currentCount; i++)
                {
                    GameObject go = Instantiate(itemPrefab, transform);
                    _spawnedItems.Add(go);
                }
            }

            UpdatePositions();
        }

        private void UpdatePositions()
        {
            if (_spawnedItems.Count == 0)
                return;

            float angleStep = 360f / _spawnedItems.Count;
            for (int i = 0; i < _spawnedItems.Count; i++)
            {
                float angle = (_currentAngle + (i * angleStep)) * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;

                _spawnedItems[i].transform.localPosition = new Vector3(x, y, 0);
            }
        }
    }
}
