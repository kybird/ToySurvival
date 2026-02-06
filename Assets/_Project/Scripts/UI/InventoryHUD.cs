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

        SetupProceduralUI();
    }

    private void SetupProceduralUI()
    {
        // 1. Canvas 확인 및 자신을 Canvas 밑으로 배치
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("MainCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
                Debug.Log("[InventoryHUD] MainCanvas를 찾을 수 없어 새로 생성했습니다.");
            }
            transform.SetParent(canvas.transform, false);
        }

        // 2. 인벤토리 HUD 자체의 위치 설정 (상단 배치를 기본으로 함)
        RectTransform rect = GetComponent<RectTransform>();
        if (rect == null)
            rect = gameObject.AddComponent<RectTransform>();

        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(20, -20);
        rect.sizeDelta = new Vector2(800, 100);

        // 3. 컨테이너 자동 탐색 및 생성
        if (_weaponContainer == null)
            _weaponContainer = CreateContainer("WeaponContainer", new Vector2(0, 0));

        if (_passiveContainer == null)
            _passiveContainer = CreateContainer("PassiveContainer", new Vector2(0, -50));
    }

    private RectTransform CreateContainer(string name, Vector2 pos)
    {
        Transform t = transform.Find(name);
        if (t == null)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(this.transform, false);
            t = obj.transform;
        }

        RectTransform rt = t.GetComponent<RectTransform>();
        if (rt == null)
            rt = t.gameObject.AddComponent<RectTransform>();

        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(600, 45);

        HorizontalLayoutGroup layout = t.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
        {
            layout = t.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 10;
        }

        return rt;
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

            if (container == null)
            {
                Debug.LogWarning(
                    $"[InventoryHUD] {(item.IsPassive ? "Passive" : "Weapon")} 컨테이너가 설정되지 않았습니다."
                );
                continue;
            }

            Image iconImg;
            if (_iconPrefab != null)
            {
                iconImg = Instantiate(_iconPrefab, container);
            }
            else
            {
                // [Procedural] 프리팹이 없는 경우 즉석 생성
                iconImg = CreateIconObject(container);
            }

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

    private Image CreateIconObject(Transform parent)
    {
        GameObject obj = new GameObject("Icon");
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(40, 40);

        Image img = obj.AddComponent<Image>();
        img.raycastTarget = false;

        // 레벨 텍스트 생성
        GameObject textObj = new GameObject("LevelText");
        textObj.transform.SetParent(obj.transform, false);

        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0, 0);
        textRt.anchorMax = new Vector2(1, 0.4f);
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.fontSize = 12;
        tmp.alignment = TMPro.TextAlignmentOptions.BottomRight;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        // 외곽선 효과 추가 (가시성)
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = Color.black;

        return img;
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
                default:
                    iconName = "Icon_Level";
                    break; // Common fallback
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

        // [C1] 리소스 가이드에 따른 경로 수정 (Assets/_Project/Resources/Textures/)
        Sprite sprite = Resources.Load<Sprite>($"Textures/{iconName}");

        if (sprite == null)
        {
            Debug.LogWarning(
                $"[InventoryHUD] 리소스 로드 실패: Textures/{iconName} (ID: {id}, Passive: {isPassive})"
            );
        }

        return sprite;
    }
}
