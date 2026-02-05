using System.Collections.Generic;
using Protocol;
using UnityEngine;
using UnityEngine.UI;

public class InventoryHUD : MonoBehaviour
{
    public static InventoryHUD Instance { get; private set; }

    [Header("Layout")]
    public Transform weaponContainer;
    public Transform passiveContainer;
    public GameObject iconPrefab; // Image(Icon) + Text(Level)

    private Dictionary<int, GameObject> _weaponIcons = new Dictionary<int, GameObject>();
    private Dictionary<int, GameObject> _passiveIcons = new Dictionary<int, GameObject>();

    void Awake()
    {
        Instance = this;
    }

    public void UpdateInventory(S_UpdateInventory msg)
    {
        foreach (var item in msg.Items)
        {
            if (item.IsPassive)
            {
                UpdateItem(_passiveIcons, passiveContainer, item);
            }
            else
            {
                UpdateItem(_weaponIcons, weaponContainer, item);
            }
        }
    }

    private void UpdateItem(
        Dictionary<int, GameObject> dict,
        Transform container,
        InventoryItem item
    )
    {
        if (dict.TryGetValue(item.Id, out GameObject go))
        {
            // Update Level
            var levelText = go.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (levelText != null)
                levelText.text = item.Level.ToString();
        }
        else
        {
            // Create New Icon
            if (iconPrefab != null && container != null)
            {
                GameObject newIcon = Instantiate(iconPrefab, container);
                dict.Add(item.Id, newIcon);

                // Set Icon Sprite (Resource load logic should go here)
                // string iconPath = item.IsPassive ? $"Icons/Passive_{item.Id}" : $"Icons/Weapon_{item.Id}";
                // var sprite = Resources.Load<Sprite>(iconPath);
                // if (sprite != null) newIcon.GetComponent<Image>().sprite = sprite;

                var levelText = newIcon.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (levelText != null)
                    levelText.text = item.Level.ToString();
            }
        }
    }
}
