using UnityEngine;
using UnityEngine.InputSystem;
using Protocol;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5.0f;
    public float sendInterval = 0.1f; // 100ms

    private float _lastSendTime = 0;
    private Vector2 _lastDir = Vector2.zero;
    private Vector2 _inputDirection;

    // PlayerInput 컴포넌트의 Behavior가 "Send Messages"일 때 호출됩니다.
    public void OnMove(InputValue value)
    {
        _inputDirection = value.Get<Vector2>();
    }

    void Update()
    {
        Vector2 dir = _inputDirection;
        if (dir.magnitude > 1.0f)
            dir.Normalize();

        // Local move for responsiveness
        // 2D 뷰(X-Y 평면)를 사용하므로 입력을 Y축으로 바로 매핑합니다.
        transform.Translate(new Vector3(dir.x, dir.y, 0) * moveSpeed * Time.deltaTime);

        // Send move packet to server
        if (Time.time - _lastSendTime >= sendInterval)
        {
            // 방향이 바뀌었을 때만 전송 (멈춤 포함)
            if (dir != _lastDir)
            {
                SendMovePacket(dir);
                _lastSendTime = Time.time;
                _lastDir = dir;
            }
        }

        // UI에 디버깅 정보 표시
        if (InGameUI.Instance != null)
        {
            float serverX = transform.position.x;
            float serverY = transform.position.y;
            
            string debugText = $"ID: {NetworkManager.Instance.MyPlayerId}\n" +
                               $"Pos: ({serverX:F1}, {serverY:F1})";
            InGameUI.Instance.SetDebugText(debugText);
        }
    }

    void SendMovePacket(Vector2 dir)
    {
        if (NetworkManager.Instance == null || NetworkManager.Instance.IsConnected == false)
            return;

        C_Move movePkt = new C_Move()
        {
            DirX = dir.x,
            DirY = dir.y
        };

        NetworkManager.Instance.Send(movePkt);
    }
}
