using UnityEngine;
using UnityEngine.InputSystem;
using Protocol;
using Core;
using System.Collections.Generic;

[RequireComponent(typeof(PlayerInput))]
public class ClientSidePredictionController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5.0f;
    public float sendInterval = 0.1f; // 100ms

    private float _lastSendTime = 0;
    private Vector2 _lastDir = Vector2.zero;
    private Vector2 _inputDirection;
    
    // Server Reconciliation: 과거 위치 기록
    private struct PositionHistory
    {
        public uint tick;
        public Vector2 position;
    }
    private Queue<PositionHistory> _positionHistory = new Queue<PositionHistory>();
    private const int MAX_HISTORY_SIZE = 60; // 2초 분량 (30 TPS)

    private void Start()
    {
        Debug.Log("[CSP] Start() called");
        var playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            Debug.Log($"[CSP] PlayerInput found. Current Map: {playerInput.currentActionMap?.name}");
            // "Player" 맵이 활성화되어 있는지 확인 및 활성화
            if (playerInput.currentActionMap == null || playerInput.currentActionMap.name != "Player")
            {
                playerInput.SwitchCurrentActionMap("Player");
                Debug.Log("[CSP] Switched to Player action map");
            }
            Debug.Log($"[CSP] PlayerInput Initialized. Current Map: {playerInput.currentActionMap?.name}");
        }
        else
        {
            Debug.LogError("[CSP] PlayerInput component NOT FOUND!");
        }
        
        // 서버와 동일한 고정 타임스텝 설정 (30 TPS = 0.0333초)
        Time.fixedDeltaTime = 1.0f / 30.0f;
        Debug.Log($"[CSP] Fixed timestep set to {Time.fixedDeltaTime:F4}s (30 TPS)");
    }

    public void OnMove(InputValue value)
    {
        _inputDirection = value.Get<Vector2>();
        Debug.Log($"[CSP] OnMove called! Input: {_inputDirection}");
    }

    void Update()
    {
        // Update는 네트워크 전송과 UI 업데이트만 담당
        Vector2 dir = _inputDirection;
        if (dir.magnitude > 1.0f)
            dir.Normalize();

        handleNetwork(dir);
        updateDebugInfo();
    }
    
    void FixedUpdate()
    {
        // FixedUpdate에서 실제 이동 처리 (서버와 동일한 고정 타임스텝)
        Vector2 dir = _inputDirection;
        if (dir.magnitude > 1.0f)
            dir.Normalize();

        handleMovement(dir);
        
        // 이동 후 위치 기록 (Server Reconciliation용)
        if (TickManager.Instance != null)
        {
            uint currentTick = (uint)TickManager.Instance.GetCurrentTick();
            Vector2 currentPos = new Vector2(transform.position.x, transform.position.y);
            
            _positionHistory.Enqueue(new PositionHistory 
            { 
                tick = currentTick, 
                position = currentPos 
            });
            
            // 오래된 기록 제거
            while (_positionHistory.Count > MAX_HISTORY_SIZE)
            {
                _positionHistory.Dequeue();
            }
        }
    }

    private void handleMovement(Vector2 dir)
    {
        // 1. Client-Side Prediction: 입력 즉시 이동
        // 서버와 동일한 고정 타임스텝 사용 (FixedUpdate에서 호출되므로 Time.fixedDeltaTime 자동 적용)
        // if (dir != Vector2.zero)
        // {
        //     Debug.Log($"[CSP] Moving! Dir: {dir}, Speed: {moveSpeed}, DeltaTime: {Time.fixedDeltaTime}");
        // }
        transform.Translate(new Vector3(dir.x, dir.y, 0) * moveSpeed * Time.fixedDeltaTime);
    }

    private void handleNetwork(Vector2 dir)
    {
        if (NetworkManager.Instance == null || NetworkManager.Instance.IsConnected == false)
            return;

        // 2. 패킷 전송 규칙
        // - 일정 주기(sendInterval)마다 전송
        // - 방향이 급변하면 즉시 전송 (반응성 향상)
        
        bool isDirectionChanged = (dir != _lastDir);
        bool isIntervalPassed = (Time.time - _lastSendTime >= sendInterval);

        if (isIntervalPassed || (isDirectionChanged && dir != Vector2.zero)) 
        // 멈추는 것은 Interval에 맡기거나, 즉시 전송할 수도 있음. 여기서는 방향 전환 시 즉시 전송.
        {
            if (isDirectionChanged || isIntervalPassed) 
            {
               // Note: C_Move 대신 C_MoveInput 사용
               // 방향 전환 시에는 쿨타임 무시하고 보낼 수도 있음 (토폴로지에 따라 다름)
               // 여기서는 간단하게 Interval or Change Logic
            }
        }
        
        // 단순화: Interval 체크 + 변경 시 즉시
        if (Time.time - _lastSendTime >= sendInterval || dir != _lastDir)
        {
             // 변화가 없는데 Interval만 지났다면 굳이 자주 보낼 필요가 있는가?
             // KeepAlive 및 위치 검증을 위해 이동 중이면 계속 보내는 것이 좋음.
             // 정지 상태면 보내지 않음 (최적화)
             
             if (dir == Vector2.zero && _lastDir == Vector2.zero)
             {
                 // 계속 정지 중 -> 패킷 생략 가능 (Heartbeat로 대체)
                 // 하지만 마지막 정지 패킷은 확실히 보내야 함.
                 // _lastDir 갱신 로직에 따라 처리.
             }
             else
             {
                 SendMoveInputPacket(dir);
                 _lastSendTime = Time.time;
                 _lastDir = dir;
             }
        }
    }

    private void SendMoveInputPacket(Vector2 dir)
    {
        // 서버로 현재 입력와 클라이언트 틱 전송 (+ 실제 위치도 보낼 수 있지만, 여기서는 입력만)
        C_MoveInput pkt = new C_MoveInput()
        {
            DirX = (int)(dir.x * 100), // float -> int 정밀도 변환 or 그냥 float? Protocol.cs 확인 필요. 
                                       // Protocol 정의: DirX (int), DirY (int) 라고 되어있으면 변환 필요.
                                       // 아까 본 C_MoveInput은 DirX(int), DirY(int) 였나?
                                       // 다시 확인: C_Move_Input: DirX (int), DirY (int) -> Protocol View 결과 확인
        };
        
        // Protocol View 결과:
        // message C_MoveInput { uint32 client_tick = 1; int32 dir_x = 2; int32 dir_y = 3; }
        // 방향은 보통 -1, 0, 1 이거나 정밀도 있는 float일 수 있는데, int32면 고정소수점 혹은 단순 방향 인덱스?
        // 기존 PlayerController: C_Move { float DirX, float DirY } 였음.
        // User Request: Client must now send C_MoveInput instead of C_Move.
        // C_MoveInput definition in Protocol.cs: 
        // public int DirX { get; set; }
        // public int DirY { get; set; }
        // 입력은 대개 -1~1 사이 소수점 포함 값 (조이스틱).
        // int로 보낸다면 x1000 등을 해야 함. 
        // 혹은 Input System의 Vector2는 Normalized된 값이므로 -1.0 ~ 1.0.
        // Protocol 설계 의도가 "Key Input (-1, 0, 1)" 인지 "Joystick Angle" 인지 중요.
        // "방향 급변 허용 (velocity 즉시 변경)" 지시가 있으므로 Analog 입력일 가능성.
        // 여기서는 x1000 처리하여 정밀도 유지.
        
        // 서버가 정수 방향(-1, 0, 1)을 기대하는 경우
        pkt.DirX = (int)Mathf.Round(dir.x);  // -1, 0, 1
        pkt.DirY = (int)Mathf.Round(dir.y);
        
        if (TickManager.Instance != null)
        {
            pkt.ClientTick = (uint)TickManager.Instance.GetCurrentTick();
        }

        NetworkManager.Instance.Send(pkt);
    }
    
    // 서버로부터 위치 보정 요청 수신 (S_PlayerStateAck)
    public void OnServerCorrection(float serverX, float serverY, uint serverTick)
    {
        Vector2 serverPos = new Vector2(serverX, serverY);
        
        // Server Reconciliation: serverTick 시점의 클라이언트 위치 찾기
        Vector2 clientPosAtServerTick = Vector2.zero;
        bool found = false;
        
        foreach (var history in _positionHistory)
        {
            if (history.tick == serverTick)
            {
                clientPosAtServerTick = history.position;
                found = true;
                break;
            }
        }
        
        if (!found)
        {
            // 해당 틱의 기록이 없으면 보정 포기 (혹은 현재 위치로 대충 비교)
            // 기록이 없다는 건 너무 오래되었거나(Desync 심각) 아직 도달하지 않은 미래(불가능)
            return; 
        }
        
        float distance = Vector2.Distance(clientPosAtServerTick, serverPos);
        Vector2 delta = serverPos - clientPosAtServerTick;

        // Debug.Log($"[CSP] OnServerCorrection - Tick: {serverTick}, Error: {distance:F4}, Delta: {delta}");

        // 1. 오차가 너무 크면 (Hard Desync) -> 강제 스냅 (기존 로직 유지)
        if (distance > GameConstants.HARD_DESYNC_THRESHOLD)
        {
            Debug.LogWarning($"[CSP] Hard Desync! Error: {distance:F4}. Snapping.");
            transform.position = new Vector3(serverX, serverY, 0);
            _positionHistory.Clear();
            return;
        }

        // 2. 오차가 작지만 존재하면 (Soft Correction) -> 현재 위치와 기록을 '이동'시킴
        // 과거의 오차만큼 현재 위치도 틀어졌다고 가정하고 보정
        const float SOFT_CORRECTION_THRESHOLD = 0.05f; 
        
        if (distance > SOFT_CORRECTION_THRESHOLD)
        {
            // 현재 위치 보정
            transform.position += (Vector3)delta;
            Debug.Log($"[CSP] Soft Corrected by {delta}. NewPos: {transform.position}");
            
            // 미래의 기록들도 모두 보정 (중복 보정 방지)
            // Struct라 직접 수정 불가하므로 큐를 새로 구성해아 함
            int count = _positionHistory.Count;
            for (int i = 0; i < count; i++)
            {
                var h = _positionHistory.Dequeue();
                if (h.tick >= serverTick) // 해당 틱 포함 이후 기록들 보정
                {
                    h.position += delta;
                }
                _positionHistory.Enqueue(h);
            }
        }
    }

    void updateDebugInfo()
    {
        if (InGameUI.Instance != null)
        {
            float x = transform.position.x;
            float y = transform.position.y;
            
            string debugText = $"[CSP] ID: {NetworkManager.Instance.MyPlayerId}\n" +
                               $"Pos: ({x:F1}, {y:F1})";
            InGameUI.Instance.SetDebugText(debugText);
        }
    }
}
