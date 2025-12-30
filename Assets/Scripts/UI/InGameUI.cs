using UnityEngine;
using UnityEngine.UI;

public class InGameUI : MonoBehaviour
{
    [Header("Stats UI")]
    public Slider healthBar;
    public Text healthText;
    public Text ammoText;
    
    [Header("Panels")]
    public CanvasGroup hudCanvasGroup;
    public GameObject damageOverlay;

    void Start()
    {
        // 초기 연출: HUD 자연스럽게 나타나기
        if (hudCanvasGroup != null)
        {
            hudCanvasGroup.alpha = 0;
            StartCoroutine(FadeInHUD());
        }
    }

    System.Collections.IEnumerator FadeInHUD()
    {
        float duration = 0.5f;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            hudCanvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
    }

    public void UpdateHealth(float current, float max)
    {
        if (healthBar != null)
            healthBar.value = current / max;
        
        if (healthText != null)
            healthText.text = $"HP: {current} / {max}";
            
        if (current < max * 0.3f)
        {
            // 체력이 낮을 때 경고 효과 (Damage Overlay 활성화 등)
            if (damageOverlay != null) damageOverlay.SetActive(true);
        }
    }

    public void UpdateAmmo(int current, int max)
    {
        if (ammoText != null)
            ammoText.text = $"AMMO: {current} / {max}";
    }
}
