using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    public static PlayerHUD Instance { get; private set; }

    [Header("HP")]
    public Slider hpBar;
    public Text hpText;

    [Header("Experience")]
    public Slider expBar;
    public Text levelText;

    void Awake()
    {
        Instance = this;
    }

    public void UpdateHP(int current, int max)
    {
        if (hpBar != null)
            hpBar.value = max > 0 ? (float)current / max : 0;
        if (hpText != null)
            hpText.text = $"{current} / {max}";
    }

    public void UpdateExp(int current, int max, int level)
    {
        if (expBar != null)
            expBar.value = max > 0 ? (float)current / max : 0;
        if (levelText != null)
            levelText.text = $"Lv.{level}";
    }
}