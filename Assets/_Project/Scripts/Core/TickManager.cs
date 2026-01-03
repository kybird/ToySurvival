using UnityEngine;

/// <summary>
/// 클라이언트 tick 관리 싱글톤
/// 서버 tick과 동기화하여 tick 기반 시뮬레이션 제공
/// </summary>
public class TickManager : MonoBehaviour
{
    public static TickManager Instance { get; private set; }

    public int TickRate { get; private set; }
    public float TickInterval { get; private set; }

    #region 서버틱 앵커
    private uint baseServerTick;
    private double baseLocalTime;
    
    // serverTickRate removed as member field, replaced by property above
    // private int serverTickRate; 

    private bool initialized;

    public void SetServerTickAnchor(uint serverTick, double localTime, int tickRate)
    {
        TickRate = tickRate;
        TickInterval = 1.0f / tickRate;

        float rttMs = (NetworkManager.Instance != null) ? NetworkManager.Instance.RTT : 0;
        float rttSeconds = rttMs / 1000f;

        int latencyTicks = Mathf.RoundToInt((rttSeconds/2f) * tickRate);

        baseServerTick = serverTick + (uint)latencyTicks;
        baseLocalTime = Time.realtimeSinceStartupAsDouble;
        
        // _clientTick 초기화 (서버 틱에 맞춤)
        _clientTick = (int)baseServerTick;
        // _clientTickOffset = 16; // Legacy field removed
        _isSynced = true;

        initialized = true;
        uint currentEst = EstimateServerTick();
        Debug.Log($"[TickSync] Anchor Set! RTT: {rttMs}ms, LatencyTicks: {latencyTicks}, FinalBase: {baseServerTick}, CurrentEst: {currentEst}, Rate: {TickRate}");
    }

    public uint EstimateServerTick()
    {
        if (!initialized || TickRate <= 0)
            return baseServerTick;

        double dt = Time.realtimeSinceStartupAsDouble - baseLocalTime;
        if (dt < 0)
            dt = 0;

        return baseServerTick + (uint)(dt * TickRate);
    }

    public float EstimateServerTickFloat()
    {
        if (!initialized || TickRate <= 0)
            return baseServerTick;

        double dt = Time.realtimeSinceStartupAsDouble - baseLocalTime;
        if (dt < 0) dt = 0;

        return baseServerTick + (float)(dt * TickRate);
    }

    public bool IsInitialized()
    {
        return initialized;
    }
    
    #endregion 서버틱 앵커


    // ========================================
    // Tick 상태 (모두 int 타입 사용)
    // ========================================
    
    private int _clientTick = 0;
    // private int _clientTickOffset = 0; // Removed unused field
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
            Time.fixedDeltaTime = GameConstants.DEFAULT_SERVER_DT;
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
    public void Initialize(int tickRate)
    {
        TickRate = tickRate;
        TickInterval = 1.0f / tickRate;
        
        Time.fixedDeltaTime = TickInterval;
        _isSynced = false;
        
        Debug.Log($"[TickManager] Initialize(int) called. InputRate: {tickRate}, CalculatedInterval: {TickInterval:F6}, FixedDeltaTime: {Time.fixedDeltaTime:F6}");
    }

    void FixedUpdate()
    {
        if (!IsInitialized())
            return;
        // FixedUpdate는 Unity 내부에서 누락 보정을 수행하므로
        // clientTick은 호출 횟수에 정확히 비례하여 증가
        // 프레임 드랍 시에도 tick 손실 없음
        _clientTick++;

        // 50틱(1초)마다 한 번씩만 출력
        if (_clientTick % 50 == 0)
        {
            int est = (int)EstimateServerTick();
            Debug.Log($"[TickCheck] Client: {_clientTick}, Est: {est}, Diff: {_clientTick - est}");
        }
    }

    public void SyncWithServer(uint serverTick)
    {
        // Hard sync는 최초 1회만 허용
        // if (_isSynced)
        //     return;

        // int serverTickInt = unchecked((int)serverTick);

        // _clientTickOffset = _clientTick - serverTickInt;
        // _isSynced = true;

        // Debug.Log(
        //     $"[TickManager] HardSync complete: clientTick={_clientTick}, serverTick={serverTickInt}, offset={_clientTickOffset}"
        // );
    }

    public void ResyncWithServer(uint serverTick)
    {
        // if (!_isSynced)
        //     return;

        // int serverTickInt = unchecked((int)serverTick);
        // int currentTick = GetCurrentTick();
        // int error = currentTick - serverTickInt;

        // // Hard resync는 극단적 케이스만
        // if (Mathf.Abs(error) > 100)
        // {
        //     _clientTickOffset = _clientTick - serverTickInt;

        //     Debug.LogError(
        //         $"[TickManager] HARD RESYNC: error={error}, clientTick={_clientTick}, serverTick={serverTickInt}"
        //     );
        //     return;
        // }

        // // Soft sync: 한 번에 1 tick만 보정
        // if (Mathf.Abs(error) >= 5)
        // {
        //     int correction = Mathf.Clamp(error, -1, 1);
        //     _clientTickOffset += correction;

        //     Debug.LogWarning(
        //         $"[TickManager] SoftSync: error={error}, correction={correction}, newOffset={_clientTickOffset}"
        //     );
        // }
    }

    /// <summary>
    /// 현재 클라이언트 tick 반환 (int 타입)
    /// 주의: tick 계산은 전 구간 int로 통일 (uint underflow 방지)
    /// </summary>
    /// <returns>현재 tick (서버 기준)</returns>
    public int GetCurrentTick()
    {
        return _clientTick; // - _clientTickOffset;
    }

    /// <summary>
    /// 클라이언트 사이드 예측을 위한 미래 Tick (RTT 반영)
    /// </summary>
    public int GetPredictionTick()
    {
        // RTT가 아직 측정 안됐으면 기본값 반환
        if (NetworkManager.Instance == null || NetworkManager.Instance.RTT == 0)
            return GetCurrentTick() + 2; // 최소 버퍼

        // RTT(ms) -> Seconds
        float rttSeconds = NetworkManager.Instance.RTT / 1000f;
        
        // One-way latency (RTT/2)를 Tick으로 변환
        int latencyTicks = Mathf.CeilToInt((rttSeconds / 2f) / Time.fixedDeltaTime);
        
        // Jitter Buffer (2~3 ticks)
        int bufferTicks = 2;

        return GetCurrentTick() + latencyTicks + bufferTicks;
    }
}
