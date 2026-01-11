using System.Collections.Generic;
using Core;
using UnityEngine;

/// <summary>
/// Tick 기반 데드레코닝 컴포넌트
/// 서버로부터 받은 tick, 위치, 속도 정보를 사용하여 원격 오브젝트를 예측 및 보정
/// </summary>
public class DeadReckoning : MonoBehaviour
{
    // ========================================
    // Snapshot Interpolation (Valve Style)
    // ========================================

    private struct Snapshot
    {
        public float time; // ★ server tick
        public Vector2 pos;
        public Vector2 vel; // ★ per-tick velocity
    }

    private List<Snapshot> _snapshots = new List<Snapshot>();
    private const float INTERPOLATION_DELAY = 0.1f; // 100ms (약 3틱) 지연 렌더링 to ensure smooth interpolation

    // Missing fields restored
    private bool _hasReceivedUpdate = false;
    public int LastReceivedTick { get; private set; }
    private double _lastSnapshotTime = 0;

    // Adaptive Interpolation
    private float _avgRTT = 0.05f; // 초기값 50ms
    private float _rttVar = 0f; // RTT 분산 (Jitter Estimator)
    private float _currentDelay = 0.1f;
    private const float MIN_DELAY = 0.05f; // 최소 버퍼 50ms (TickRate 30Hz 기준 약 1.5틱)

    // Components (Cached)
    private SpriteRenderer _spriteRenderer;
    private Animator _animator;

    private void Awake()
    {
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _animator = GetComponent<Animator>();
    }

    private void UpdateJitter(float newRTT)
    {
        // EWMA (Exponential Weighted Moving Average)
        // RTT 평균 갱신
        _avgRTT = _avgRTT * 0.9f + newRTT * 0.1f;

        // 분산(Jitter) 갱신: |New - Avg|
        float diff = Mathf.Abs(newRTT - _avgRTT);
        _rttVar = _rttVar * 0.9f + diff * 0.1f;

        // 목표 딜레이: RTT/2 + Jitter*2 + BaseBuffer(0.02s)
        // RTT/2를 하는 이유: 서버 틱은 클라이언트 동작 시점보다 RTT/2만큼 과거임.
        // 하지만 여기선 서버틱 타임스탬프를 직접 쓰므로, 도착 시간 기준으로는 RTT만큼 과거가 아님...?
        // Valve 방식: interp = PacketTime - LerpTime.
        // ToyServer 방식: RenderTime = ServerTick - Delay.
        // ServerTick은 '생성된 시간'. 도착했을 땐 이미 RTT/2 만큼 지남.
        // 따라서 Delay는 최소 RTT/2 보단 커야 과거 데이터를 볼 수 있음.

        float targetDelay = (_avgRTT / 2f) + (_rttVar * 4f) + 0.03f; // Jitter * 4 (보수적) + 30ms

        _currentDelay = Mathf.Lerp(_currentDelay, targetDelay, 0.05f);
        if (_currentDelay < MIN_DELAY)
            _currentDelay = MIN_DELAY;
    }

    public void UpdateFromServer(float x, float y, float vx, float vy, uint serverTick)
    {
        // RTT 갱신 (NetworkManager 의존)
        if (NetworkManager.Instance != null)
        {
            // 밀리초 -> 초
            UpdateJitter(NetworkManager.Instance.RTT / 1000f);
        }
        // 오래된 패킷 무시 (이중 방어)
        int tickDiff = unchecked((int)serverTick - LastReceivedTick);
        if (_hasReceivedUpdate && tickDiff <= 0)
            return;

        Snapshot snap = new Snapshot
        {
            time = serverTick, // ★ tick 기준
            pos = new Vector2(x, y),
            // Server sends units/second (e.g. 2.0)
            // We use Ticks for time, so convert to units/tick
            vel = new Vector2(vx, vy) / TickManager.Instance.TickRate,
        };

        LastReceivedTick = (int)serverTick;

        // 텔레포트 판정 (마지막 스냅샷 기준)
        if (_snapshots.Count > 0)
        {
            Snapshot last = _snapshots[_snapshots.Count - 1];
            float dist = Vector2.Distance(last.pos, snap.pos);
            if (dist > 5.0f)
            {
                _snapshots.Clear();
                transform.position = new Vector3(x, y, 0);
            }
        }

        _snapshots.Add(snap);
        _lastSnapshotTime = Time.realtimeSinceStartupAsDouble; // Record when this snapshot was received

        // DEBUG: 서버 위치 vs 현재 렌더링 위치 비교
        Vector2 currentRenderPos = new Vector2(transform.position.x, transform.position.y);
        float diff = Vector2.Distance(currentRenderPos, snap.pos);
        if (diff > 0.5f) // 0.5 유닛 이상 차이 나면 로그
        {
            Debug.LogWarning(
                $"[DeadReckoning] ID={gameObject.name} | ServerPos=({snap.pos.x:F2},{snap.pos.y:F2}) | RenderPos=({currentRenderPos.x:F2},{currentRenderPos.y:F2}) | Diff={diff:F2} | Vel=({snap.vel.x:F2},{snap.vel.y:F2})"
            );
        }

        // 정렬 보장 (tick 오름차순)
        _snapshots.Sort((a, b) => a.time.CompareTo(b.time));

        // 버퍼 제한
        if (_snapshots.Count > 20)
            _snapshots.RemoveAt(0);

        if (!_hasReceivedUpdate)
        {
            transform.position = new Vector3(x, y, 0);
            _hasReceivedUpdate = true;
        }
    }

    void Update()
    {
        if (!_hasReceivedUpdate || TickManager.Instance == null || _snapshots.Count == 0)
            return;

        // TickManager의 EstimateGameTick()은 이미 서버 틱과 동기화되어 있음
        // (InitGameAnchor에서 서버 틱 기준으로 초기화됨)
        float currentGameTick = TickManager.Instance.EstimateServerTickFloat();

        // 보간 지연 적용 (Adaptive Delay)
        // Delay (Seconds) -> Delay (Ticks)
        float delayTicks = _currentDelay * TickManager.Instance.TickRate;
        float renderTick = currentGameTick - delayTicks;

        Vector2 nextPos;

        Snapshot first = _snapshots[0];
        Snapshot last = _snapshots[_snapshots.Count - 1];

        // 아직 과거 데이터만 있음
        if (renderTick <= first.time)
        {
            nextPos = first.pos;
        }
        // 최신 스냅샷보다 미래 → extrapolation
        else if (renderTick >= last.time)
        {
            float dt = renderTick - last.time;
            // Delta Sync로 인해 패킷 간격이 클 수 있음 (최대 30틱)
            // Extrapolation을 2틱(80ms)으로 제한하여 과도한 예측 방지
            if (dt > 2f)
                dt = 2f;
            nextPos = last.pos + last.vel * dt;
        }
        else
        {
            // interpolation
            Snapshot a = first,
                b = last;
            for (int i = 0; i < _snapshots.Count - 1; i++)
            {
                if (_snapshots[i].time <= renderTick && _snapshots[i + 1].time >= renderTick)
                {
                    a = _snapshots[i];
                    b = _snapshots[i + 1];
                    break;
                }
            }

            float t = (renderTick - a.time) / (b.time - a.time);

            // Hermite Spline Interpolation
            // p(t) = (2t^3 - 3t^2 + 1)p0 + (t^3 - 2t^2 + t)m0 + (-2t^3 + 3t^2)p1 + (t^3 - t^2)m1
            // m0, m1 are tangents (velocity * dt)

            // Normalize Velocity from Units/Tick to Units/Segment
            float segmentDuration = b.time - a.time;

            // 단순 Lerp (Fallback)
            // nextPos = Vector2.Lerp(a.pos, b.pos, t);

            // Spline
            Vector2 m0 = a.vel * segmentDuration;
            Vector2 m1 = b.vel * segmentDuration;

            nextPos = Hermite(a.pos, b.pos, m0, m1, t);
        }

        transform.position = new Vector3(nextPos.x, nextPos.y, 0);
        UpdateVisuals(last.vel);
    }

    private Vector2 Hermite(Vector2 p0, Vector2 p1, Vector2 m0, Vector2 m1, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        float h00 = 2 * t3 - 3 * t2 + 1;
        float h10 = t3 - 2 * t2 + t;
        float h01 = -2 * t3 + 3 * t2;
        float h11 = t3 - t2;

        return h00 * p0 + h10 * m0 + h01 * p1 + h11 * m1;
    }

    private void UpdateVisuals(Vector2 velocity)
    {
        // 1. 좌우 반전 (Flip) - Use Cached SpriteRenderer
        if (Mathf.Abs(velocity.x) > 0.01f)
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.flipX = velocity.x < 0;
            }
        }

        // 2. 애니메이션 - Use Cached Animator
        if (_animator != null)
        {
            bool isMoving = velocity.sqrMagnitude > 0.01f;
            _animator.SetBool("IsRun", isMoving);
        }
    }

    void OnDrawGizmos()
    {
        // Debugging visualization
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}
