using System.Collections.Generic;
using Core;
using Protocol;
using UnityEngine;
using UnityEngine.InputSystem;

struct InputCmd
{
    public uint tick;
    public Vector2 dir;
}

[RequireComponent(typeof(PlayerInput))]
public class ClientSidePredictionController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5.0f;
    public float sendInterval = 0.1f; // 100ms

    private void UpdateHistorySize()
    {
        if (TickManager.Instance != null && TickManager.Instance.TickRate > 0)
        {
            _maxHistorySize = TickManager.Instance.TickRate * 2; // 2초 버퍼
        }
    }

    Queue<InputCmd> _pendingInputs = new Queue<InputCmd>();

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
    private int _maxHistorySize = 120; // 기본 120. TickManager 초기화 후 (TickRate * 2초)로 재설정 권장

    private void Start()
    {
        Debug.Log("[CSP] Start() called");
        UpdateHistorySize();
        var playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            Debug.Log(
                $"[CSP] PlayerInput found. Current Map: {playerInput.currentActionMap?.name}"
            );
            // "Player" 맵이 활성화되어 있는지 확인 및 활성화
            if (
                playerInput.currentActionMap == null
                || playerInput.currentActionMap.name != "Player"
            )
            {
                playerInput.SwitchCurrentActionMap("Player");
                Debug.Log("[CSP] Switched to Player action map");
            }
            Debug.Log(
                $"[CSP] PlayerInput Initialized. Current Map: {playerInput.currentActionMap?.name}"
            );
        }
        else
        {
            Debug.LogError("[CSP] PlayerInput component NOT FOUND!");
        }

        // 서버와 동일한 고정 타임스텝 설정
        // TickManager에서 S_Login 시점에 이미 설정됨
        // Time.fixedDeltaTime = 1.0f / 30.0f; // REMOVED

        Debug.Log($"[CSP] Fixed timestep is {Time.fixedDeltaTime:F4}s");
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
            uint currentTick = (uint)TickManager.Instance.GetPredictionTick();
            Vector2 currentPos = new Vector2(transform.position.x, transform.position.y);

            _positionHistory.Enqueue(
                new PositionHistory { tick = currentTick, position = currentPos }
            );

            // 오래된 기록 제거
            while (_positionHistory.Count > _maxHistorySize)
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

        if (Time.time - _lastSendTime >= sendInterval || dir != _lastDir)
        {
            // 최소구현
            //  if (dir == Vector2.zero && _lastDir == Vector2.zero)
            //  {
            //      // 계속 정지 중 -> 패킷 생략 가능 (Heartbeat로 대체)
            //      // 하지만 마지막 정지 패킷은 확실히 보내야 함.
            //      // _lastDir 갱신 로직에 따라 처리.
            //  }
            //  else
            {
                SendMoveInputPacket(dir);
                _lastSendTime = Time.time;
                _lastDir = dir;
            }
        }
    }

    private void SendMoveInputPacket(Vector2 dir)
    {
        C_MoveInput pkt = new C_MoveInput();
        pkt.DirX = (int)Mathf.Round(dir.x); // -1, 0, 1
        pkt.DirY = (int)Mathf.Round(dir.y);

        if (TickManager.Instance != null)
        {
            pkt.ClientTick = 0; //(uint)TickManager.Instance.EstimateGameTick(); // Use current tick, not prediction tick
        }

        NetworkManager.Instance.Send(pkt);
    }

    // 서버로부터 위치 보정 요청 수신 (S_PlayerStateAck)
    public void OnServerCorrection(float serverX, float serverY, uint serverTick, uint clientTick)
    {
        Vector2 serverPos = new Vector2(serverX, serverY);

        // Server Reconciliation: clientTick 시점의 클라이언트 위치 찾기
        Vector2 clientPosAtClientTick = Vector2.zero;
        bool found = false;

        foreach (var history in _positionHistory)
        {
            if (history.tick == clientTick) // clientTick으로 비교
            {
                clientPosAtClientTick = history.position;
                found = true;
                break;
            }
        }

        if (!found)
        {
            // 해당 틱의 기록이 없으면 보정 포기
            Debug.LogWarning(
                $"[CSP] OnServerCorrection - ClientTick {clientTick} not found in history"
            );
            return;
        }

        float distance = Vector2.Distance(clientPosAtClientTick, serverPos);
        Vector2 delta = serverPos - clientPosAtClientTick;

        // Debug.Log($"[CSP] OnServerCorrection - ClientTick: {clientTick}, Error: {distance:F4}, Delta: {delta}");

        // 1. 오차가 너무 크면 (Hard Desync) -> 강제 스냅
        if (distance > GameConstants.HARD_DESYNC_THRESHOLD)
        {
            Debug.LogWarning($"[CSP] Hard Desync! Error: {distance:F4}. Snapping.");
            transform.position = new Vector3(serverX, serverY, 0);
            _positionHistory.Clear();
            return;
        }

        // 2. 오차가 작지만 존재하면 (Soft Correction) -> 현재 위치와 기록을 '이동'시킴
        // 과거의 오차만큼 현재 위치도 틀어졌다고 가정하고 보정
        const float SOFT_CORRECTION_THRESHOLD = 0.5f; // 0.05 -> 0.5 완화 (과도한 보정 방지)

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

            string debugText =
                $"[CSP] ID: {NetworkManager.Instance.MyPlayerId}\n"
                + $"RTT: {NetworkManager.Instance.RTT}ms\n"
                + $"Pos: ({x:F1}, {y:F1})";
            InGameUI.Instance.SetDebugText(debugText);
        }
    }
}
