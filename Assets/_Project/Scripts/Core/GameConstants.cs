/// <summary>
/// 게임 전역 상수 정의
/// 서버와 동일한 값을 사용하여 tick 기반 시뮬레이션 동기화
/// </summary>
public static class GameConstants
{
    // ========================================
    // Tick System
    // ========================================
    
    /// <summary>
    /// [Legacy/Fallback] 기본 서버 tick rate (초당 tick 수)
    /// 서버 연결 전이나 초기화 실패 시 사용
    /// </summary>
    public const int DEFAULT_TICK_RATE = 30; // Deprecated: Use TickManager.Instance.TickRate
    
    /// <summary>
    /// [Legacy/Fallback] 기본 서버 tick 간격 (초 단위)
    /// </summary>
    public const float DEFAULT_SERVER_DT = 1.0f / 30.0f; // Deprecated: Use TickManager.Instance.TickInterval
    
    /// <summary>
    /// 최대 extrapolation tick 수
    /// 서버 패킷이 지연될 때 최대 몇 tick까지 예측할지 결정
    /// </summary>
    public const int MAX_EXTRAPOLATION_TICKS = 10;
    
    // ========================================
    // Movement Correction
    // ========================================
    
    /// <summary>
    /// Snap 임계값 (단위: Unity units)
    /// 예측 오차가 이 값보다 작으면 즉시 적용 (snap)
    /// </summary>
    public const float SNAP_EPSILON = 0.05f;
    
    /// <summary>
    /// Tick당 보간 계수 (0~1 범위)
    /// 예측 오차가 SNAP_EPSILON보다 클 때 Lerp 보정에 사용
    /// 주의: "SPEED" 용어 사용 금지 (tick 기반 시스템에서 혼동 방지)
    /// </summary>
    public const float CORRECTION_ALPHA_PER_TICK = 0.15f;

    /// <summary>
    /// 강제 동기화 임계값 (단위: Unity units)
    /// 서버 위치와 로컬 예측 위치 차이가 이 값보다 크면 강제로 서버 위치로 이동 (Snap)
    /// </summary>
    public const float HARD_DESYNC_THRESHOLD = 2.0f;
}
