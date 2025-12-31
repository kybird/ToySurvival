using UnityEngine;

/// <summary>
/// 데드레코닝 컴포넌트
/// 서버로부터 받은 위치와 속도 정보를 사용하여 원격 오브젝트를 부드럽게 이동시킵니다.
/// </summary>
public class DeadReckoning : MonoBehaviour
{
    [Header("Dead Reckoning Settings")]
    [Tooltip("보간 속도 (높을수록 빠르게 목표 위치로 이동)")]
    public float interpolationSpeed = 10f;
    
    [Tooltip("예측 시간 제한 (초) - 서버 업데이트가 없을 때 최대 예측 시간")]
    public float maxPredictionTime = 0.5f;

    private Vector3 _serverPosition;
    private Vector3 _serverVelocity;
    private float _lastUpdateTime;
    private bool _hasReceivedUpdate = false;

    /// <summary>
    /// 서버로부터 위치/속도 업데이트를 받았을 때 호출
    /// </summary>
    public void UpdateFromServer(float x, float y, float vx, float vy)
    {
        _serverPosition = new Vector3(x, y, 0);
        _serverVelocity = new Vector3(vx, vy, 0);
        _lastUpdateTime = Time.time;
        _hasReceivedUpdate = true;

        // 첫 업데이트 시 즉시 위치 설정 (텔레포트)
        if (!_hasReceivedUpdate)
        {
            transform.position = _serverPosition;
        }
    }

    void Update()
    {
        if (!_hasReceivedUpdate)
            return;

        // 서버 업데이트 이후 경과 시간
        float elapsed = Time.time - _lastUpdateTime;
        
        // 예측 시간 제한 (너무 오래된 데이터는 사용하지 않음)
        elapsed = Mathf.Min(elapsed, maxPredictionTime);

        // 데드레코닝: 서버 위치 + (속도 * 경과 시간)
        Vector3 predictedPosition = _serverPosition + _serverVelocity * elapsed;

        // 부드러운 보간 (Lerp)
        transform.position = Vector3.Lerp(transform.position, predictedPosition, Time.deltaTime * interpolationSpeed);
    }

    /// <summary>
    /// 디버그용: 현재 예측 상태 표시
    /// </summary>
    void OnDrawGizmos()
    {
        if (!_hasReceivedUpdate)
            return;

        // 서버 위치 (빨간색)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(_serverPosition, 0.2f);

        // 속도 방향 (파란색 화살표)
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(_serverPosition, _serverPosition + _serverVelocity * 0.5f);
    }
}
