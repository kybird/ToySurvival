using UnityEngine;
using UnityEngine.UI;
using Protocol;

public class LobbyUI : MonoBehaviour
{
    public static LobbyUI Instance { get; private set; }

    [Header("UI References")]
    public Button enterRoomButton; // 기존 버튼 (테스트용)
    public Text statusText;
    public CanvasGroup lobbyCanvasGroup;

    [Header("Room List UI")]
    public Transform roomListContent;
    public GameObject roomItemPrefab;
    public Button refreshButton;
    public Button createRoomButton; // 팝업 열기 버튼

    [Header("Create Room Popup")]
    public GameObject createRoomPopup;
    public InputField roomTitleInput;
    public Button confirmCreateButton;
    public Button cancelCreateButton;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Debug.Log("[LobbyUI] Start Initializing...");

        if (enterRoomButton != null)
        {
            enterRoomButton.onClick.RemoveAllListeners();
            enterRoomButton.onClick.AddListener(OnClickEnterRoom);
        }
        else Debug.LogError("[LobbyUI] enterRoomButton is NULL");

        if (refreshButton != null)
            refreshButton.onClick.AddListener(SendGetRoomList);
        else Debug.LogError("[LobbyUI] refreshButton is NULL");

        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(OpenCreateRoomPopup);
        else Debug.LogError("[LobbyUI] createRoomButton is NULL");

        if (confirmCreateButton != null)
            confirmCreateButton.onClick.AddListener(SendCreateRoom);
        
        if (cancelCreateButton != null)
            cancelCreateButton.onClick.AddListener(CloseCreateRoomPopup);

        if (confirmCreateButton != null)
            confirmCreateButton.onClick.AddListener(SendCreateRoom);
        
        if (cancelCreateButton != null)
            cancelCreateButton.onClick.AddListener(CloseCreateRoomPopup);

        // Ensure popup is hidden initially
        if (createRoomPopup != null)
        {
            createRoomPopup.SetActive(false);
        }
        else Debug.LogError("[LobbyUI] createRoomPopup is NULL");

        if (statusText != null)
            statusText.text = "Logged In. Ready.";
            
        // 연출: 페이드 인
        if (lobbyCanvasGroup != null)
        {
            lobbyCanvasGroup.alpha = 0;
            StartCoroutine(FadeInUI());
        }

        // 시작 시 방 목록 갱신 요청
        SendGetRoomList();
    }

    System.Collections.IEnumerator FadeInUI()
    {
        float duration = 0.8f;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            lobbyCanvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
    }

    // 선택된 방 ID (-1이면 선택 안 함)
    private int _selectedRoomId = -1;
    // 생성된 RoomItem 리스트 관리
    private System.Collections.Generic.List<RoomItem> _roomItems = new System.Collections.Generic.List<RoomItem>();

    public void SelectRoom(int roomId)
    {
        _selectedRoomId = roomId;
        Debug.Log($"[LobbyUI] Room Selected: {_selectedRoomId}");
        
        // 아이템들의 선택 상태 갱신
        foreach (var item in _roomItems)
        {
            // 방 ID가 프로퍼티로 노출되지 않았으므로 RoomItem에 Getter가 필요하거나 
            // _selectedRoomId와 비교할 수 있는 메서드가 필요하지만,
            // 간단하게 RoomItem 내부에서 RoomId를 확인하거나, 
            // 여기서는 RoomItem에 public RoomId Getter를 추가하는게 정석입니다.
            // 하지만 일단 RoomItem 코드를 수정했으니, 아래 UpdateRoomList에서 리스트를 관리하고
            // 여기서 순회하며 처리하겠습니다.
            
            // RoomItem에 public RoomId 프로퍼티가 없으므로 CheckSelection 메서드 추가 필요
            // 혹은 Setup 시점에 저장해둔 Dictionary 사용
        }

        // RoomItem에 CheckSelected 메서드를 추가하는 편이 빠르겠네요.
        // 일단 UI 텍스트 갱신
        if (statusText != null)
            statusText.text = $"Selected Room: {_selectedRoomId}. Press 'Enter Room'.";
            
        RefreshSelectionVisuals();
    }
    
    void RefreshSelectionVisuals()
    {
        foreach (var item in _roomItems)
        {
            if (item != null) item.UpdateSelectionState(_selectedRoomId);
        }
    }

    // Enter Room 버튼 클릭 시 호출
    public void OnClickEnterRoom()
    {
        if (_selectedRoomId == -1)
        {
            Debug.Log("[LobbyUI] No room selected.");
            // if (statusText != null) statusText.text = "Please select a room first.";
            return;
        }

        SendJoinRoom(_selectedRoomId);
    }

    // --- Packet Sending Request ---

    public void SendGetRoomList()
    {
        // 갱신 시 선택 초기화
        _selectedRoomId = -1; 
        
        C_GetRoomList req = new C_GetRoomList();
        req.OnlyJoinable = false;
        NetworkManager.Instance.Send(req);
        // Debug.Log("[LobbyUI] Sent C_GetRoomList");
    }

    public void SendCreateRoom()
    {
        string title = "My Room";
        if (roomTitleInput != null && !string.IsNullOrEmpty(roomTitleInput.text))
            title = roomTitleInput.text;

        C_CreateRoom req = new C_CreateRoom();
        req.WavePatternId = 1; // Default
        req.RoomTitle = title;
        
        NetworkManager.Instance.Send(req);
        Debug.Log($"[LobbyUI] Sent C_CreateRoom: {title}");

        CloseCreateRoomPopup();
    }

    public void SendJoinRoom(int roomId)
    {
        Debug.Log($"[LobbyUI] Sending C_JoinRoom: {roomId}");
        C_JoinRoom req = new C_JoinRoom();
        req.RoomId = roomId;
        NetworkManager.Instance.Send(req);

        if (statusText != null)
            statusText.text = $"Joining Room {roomId}...";
    }

    // --- Packet Handling Callbacks ---

    public void UpdateRoomList(System.Collections.Generic.IList<RoomInfo> rooms)
    {
        if (roomListContent == null || roomItemPrefab == null) return;

        // 기존 목록 삭제
        foreach (Transform child in roomListContent)
            Destroy(child.gameObject);
        
        _roomItems.Clear();

        // 새 목록 생성
        foreach (RoomInfo r in rooms)
        {
            GameObject go = Instantiate(roomItemPrefab, roomListContent);
            RoomItem item = go.GetComponent<RoomItem>();
            if (item != null)
            {
                item.Setup(r);
                _roomItems.Add(item);
            }
            else
            {
                Debug.LogWarning($"[LobbyUI] RoomItem component missing on instantiated object. Adding it automatically.");
                item = go.AddComponent<RoomItem>();
                item.Setup(r);
                _roomItems.Add(item);
            }
        }
        Debug.Log($"[LobbyUI] Updated Room List: {rooms.Count} rooms");
        
        // 목록 갱신 후 선택 상태 초기화 (또는 유지하려면 여기서 처리)
        _selectedRoomId = -1;
        if (statusText != null) statusText.text = "Room List Updated.";
    }

    public void OnCreateRoomSuccess(int roomId)
    {
        Debug.Log($"[LobbyUI] Created Room {roomId} Successfully!");
        if (statusText != null)
            statusText.text = $"Created Room {roomId}. Joining...";
        
        // 방 생성 성공 시 바로 입장 시도
        SendJoinRoom(roomId);
    }

    // --- Popup Logic ---

    void OpenCreateRoomPopup()
    {
        Debug.Log("[LobbyUI] OpenCreateRoomPopup Called");
        if (createRoomPopup != null)
        {
            createRoomPopup.SetActive(true);
            if (roomTitleInput != null) roomTitleInput.text = "";
        }
        else
        {
            Debug.LogError("[LobbyUI] createRoomPopup is NULL in OpenCreateRoomPopup");
        }
    }

    void CloseCreateRoomPopup()
    {
        if (createRoomPopup != null)
            createRoomPopup.SetActive(false);
    }
}
