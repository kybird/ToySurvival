using UnityEngine;

/// <summary>
/// Tick 기반 데드레코닝 컴포넌트
/// 서버로부터 받은 tick, 위치, 속도 정보를 사용하여 원격 오브젝트를 예측 및 보정
/// </summary>
public class DeadReckoning : MonoBehaviour
{
    // ========================================
    // 서버 스냅샷 상태 (int 타입 tick 사용)
    // ========================================
    
    private int _lastReceivedTick;
    private Vector2 _lastReceivedPos;
    private Vector2 _lastReceivedVel;
    private bool _hasReceivedUpdate = false;

    /// <summary>
    /// 마지막으로 받은 서버 tick (ObjectManager에서 오래된 패킷 체크용)
    /// </summary>
    public int LastReceivedTick => _lastReceivedTick;

    // ========================================
    // 서버 업데이트
    // ========================================

    /// <summary>
    /// 서버로부터 위치/속도/tick 업데이트를 받았을 때 호출
    /// </summary>
    public void UpdateFromServer(float x, float y, float vx, float vy, uint serverTick)
    {
        // 첫 수신 여부를 먼저 저장 (중요! _hasReceivedUpdate = true 전에 평가)
        bool isFirst = !_hasReceivedUpdate;
        
        _lastReceivedPos = new Vector2(x, y);
        _lastReceivedVel = new Vector2(vx, vy);
        _lastReceivedTick = (int)serverTick;  // int로 변환
        _hasReceivedUpdate = true;

        // NOTE:
        // If velocity == zero, predictedPos remains constant.
        // This is intentional to preserve authoritative stop without oscillation.

        // 첫 업데이트 시 즉시 위치 설정 (텔레포트)
        if (isFirst)
        {
            transform.position = new Vector3(x, y, 0);
            Debug.Log($"[DeadReckoning] Initial Pos: ({x:F2}, {y:F2}), Vel: ({vx:F2}, {vy:F2}) Tick: {serverTick}");
        }
        else
        {
            // 이동 중인데 속도가 0인지 확인 (Stuttering 원인 의심)
            float dist = Vector2.Distance(_lastReceivedPos, new Vector2(x, y));
            float speed = new Vector2(vx, vy).magnitude;
            
            // 위치는 변했는데 속도가 0에 가깝다면 경고
            if (dist > 0.01f && speed < 0.01f)
            {
                Debug.LogWarning($"[DeadReckoning] Suspicious Data! Pos Changed ({dist:F4}) but Vel is ZERO. Object: {name}, Tick: {serverTick}");
            }
        }
    }

    // ========================================
    // Tick 기반 예측 및 보정
    // ========================================

    // WARNING:
    // Do NOT modify transform.position in Update().
    // All movement logic MUST reside in FixedUpdate().
    
    void FixedUpdate()
    {
        if (!_hasReceivedUpdate || TickManager.Instance == null)
            return;
            
        // 현재 클라이언트 tick 가져오기 (int 타입)
        int currentTick = TickManager.Instance.GetCurrentTick();
        int dtTicks = currentTick - _lastReceivedTick;
        
        // NOTE: dtTicks가 음수인 경우(지연 패킷) 최소 0으로 처리하여 서버 위치 유지.
        // MAX_EXTRAPOLATION_TICKS에 도달한 경우, 추가 extrapolation을 중단.
        dtTicks = Mathf.Clamp(dtTicks, 0, GameConstants.MAX_EXTRAPOLATION_TICKS);
        
        // 동적 서버 DT 사용 (NetworkManager가 없으면 기본값 사용)
        float serverDt = NetworkManager.Instance != null ? NetworkManager.Instance.ServerTickInterval : GameConstants.SERVER_DT;
        
        // Tick 기반 예측 위치 계산
        Vector2 predictedPos = _lastReceivedPos + _lastReceivedVel * (dtTicks * serverDt);
        Vector2 currentPos = new Vector2(transform.position.x, transform.position.y);
        float error = Vector2.Distance(currentPos, predictedPos);
        
        // Soft Correction
        if (error < GameConstants.SNAP_EPSILON)
        {
            // 오차가 작으면 즉시 적용 (snap)
            transform.position = new Vector3(predictedPos.x, predictedPos.y, 0);
        }
        else
        {
            // 오차가 크면 Lerp 보정 (tick 기준 alpha, Time.deltaTime 사용 금지!)
            float alpha = GameConstants.CORRECTION_ALPHA_PER_TICK;
            Vector2 correctedPos = Vector2.Lerp(currentPos, predictedPos, alpha);
            transform.position = new Vector3(correctedPos.x, correctedPos.y, 0);
        }
    }

    // ========================================
    // 디버그 시각화
    // ========================================

    void OnDrawGizmos()
    {
        if (!_hasReceivedUpdate)
            return;

        // 서버 위치 (빨간색)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(new Vector3(_lastReceivedPos.x, _lastReceivedPos.y, 0), 0.2f);

        // 속도 방향 (파란색 화살표)
        Gizmos.color = Color.blue;
        Vector3 serverPos3D = new Vector3(_lastReceivedPos.x, _lastReceivedPos.y, 0);
        Gizmos.DrawLine(serverPos3D, serverPos3D + new Vector3(_lastReceivedVel.x, _lastReceivedVel.y, 0) * 0.5f);
        
        // NOTE:
        // If velocity == zero, predictedPos remains constant.
        // This is intentional to preserve authoritative stop without oscillation.
    }
}
