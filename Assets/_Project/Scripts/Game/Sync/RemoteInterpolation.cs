using UnityEngine;

public class RemoteInterpolation : MonoBehaviour
{
    public Vector3 TargetPos;
    public float LerpSpeed = 10f; // 나중에 조절

    void Start()
    {
        // 초기화 시 현재 위치를 TargetPos로 설정하여 갑작스런 텔레포트 방지
        TargetPos = transform.position;
    }

    void Update()
    {
        transform.position = Vector3.Lerp(
            transform.position,
            TargetPos,
            Time.deltaTime * LerpSpeed
        );
    }
}
