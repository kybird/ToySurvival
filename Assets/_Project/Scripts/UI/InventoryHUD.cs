using System.Collections;
using System.Collections.Generic;
using Protocol;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어의 인벤토리(무기, 패시브) 상태를 화면에 표시하는 UI 클래스
/// </summary>
public class InventoryHUD : MonoBehaviour
{
    private static InventoryHUD _instance;
    public static InventoryHUD Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<InventoryHUD>();
                if (_instance == null)
                {
                    Debug.LogWarning("[InventoryHUD] 씬에 인벤토리 HUD가 배치되어 있지 않습니다.");
                }
            }
            return _instance;
        }
    }

    [Header("레이아웃 설정")]
    [SerializeField]
    private RectTransform _weaponContainer;

    [SerializeField]
    private RectTransform _passiveContainer;

    [SerializeField]
    private Image _iconPrefab;

    private List<Image> _weaponIcons = new List<Image>();
    private List<Image> _passiveIcons = new List<Image>();

    private void Awake()
    {
        if (_instance == null)
            _instance = this;
    }

    /// <summary>
    /// 서버로부터 받은 인벤토리 정보를 기반으로 UI를 갱신합니다.
    /// </summary>
    public void UpdateInventory(S_UpdateInventory msg)
    {
        ClearIcons();

        if (msg == null || msg.Items == null)
            return;

        foreach (var item in msg.Items)
        {
            RectTransform container = item.IsPassive ? _passiveContainer : _weaponContainer;
            List<Image> list = item.IsPassive ? _passiveIcons : _weaponIcons;

            if (container == null || _iconPrefab == null)
                continue;

            Image iconImg = Instantiate(_iconPrefab, container);
            iconImg.sprite = GetIconSprite(item.Id, item.IsPassive);

            var text = iconImg.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (text != null)
                text.text = $"Lv.{item.Level}";
            else
            {
                var legacyText = iconImg.GetComponentInChildren<Text>();
                if (legacyText != null)
                    legacyText.text = $"v{item.Level}";
            }

            list.Add(iconImg);
        }
    }

    private void ClearIcons()
    {
        foreach (var icon in _weaponIcons)
            if (icon != null)
                Destroy(icon.gameObject);
        foreach (var icon in _passiveIcons)
            if (icon != null)
                Destroy(icon.gameObject);
        _weaponIcons.Clear();
        _passiveIcons.Clear();
    }

    private Sprite GetIconSprite(int id, bool isPassive)
    {
        string iconName = "";

        if (isPassive)
        {
            switch (id)
            {
                case 1:
                    iconName = "Icon_Exp";
                    break; // Spinach
                case 2:
                    iconName = "Icon_Heart";
                    break; // Hollow Heart
                case 3:
                    iconName = "Icon_Level";
                    break; // Wings
                case 4:
                    iconName = "Icon_Level";
                    break; // Empty Tome
                case 5:
                    iconName = "Icon_Level";
                    break; // Candelabrador
                case 6:
                    iconName = "Icon_Level";
                    break; // Spellbinder
                case 7:
                    iconName = "Icon_Level";
                    break; // Duplicator
                case 11:
                    iconName = "Icon_Level";
                    break; // Magic Bindi
                default:
                    iconName = "Icon_Level";
                    break;
            }
        }
        else
        {
            switch (id)
            {
                case 1:
                    iconName = "dagger";
                    break;
                case 2:
                    iconName = "dart";
                    break;
                case 3:
                    iconName = "MagicBolt";
                    break;
                case 4:
                    iconName = "dagger";
                    break; // Greatsword fallback
                default:
                    iconName = "MagicBolt";
                    break;
            }
        }

        Sprite sprite = Resources.Load<Sprite>($"Textures/{iconName}");
        if (sprite == null)
            sprite = Resources.Load<Sprite>($"_Project/Resources/Textures/{iconName}");

        if (sprite == null)
        {
            Debug.LogWarning(
                $"[InventoryHUD] 리소스 로드 실패: {iconName} (ID: {id}, Passive: {isPassive})"
            );
        }

        return sprite;
    }
}
