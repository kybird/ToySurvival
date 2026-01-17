/// <summary>
/// 렌더링 지연 정책 모드
/// DeadReckoning 컴포넌트가 네트워크 객체를 렌더링할 때 적용할 지연 전략을 정의합니다.
/// </summary>
public enum RenderDelayMode
{
    /// <summary>
    /// 네트워크 상태(RTT, Jitter)에 따라 동적으로 지연을 조정합니다.
    /// 플레이어, 몬스터 등 부드러운 보간이 필요한 객체에 적합합니다.
    /// </summary>
    Adaptive,

    /// <summary>
    /// 최소 지연(1틱)을 적용합니다.
    /// 빠르게 이동하는 객체에 적합합니다.
    /// </summary>
    Minimal,

    /// <summary>
    /// 지연 없이 즉시 렌더링합니다.
    /// 발사체, 이펙트 등 즉시성이 중요한 객체에 적합합니다.
    /// </summary>
    None,
}
