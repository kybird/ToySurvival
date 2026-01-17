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

    private Material _flashMaterial;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialize Flash Material (Optimization)
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader != null)
            {
                _flashMaterial = new Material(shader);
                if (_flashMaterial.HasProperty("_BaseColor"))
                    _flashMaterial.SetColor("_BaseColor", Color.red);
                else if (_flashMaterial.HasProperty("_Color"))
                    _flashMaterial.SetColor("_Color", Color.red);
            }
            else
            {
                Debug.LogError("[ObjectManager] Could not find Unlit shader for flash effect.");
            }
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

    public void Spawn(ObjectInfo info, uint serverTick = 0)
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
            // Monster_1, Monster_2 등 타입별 프리팹 지원
            resourcePath = $"Prefabs/Monster_{info.TypeId}";
        }
        else if (info.Type == ObjectType.Projectile)
        {
            // Projectile_1 등 타입별 프리팹 지원
            resourcePath = $"Prefabs/Projectile_{info.TypeId}";
        }

        Debug.Log($"[ObjectManager] Try to load resource: '{resourcePath}' for Type: {info.Type}");
        GameObject go = _resourceManager.Instantiate(resourcePath);

        if (go == null)
        {
            Debug.LogWarning(
                $"[ObjectManager] Failed to load prefab at '{resourcePath}'. Fallback to generic {info.Type}"
            );

            // 1. Enum String Fallback
            resourcePath = $"Prefabs/{info.Type}";
            go = _resourceManager.Instantiate(resourcePath);

            // 2. Explicit String Fallback (Guard against Enum name mismatch)
            if (go == null && info.Type == ObjectType.Projectile)
            {
                Debug.LogWarning("[ObjectManager] Fallback to explicit 'Prefabs/Projectile'");
                resourcePath = "Prefabs/Projectile";
                go = _resourceManager.Instantiate(resourcePath);
            }
        }

        if (go == null)
        {
            Debug.LogError(
                $"[ObjectManager] Failed to load fallback prefab at '{resourcePath}'. Check if file exists in Resources/Prefabs/."
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
        go.transform.rotation = Quaternion.identity;
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

            // [Important] 렌더링 정책 결정 (타입 기반)
            // ObjectManager는 타입을 알고, DeadReckoning은 정책만 받음
            RenderDelayMode delayMode = info.Type switch
            {
                ObjectType.Projectile => RenderDelayMode.None,
                ObjectType.Monster => RenderDelayMode.Adaptive,
                ObjectType.Player => RenderDelayMode.Adaptive,
                _ => RenderDelayMode.Adaptive,
            };

            // [Important] Initialize immediately with Velocity and Delay Policy
            if (serverTick > 0 && dr != null)
            {
                dr.Initialize(delayMode);
                dr.UpdateFromServer(info.X, info.Y, info.Vx, info.Vy, serverTick);
            }
        }

        // Initialize HP Bar if exists
        HpBar hpBar = go.GetComponentInChildren<HpBar>();
        if (hpBar != null)
        {
            hpBar.Init(info.Hp, info.MaxHp);
        }
    }

    public void UpdateHp(int objectId, float currentHp, float maxHp)
    {
        if (_objects.TryGetValue(objectId, out GameObject go))
        {
            // 1. Flash Effect (Damage Feedback)
            var flash = go.GetComponent<SimpleFlash>();
            if (flash == null)
                flash = go.AddComponent<SimpleFlash>();

            flash.Flash(Color.red, _flashMaterial, 0.1f);

            // 2. Update HP Bar
            HpBar hpBar = go.GetComponentInChildren<HpBar>();
            if (hpBar != null)
            {
                hpBar.SetHp(currentHp, maxHp);
            }
        }
    }

    public void Despawn(int objectId)
    {
        if (_objects.TryGetValue(objectId, out GameObject go))
        {
            // [Fix] DeadReckoning 버퍼 정리 (늦은 패킷으로 인한 유령 이동 방지)
            var dr = go.GetComponent<DeadReckoning>();
            if (dr != null)
            {
                dr.ClearSnapshots();
            }

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
            // 1. Flash Effect
            var flash = go.GetComponent<SimpleFlash>();
            if (flash == null)
                flash = go.AddComponent<SimpleFlash>();

            flash.Flash(Color.red, _flashMaterial, 0.1f);

            // 2. Spawn Damage Text
            // Load "Prefabs/DamageText"
            // Note: In a real project, cache this prefab or use an object pool.
            GameObject dmgTextObj = _resourceManager.Instantiate("Prefabs/DamageText");
            if (dmgTextObj != null)
            {
                dmgTextObj.transform.position = go.transform.position + Vector3.up * 1.0f; // Above the unit
                DamageText dt = dmgTextObj.GetComponent<DamageText>();
                if (dt != null)
                {
                    dt.Setup(damage);
                }
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

    private System.Collections.IEnumerator CoDespawnWithDelay(int objectId, float delay)
    {
        yield return new WaitForSeconds(delay);
        Despawn(objectId);
    }

    public void ApplyKnockback(int objectId, float dirX, float dirY, float force, float duration)
    {
        if (_objects.TryGetValue(objectId, out GameObject go))
        {
            var dr = go.GetComponent<DeadReckoning>();
            if (dr != null)
            {
                dr.ForceImpulse(dirX, dirY, force, duration);
            }
        }
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
