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
    // [Sync Fix] 3Hz Heartbeat (User Requirement)
    // We rely on "Send on Change" for responsiveness.
    public float sendInterval = 0.333f;

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

    // Check if input should be blocked (e.g., during level-up selection)
    private bool IsInputBlocked()
    {
        return LevelUpUI.Instance != null && LevelUpUI.Instance.IsActive;
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

        Vector2 effectiveDir = _inputDir;
        if (IsInputBlocked())
            effectiveDir = Vector2.zero;

        _pendingInputs.Enqueue(new InputCmd { tick = _localSimTick, dir = effectiveDir });

        while (_pendingInputs.Count > _maxHistorySize)
            _pendingInputs.Dequeue();

        // 2️⃣ 보간용 스냅샷
        _prevLogicPos = _logicPos;

        SimulateMove(effectiveDir);
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

        Vector2 sendDir = _inputDir;
        if (IsInputBlocked())
            sendDir = Vector2.zero;

        // [Bug Fix] Round values before comparison to ignore micro float variations
        Vector2Int roundedDir = new Vector2Int(
            Mathf.RoundToInt(sendDir.x),
            Mathf.RoundToInt(sendDir.y)
        );
        Vector2Int roundedLastDir = new Vector2Int(
            Mathf.RoundToInt(_lastSentDir.x),
            Mathf.RoundToInt(_lastSentDir.y)
        );

        // [Sync Fix] Send ONLY if:
        // 1. Time overlapped (Heartbeat)
        // 2. OR Direction changed (Responsiveness)
        bool isTimeOver = (Time.unscaledTime - _lastSendTime >= sendInterval);
        bool isDirChanged = (roundedDir != roundedLastDir);

        // [Bug Fix] Don't send heartbeat when input is zero (prevents spam)
        if (isTimeOver && !isDirChanged && roundedDir == Vector2Int.zero)
            return;

        if (!isTimeOver && !isDirChanged)
            return;

        C_MoveInput pkt = new()
        {
            DirX = roundedDir.x,
            DirY = roundedDir.y,
            ClientTick = _localSimTick,
        };

        NetworkManager.Instance.Send(pkt);

        _lastSendTime = Time.unscaledTime;
        _lastSentDir = sendDir;
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

        // Debugging large errors
        if (error.magnitude > 0.5f)
        {
            // Debug.LogWarning($"[CSP] Hard Divergence: Error={error.magnitude:F2} | Pending={_pendingInputs.Count}");
        }

        // 3️⃣ Hard snap
        if (error.magnitude > snapThreshold)
        {
            // Debug.LogWarning($"[CSP] Snap! Error={error.magnitude:F2}");
            _logicPos = idealPos;
            _prevLogicPos = _logicPos;
            _pendingCorrection = Vector2.zero;
            return;
        }

        // 4️⃣ Soft correction (방향 튐 방지)
        _pendingCorrection = Vector2.Lerp(_pendingCorrection, error, 0.35f);
    }
}
