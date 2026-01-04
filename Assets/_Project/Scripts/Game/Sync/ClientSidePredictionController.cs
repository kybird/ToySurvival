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

    Queue<InputCmd> _pendingInputs = new Queue<InputCmd>();

    private float _lastSendTime = 0;
    private Vector2 _lastDir = Vector2.zero;
    private Vector2 _inputDirection;

    private uint _localInputTick = 0;
    private int _maxHistorySize = 60;

    private void UpdateHistorySize()
    {
        if (TickManager.Instance != null && TickManager.Instance.TickRate > 0)
        {
            _maxHistorySize = TickManager.Instance.TickRate * 2; // 2초 버퍼
        }
    }

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
        // Debug.Log($"[CSP] OnMove called! Input: {_inputDirection}");
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
        _localInputTick++;

        _pendingInputs.Enqueue(new InputCmd { tick = _localInputTick, dir = dir });

        // Safety: Prevent queue from growing indefinitely
        while (_pendingInputs.Count > _maxHistorySize)
        {
            _pendingInputs.Dequeue();
        }

        C_MoveInput pkt = new C_MoveInput();
        pkt.DirX = (int)Mathf.Round(dir.x); // -1, 0, 1
        pkt.DirY = (int)Mathf.Round(dir.y);

        pkt.ClientTick = _localInputTick;

        NetworkManager.Instance.Send(pkt);
    }

    // 서버로부터 위치 보정 요청 수신 (S_PlayerStateAck)
    public void OnServerCorrection(
        float serverX,
        float serverY,
        uint serverTick,
        uint clientTick
    ) { }

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

    public void OnPlayerStateAck(S_PlayerStateAck ack)
    {
        Debug.Log($"[Reconcile] Ack received Tick={ack.ClientTick}");

        float error = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.y),
            new Vector2(ack.X, ack.Y)
        );

        Debug.Log($"[Reconcile] Error={error:F3} Pending={_pendingInputs.Count}");

        // 서버 위치로 보정 (하드스냅)
        transform.position = new Vector3(ack.X, ack.Y, 0);

        // Ack tick 기준으로 _pendingInputs 큐 처리
        int removedCount = 0;
        while (_pendingInputs.Count > 0 && _pendingInputs.Peek().tick <= ack.ClientTick)
        {
            _pendingInputs.Dequeue();
            removedCount++;
        }

        // 남은 입력 재적용
        foreach (var input in _pendingInputs)
            ApplyLocalMove(input.dir);

        // 테스트용 로그
        Debug.Log(
            $"[Reconcile] Pos=({transform.position.x:F2},{transform.position.y:F2}) "
                + $"AckTick={ack.ClientTick} Removed={removedCount} Pending={_pendingInputs.Count}"
        );
    }

    private void ApplyLocalMove(Vector2 dir)
    {
        if (dir == Vector2.zero)
            return;

        // 1. 대각선 이동 정규화
        Vector2 moveDir = dir.normalized;

        // 2. Tick 단위 이동 거리 계산
        float moveDistance = moveSpeed * NetworkManager.Instance.ServerTickInterval;

        // 3. 현재 위치 업데이트
        Vector3 pos = transform.position;
        pos.x += moveDir.x * moveDistance;
        pos.y += moveDir.y * moveDistance;
        transform.position = pos;
    }
}
