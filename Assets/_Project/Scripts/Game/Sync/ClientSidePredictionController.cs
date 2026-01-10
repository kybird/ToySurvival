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
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Network")]
    public float sendInterval = 0.1f; // 전송 샘플링 전용

    [Header("Correction")]
    public float correctionSpeed = 8f;
    public float snapThreshold = 2.0f;

    // ===== Tick & Input =====
    private uint _localSimTick = 0;
    private Vector2 _inputDir;
    private Vector2 _lastSentDir;
    private float _lastSendTime;

    private readonly Queue<InputCmd> _pendingInputs = new();
    private int _maxHistorySize = 120;

    // ===== Position =====
    private Vector3 _logicPos;
    private Vector3 _prevLogicPos;
    private Vector2 _pendingCorrection;

    private void Start()
    {
        _logicPos = transform.position;
        _prevLogicPos = _logicPos;

        if (TickManager.Instance != null)
            _maxHistorySize = TickManager.Instance.TickRate * 2;
    }

    public void OnMove(InputValue value)
    {
        _inputDir = value.Get<Vector2>();
        if (_inputDir.sqrMagnitude > 1f)
            _inputDir.Normalize();
    }

    // ===============================
    // CLIENT SIMULATION (AUTHORITATIVE)
    // ===============================
    private void FixedUpdate()
    {
        _localSimTick++;

        // 1️⃣ 입력 히스토리 기록 (절대 압축 금지)
        _pendingInputs.Enqueue(new InputCmd { tick = _localSimTick, dir = _inputDir });

        while (_pendingInputs.Count > _maxHistorySize)
            _pendingInputs.Dequeue();

        // 2️⃣ 보간용 스냅샷
        _prevLogicPos = _logicPos;

        // 3️⃣ 이동
        SimulateMove(_inputDir);
    }

    private void SimulateMove(Vector2 dir)
    {
        Vector3 step = (Vector3)(dir * moveSpeed * Time.fixedDeltaTime);

        // Soft correction (저역 통과)
        if (_pendingCorrection.sqrMagnitude > 0.00001f)
        {
            Vector2 corrStep = _pendingCorrection * correctionSpeed * Time.fixedDeltaTime;

            if (corrStep.sqrMagnitude > _pendingCorrection.sqrMagnitude)
                corrStep = _pendingCorrection;

            step += (Vector3)corrStep;
            _pendingCorrection -= corrStep;
        }

        _logicPos += step;
    }

    // ===============================
    // VISUAL INTERPOLATION
    // ===============================
    private void Update()
    {
        SendInputIfNeeded();

        float alpha = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
        alpha = Mathf.Clamp01(alpha);

        transform.position = Vector3.Lerp(_prevLogicPos, _logicPos, alpha);
    }

    // ===============================
    // NETWORK SEND (SAMPLE ONLY)
    // ===============================
    private void SendInputIfNeeded()
    {
        if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
            return;

        if (Time.time - _lastSendTime < sendInterval && _inputDir == _lastSentDir)
            return;

        C_MoveInput pkt = new()
        {
            DirX = Mathf.RoundToInt(_inputDir.x),
            DirY = Mathf.RoundToInt(_inputDir.y),
            ClientTick = _localSimTick, // 마지막 시뮬 tick
        };

        NetworkManager.Instance.Send(pkt);

        _lastSendTime = Time.time;
        _lastSentDir = _inputDir;
    }

    // ===============================
    // SERVER ACK / RECONCILE
    // ===============================
    public void OnPlayerStateAck(S_PlayerStateAck ack)
    {
        Vector2 serverPos = new(ack.X, ack.Y);

        // 1️⃣ 서버가 처리한 tick까지 제거
        while (_pendingInputs.Count > 0 && _pendingInputs.Peek().tick <= ack.ClientTick)
        {
            _pendingInputs.Dequeue();
        }

        // 2️⃣ 클라 방식 그대로 재시뮬
        Vector2 idealPos = serverPos;
        foreach (var input in _pendingInputs)
        {
            idealPos += input.dir * moveSpeed * Time.fixedDeltaTime;
        }

        Vector2 currentPos = _logicPos;
        Vector2 error = idealPos - currentPos;

        // 3️⃣ Hard snap
        if (error.magnitude > snapThreshold)
        {
            _logicPos = idealPos;
            _prevLogicPos = _logicPos;
            _pendingCorrection = Vector2.zero;
            return;
        }

        // 4️⃣ Soft correction (방향 튐 방지)
        _pendingCorrection = Vector2.Lerp(_pendingCorrection, error, 0.35f);
    }
}
