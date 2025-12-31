using UnityEngine;
using UnityEngine.UI;
using Protocol;

public class RoomItem : MonoBehaviour
{
    public Text roomNameText;
    public Text playerCountText;

    private int _roomId;
    private Button _button;

    public void Setup(RoomInfo info)
    {
        _roomId = info.RoomId;

        if (roomNameText != null)
            roomNameText.text = string.IsNullOrEmpty(info.RoomTitle) ? $"Room {info.RoomId}" : info.RoomTitle;
        
        if (playerCountText != null)
            playerCountText.text = $"{info.CurrentPlayers}/{info.MaxPlayers}";

        // Ensure Button component exists
        _button = GetComponent<Button>();
        if (_button == null)
            _button = gameObject.AddComponent<Button>();

        // Ensure Image component exists (Required for RaycastTarget)
        Image bgImage = GetComponent<Image>();
        if (bgImage == null)
        {
            bgImage = gameObject.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.5f); // Semi-transparent black default
        }
        
        // Ensure Raycast is enabled
        bgImage.raycastTarget = true;

        if (_button.targetGraphic == null)
            _button.targetGraphic = bgImage;

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(OnClickItem);
    }

    void OnClickItem()
    {
        if (LobbyUI.Instance != null)
        {
            LobbyUI.Instance.SelectRoom(_roomId);
        }
    }

    public void SetSelected(bool isSelected)
    {
        if (_button != null && _button.targetGraphic != null)
        {
            _button.targetGraphic.color = isSelected ? Color.green : Color.white;
        }
    }

    public void UpdateSelectionState(int selectedRoomId)
    {
        SetSelected(_roomId == selectedRoomId);
    }
}
