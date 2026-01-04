using UnityEngine;
using System.Collections.Generic;
using Core;

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
        public float time;   // ★ server tick
        public Vector2 pos;
        public Vector2 vel;  // ★ per-tick velocity
    }

    private List<Snapshot> _snapshots = new List<Snapshot>();
    private const float INTERPOLATION_DELAY = 0.1f; // 100ms (약 3틱) 지연 렌더링 to ensure smooth interpolation
    
    // Missing fields restored
    private bool _hasReceivedUpdate = false;
    public int LastReceivedTick { get; private set; } // For ObjectManager compatibility
    private double _lastSnapshotTime = 0; // Real-time when last snapshot was received
    
    public void UpdateFromServer(float x, float y, float vx, float vy, uint serverTick)
    {
        // 오래된 패킷 무시 (이중 방어)
        int tickDiff = unchecked((int)serverTick - LastReceivedTick);
        if (_hasReceivedUpdate && tickDiff <= 0)
            return;

        Snapshot snap = new Snapshot
        {
            time = serverTick,                  // ★ tick 기준
            pos = new Vector2(x, y),
            vel = new Vector2(vx, vy)            // ★ tick당 이동량 가정
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
        
        // 보간 지연 적용 (100ms = 약 2.5틱 @ 25TPS)
        float renderTick = currentGameTick - (INTERPOLATION_DELAY * TickManager.Instance.TickRate);

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
            if (dt > 2f) dt = 2f;
            nextPos = last.pos + last.vel * dt;
        }
        else
        {
            // interpolation
            Snapshot a = first, b = last;
            for (int i = 0; i < _snapshots.Count - 1; i++)
            {
                if (_snapshots[i].time <= renderTick &&
                    _snapshots[i + 1].time >= renderTick)
                {
                    a = _snapshots[i];
                    b = _snapshots[i + 1];
                    break;
                }
            }

            float t = (renderTick - a.time) / (b.time - a.time);
            nextPos = Vector2.Lerp(a.pos, b.pos, t);
        }

        transform.position = new Vector3(nextPos.x, nextPos.y, 0);
        UpdateVisuals(last.vel);
    }
    
    private void UpdateVisuals(Vector2 velocity)
    {
        // 1. 좌우 반전 (Flip) - Use SpriteRenderer.flipX instead of negative scale
        if (Mathf.Abs(velocity.x) > 0.01f)
        {
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                sr.flipX = velocity.x < 0;
            }
        }
        
        // 2. 애니메이션 (Animator가 있다면)
        // 'IsRun' 파라미터가 있다고 가정 (ToySurvival 표준)
        Animator anim = GetComponent<Animator>();
        if (anim != null)
        {
            bool isMoving = velocity.sqrMagnitude > 0.01f;
            anim.SetBool("IsRun", isMoving);
        }
    }

    void OnDrawGizmos()
    {
        // Debugging visualization
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}
