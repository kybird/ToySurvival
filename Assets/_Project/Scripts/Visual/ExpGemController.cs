using UnityEngine;

public class ExpGemController : MonoBehaviour
{
    private Transform _target;
    private bool _isFlying = false;
    private float _magnetRadius = 5.0f; // Matches GameConfig.EXP_GEM_MAGNET_RADIUS
    private float _flySpeed = 15.0f; // Matches GameConfig.EXP_GEM_FLY_SPEED

    private void Start() { }

    public void InitAndFly(Transform target, float speed)
    {
        _target = target;
        _flySpeed = speed;
        _isFlying = true;
    }

    private void Update()
    {
        if (!_isFlying || _target == null)
            return;

        // 플레이어 방향으로 이동
        transform.position = Vector3.MoveTowards(
            transform.position,
            _target.position,
            _flySpeed * Time.deltaTime
        );

        // 거의 도달하면 제거 (습득 완료 시각화)
        if (Vector3.Distance(transform.position, _target.position) < 0.1f)
        {
            Destroy(gameObject);
        }
    }
}
