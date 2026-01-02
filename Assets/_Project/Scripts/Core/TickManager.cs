using UnityEngine;

/// <summary>
/// 클라이언트 tick 관리 싱글톤
/// 서버 tick과 동기화하여 tick 기반 시뮬레이션 제공
/// </summary>
public class TickManager : MonoBehaviour
{
    public static TickManager Instance { get; private set; }

    // ========================================
    // Tick 상태 (모두 int 타입 사용)
    // ========================================
    
    private int _clientTick = 0;
    private int _clientTickOffset = 0;
    private bool _isSynced = false;

    /// <summary>
    /// 서버와 동기화 완료 여부
    /// PacketHandler에서 hard/soft sync 분기에 사용
    /// </summary>
    public bool IsSynced => _isSynced;

    // ========================================
    // Unity Lifecycle
    // ========================================

    void Awake()
    {
        // 싱글톤 패턴
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // Awake에서는 기본값으로 먼저 설정 (안전장치)
            Time.fixedDeltaTime = GameConstants.SERVER_DT;
            Debug.Log($"[TickManager] Awake. Default FixedDeltaTime: {Time.fixedDeltaTime:F4}s");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 로그인 후 서버 설정값으로 TickManager 재설정
    /// </summary>
    public void Initialize(float tickInterval)
    {
        Time.fixedDeltaTime = tickInterval;
        _isSynced = false;
        _clientTick = 0;
        _clientTickOffset = 0;
        
        Debug.Log($"[TickManager] Initialized with Server Config. FixedDeltaTime: {Time.fixedDeltaTime:F4}s");
    }

    void FixedUpdate()
    {
        // FixedUpdate는 Unity 내부에서 누락 보정을 수행하므로
        // clientTick은 호출 횟수에 정확히 비례하여 증가
        // 프레임 드랍 시에도 tick 손실 없음
        _clientTick++;
    }

    // ========================================
    // Tick 동기화
    // ========================================

    /// <summary>
    /// 첫 패킷 수신 시 서버 tick과 hard sync
    /// </summary>
    /// <param name="serverTick">서버에서 받은 tick</param>
    public void SyncWithServer(uint serverTick)
    {
        if (!_isSynced)
        {
            // unchecked: uint를 int로 변환 시 overflow 허용 (비트 패턴 유지)
            _clientTickOffset = _clientTick - unchecked((int)serverTick);
            _isSynced = true;
            Debug.Log($"[TickManager] Hard sync: clientTick={_clientTick}, serverTick={serverTick}, offset={_clientTickOffset}");
        }
    }

    /// <summary>
    /// 이후 패킷마다 미세 재동기화 (soft sync)
    /// 장시간 플레이 시 tick drift 방지
    /// </summary>
    /// <param name="serverTick">서버에서 받은 tick</param>
    public void ResyncWithServer(uint serverTick)
    {
        if (!_isSynced)
            return;

        // unchecked: uint를 int로 변환 시 overflow 허용 (비트 패턴 유지)
        int currentTick = GetCurrentTick();
        int serverTickInt = unchecked((int)serverTick);
        int error = currentTick - serverTickInt;
        
        // 안전장치: error가 비정상적으로 크면 (100 tick 초과 = 3초 이상 drift)
        // soft sync로는 복구 불가능하므로 hard resync 수행
        if (Mathf.Abs(error) > 100)
        {
            Debug.LogError($"[TickManager] Critical drift detected: {error} ticks. Performing hard resync...");
            _clientTickOffset = _clientTick - serverTickInt;
            Debug.Log($"[TickManager] Hard resync complete: clientTick={_clientTick}, serverTick={serverTickInt}, newOffset={_clientTickOffset}");
            return;
        }
        
        // ±1~4 tick 지터는 정상 범위로 간주 (로컬 환경 및 Unity FixedUpdate 특성)
        // 5 tick 이상 drift 발생 시에만 보정하여 잦은 Snapback 방지
        
        // [User Check] 이론적으로 시간이 같아야 하지만, 실제로는 하드웨어 클럭 오차로 드리프트가 누적됩니다.
        // 테스트 결과 114 tick(약 3.8초)까지 벌어지는 것이 확인되었으므로, Soft Sync가 필수적입니다.
        if (Mathf.Abs(error) >= 5)
        {
            _clientTickOffset += error;  // 중요: += 로 drift 상쇄 (error = current - server)
            Debug.LogWarning($"[TickManager] Tick drift detected: {error} ticks. Resyncing... (current={currentTick}, server={serverTickInt})");
        }
    }

    /// <summary>
    /// 현재 클라이언트 tick 반환 (int 타입)
    /// 주의: tick 계산은 전 구간 int로 통일 (uint underflow 방지)
    /// </summary>
    /// <returns>현재 tick (서버 기준)</returns>
    public int GetCurrentTick()
    {
        return _clientTick - _clientTickOffset;
    }
}
