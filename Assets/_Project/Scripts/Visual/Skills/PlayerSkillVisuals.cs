using System.Collections.Generic;
using Protocol;
using UnityEngine;

namespace Visual.Skills
{
    /// <summary>
    /// 플레이어의 인벤토리 상태에 따라 무기 비주얼을 활성화/비활성화합니다.
    /// </summary>
    public class PlayerSkillVisuals : MonoBehaviour
    {
        [Header("Orbit (Bible)")]
        public OrbitVisual orbitVisual;
        public int bibleId = 3; // 성서 ID

        [Header("Aura (Garlic)")]
        public AuraVisual auraVisual;
        public int garlicId = 4; // 마늘 ID (임시)

        private void Start()
        {
            // 초기 상태는 비활성
            if (orbitVisual != null)
                orbitVisual.gameObject.SetActive(false);
            if (auraVisual != null)
                auraVisual.gameObject.SetActive(false);
        }

        public void Refresh(List<InventoryItem> items)
        {
            bool hasBible = false;
            int bibleLevel = 0;
            bool hasGarlic = false;
            int garlicLevel = 0;

            foreach (var item in items)
            {
                if (item.IsPassive)
                    continue;

                if (item.Id == bibleId)
                {
                    hasBible = true;
                    bibleLevel = item.Level;
                }
                else if (item.Id == garlicId)
                {
                    hasGarlic = true;
                    garlicLevel = item.Level;
                }
            }

            // 성서(Orbit) 업데이트
            if (orbitVisual != null)
            {
                orbitVisual.gameObject.SetActive(hasBible);
                if (hasBible)
                {
                    // 레벨에 따라 개수 조정 (예: 1+Level)
                    orbitVisual.SetCount(1 + bibleLevel);
                }
            }

            // 마늘(Aura) 업데이트
            if (auraVisual != null)
            {
                auraVisual.gameObject.SetActive(hasGarlic);
                if (hasGarlic)
                {
                    // 레벨에 따라 반지름 조정
                    auraVisual.SetRadius(2.0f + garlicLevel * 0.5f);
                }
            }
        }
    }
}
