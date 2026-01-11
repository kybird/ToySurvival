using System.Collections.Generic;
using Protocol;
using UnityEngine;

public class ObjectManager : MonoBehaviour
{
    public static ObjectManager Instance { get; private set; }

    // ResourceManager 인스턴스 (간단하게 멤버로 유지하거나 싱글톤 사용)
    private ResourceManager _resourceManager = new ResourceManager();

    [Header("Scaling Settings")]
    public float positionScale = 1.0f; // 서버 좌표 * positionScale = 유니티 좌표

    private Dictionary<int, GameObject> _objects = new Dictionary<int, GameObject>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // 이미 존재하면 자신을 파괴 (싱글톤 유지)
            Destroy(gameObject);
        }
    }

    public void Clear()
    {
        // 씬 전환 시 호출하여 관리 중인 오브젝트 리스트 초기화
        _objects.Clear();
    }

    public void Spawn(ObjectInfo info)
    {
        Debug.Log(
            $"[ObjectManager] Spawn called for {info.Type}_{info.ObjectId}. Current MyPlayerId: {NetworkManager.Instance?.MyPlayerId ?? -999}"
        );

        if (_objects.ContainsKey(info.ObjectId))
            return;

        // Resource Path 규칙: Prefabs/{ObjectType}
        // 예: Prefabs/Player, Prefabs/Monster
        string resourcePath = $"Prefabs/{info.Type}";

        if (info.Type == ObjectType.Monster)
        {
            // resourcePath = $"Prefabs/Monster_{info.TypeId}";
        }

        Debug.Log($"[ObjectManager] Try to load resource: '{resourcePath}' for Type: {info.Type}");
        GameObject go = _resourceManager.Instantiate(resourcePath);

        if (go == null)
        {
            Debug.LogError(
                $"[ObjectManager] Failed to load prefab at '{resourcePath}'. Check if file exists in Resources/Prefabs/."
            );
            Debug.LogWarning($"[ObjectManager] Fallback to Cube for {info.Type}");
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        }

        // 서버 좌표를 그대로 사용 (스케일링 제거)
        float unityX = info.X;
        float unityY = info.Y;

        Debug.Log(
            $"[Spawn] ID: {info.ObjectId}, Type: {info.Type}, ServerPos: ({info.X}, {info.Y}), UnityPos: ({unityX}, {unityY}, 0)"
        );

        go.transform.position = new Vector3(unityX, unityY, 0);
        go.name = $"{info.Type}_{info.ObjectId}";
        _objects.Add(info.ObjectId, go);

        // 디버그: ID 비교 확인
        int myId = NetworkManager.Instance != null ? NetworkManager.Instance.MyPlayerId : -1;
        Debug.Log(
            $"[ObjectManager] Spawning {info.Type}_{info.ObjectId}. MyPlayerId: {myId}, Match: {info.ObjectId == myId}"
        );

        // 내 플레이어인 경우 ClientSidePredictionController 부착 및 카메라 연결
        if (info.ObjectId == NetworkManager.Instance.MyPlayerId)
        {
            // CSP 컨트롤러 부착
            ClientSidePredictionController csp = go.GetComponent<ClientSidePredictionController>();
            if (csp == null)
            {
                csp = go.AddComponent<ClientSidePredictionController>();
            }

            // 카메라 연결
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                CameraFollow camFollow = mainCam.GetComponent<CameraFollow>();
                if (camFollow == null)
                {
                    camFollow = mainCam.gameObject.AddComponent<CameraFollow>();
                }
                camFollow.SetTarget(go.transform);
                Debug.Log(
                    $"[ObjectManager] Camera Locked on MyPlayer (ID: {info.ObjectId}) with CSP"
                );
            }

            // 내 플레이어에는 절대 DeadReckoning을 붙이지 않음
            DeadReckoning dr = go.GetComponent<DeadReckoning>();
            if (dr != null)
            {
                Destroy(dr);
            }
        }
        else
        {
            // Remote Interpolation 제거 (DeadReckoning으로 대체)
            RemoteInterpolation interp = go.GetComponent<RemoteInterpolation>();
            if (interp != null)
                Destroy(interp);

            // 내 플레이어가 아닌 경우 (Remote Player, Monster 등) DeadReckoning 필수
            DeadReckoning dr = go.GetComponent<DeadReckoning>();
            if (dr == null)
            {
                dr = go.AddComponent<DeadReckoning>();
            }

            // Remote 객체에는 CSP가 있으면 안 됨
            ClientSidePredictionController csp = go.GetComponent<ClientSidePredictionController>();
            if (csp != null)
                Destroy(csp);
        }

        // TODO: Apply HP, State etc.
    }

    public void Despawn(int objectId)
    {
        if (_objects.TryGetValue(objectId, out GameObject go))
        {
            Destroy(go);
            _objects.Remove(objectId);
        }
    }

    public void UpdatePos(ObjectPos pos, uint serverTick)
    {
        // 내 캐릭터는 서버 위치 무시
        if (pos.ObjectId == NetworkManager.Instance.MyPlayerId)
            return;

        if (_objects.TryGetValue(pos.ObjectId, out GameObject go))
        {
            var dr = go.GetComponent<DeadReckoning>();
            if (dr != null)
            {
                // VX, VY 포함하여 전달 (Hermite Spline 지원)
                dr.UpdateFromServer(pos.X, pos.Y, pos.Vx, pos.Vy, serverTick);
            }
        }
    }

    public void OnDamage(int targetId, int damage)
    {
        if (_objects.TryGetValue(targetId, out GameObject go))
        {
            // 간단한 히트 연출: 빨간색으로 깜빡임
            var renderer = go.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null)
            {
                StartCoroutine(CoFlashColor(renderer, Color.red, 0.1f));
            }

            Debug.Log($"[ObjectManager] Object {targetId} took {damage} damage.");
        }
    }

    public void OnPlayerDead(int playerId)
    {
        if (_objects.TryGetValue(playerId, out GameObject go))
        {
            // 사망 연출: 회색으로 변하고 잠시 후 제거
            var renderer = go.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = Color.gray;
            }

            // 0.5초 후 제거
            StartCoroutine(CoDespawnWithDelay(playerId, 0.5f));

            Debug.Log($"[ObjectManager] Player {playerId} is DEAD.");
        }
    }

    public void SetObjectState(int objectId, ObjectState state)
    {
        if (_objects.TryGetValue(objectId, out GameObject go))
        {
            var renderer = go.GetComponentInChildren<SpriteRenderer>();
            if (renderer == null)
                return;

            switch (state)
            {
                case ObjectState.Downed:
                    // 다운된 상태: 반투명 처리
                    Color c = renderer.color;
                    c.a = 0.5f;
                    renderer.color = c;
                    break;
                case ObjectState.Idle:
                case ObjectState.Moving:
                    // 정상 상태: 불투명 복구
                    Color c2 = renderer.color;
                    c2.a = 1.0f;
                    renderer.color = c2;
                    break;
            }
        }
    }

    private System.Collections.IEnumerator CoFlashColor(
        SpriteRenderer renderer,
        Color flashColor,
        float duration
    )
    {
        if (renderer == null)
            yield break;
        Color originalColor = renderer.color;
        renderer.color = flashColor;
        yield return new WaitForSeconds(duration);
        if (renderer != null)
            renderer.color = originalColor;
    }

    private System.Collections.IEnumerator CoDespawnWithDelay(int objectId, float delay)
    {
        yield return new WaitForSeconds(delay);
        Despawn(objectId);
    }

    public GameObject GetMyPlayer()
    {
        if (NetworkManager.Instance == null)
            return null;
        int myId = NetworkManager.Instance.MyPlayerId;

        if (_objects.TryGetValue(myId, out GameObject go))
        {
            return go;
        }
        return null;
    }
}
