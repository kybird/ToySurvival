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

            // 피격 시 반짝임(Flash) 효과를 위한 머티리얼 초기화 (최적화)
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
        // 씬 전환 시 호출하여 관리 중인 오브젝트 파괴 및 초기화
        foreach (var kvp in _objects)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }
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
        else if (info.Type == ObjectType.Item)
        {
            // Item(ExpGem) 프리팹 로드
            resourcePath = "Prefabs/Item";
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

        // 내 플레이어인 경우 클라이언트 측 예측(CSP) 컨트롤러 부착 및 카메라 연결
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
            {
                dr.Initialize(delayMode);
                dr.UpdateFromServer(info.X, info.Y, info.Vx, info.Vy, info.LookLeft, serverTick);
            }
        }

        // HP 바가 존재한다면 초기화
        HpBar hpBar = go.GetComponentInChildren<HpBar>();
        if (hpBar != null)
        {
            hpBar.Init(info.Hp, info.MaxHp);
        }

        // [New] PlayerSkillVisuals 부착 (플레이어 타입인 경우)
        if (info.Type == ObjectType.Player)
        {
            var visuals = go.GetComponent<Visual.Skills.PlayerSkillVisuals>();
            if (visuals == null)
            {
                visuals = go.AddComponent<Visual.Skills.PlayerSkillVisuals>();
                // 여기서 필요한 자식 오브젝트(Orbit/Aura)를 동적으로 생성하거나 프리팹에서 찾아 연결하는 로직 필요
                // 실무 구조라면 프리팹에 이미 붙어있을 확률이 높지만, 유연성을 위해 체크
                EnsurePlayerVisualComponents(visuals);
            }
        }
    }

    private void EnsurePlayerVisualComponents(Visual.Skills.PlayerSkillVisuals visuals)
    {
        // OrbitVisual/AuraVisual이 없는 경우 자식 오브젝트로 생성
        if (visuals.orbitVisual == null)
        {
            GameObject orbitObj = new GameObject("OrbitVisual");
            orbitObj.transform.SetParent(visuals.transform, false);
            visuals.orbitVisual = orbitObj.AddComponent<Visual.Skills.OrbitVisual>();
        }

        if (visuals.auraVisual == null)
        {
            GameObject auraObj = new GameObject("AuraVisual");
            auraObj.transform.SetParent(visuals.transform, false);
            visuals.auraVisual = auraObj.AddComponent<Visual.Skills.AuraVisual>();

            // Aura용 SpriteRenderer (임시 서클)
            GameObject gfx = new GameObject("Gfx");
            gfx.transform.SetParent(auraObj.transform, false);
            var sr = gfx.AddComponent<SpriteRenderer>();
            sr.sprite = Resources.Load<Sprite>("Textures/Circle"); // Circle 스프라이트가 있다고 가정
            sr.color = new Color(1, 1, 1, 0.3f);
            visuals.auraVisual.auraRenderer = sr;
        }
    }

    public void UpdatePlayerVisuals(int playerId, List<InventoryItem> items)
    {
        if (_objects.TryGetValue(playerId, out GameObject go))
        {
            var visuals = go.GetComponent<Visual.Skills.PlayerSkillVisuals>();
            if (visuals != null)
            {
                visuals.Refresh(items);
            }
        }
    }

    public void UpdateHp(int objectId, float currentHp, float maxHp)
    {
        if (_objects.TryGetValue(objectId, out GameObject go))
        {
            // 1. 피격 효과 (빨간색 반짝임)
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

    public void Despawn(int objectId, int pickerId = 0)
    {
        if (_objects.TryGetValue(objectId, out GameObject go))
        {
            // [New] Picker 정보가 있으면 소유권을 연출 컴포넌트로 넘기고 관리 리스트에서 먼저 제거
            if (pickerId != 0 && _objects.TryGetValue(pickerId, out GameObject pickerGo))
            {
                var gemCtrl = go.GetComponent<ExpGemController>();
                if (gemCtrl != null)
                {
                    // DeadReckoning 비활성화 (서버 위치 동기화 중단하고 연출 시작)
                    var dr = go.GetComponent<DeadReckoning>();
                    if (dr != null)
                        dr.enabled = false;

                    gemCtrl.InitAndFly(pickerGo.transform, 15.0f); // Speed from GameConfig
                    _objects.Remove(objectId);
                    return;
                }
            }

            // [Fix] DeadReckoning 버퍼 정리 (늦은 패킷으로 인한 유령 이동 방지)
            var dr2 = go.GetComponent<DeadReckoning>();
            if (dr2 != null)
            {
                dr2.ClearSnapshots();
            }

            Destroy(go);
            _objects.Remove(objectId);
        }
    }

    public void UpdatePos(ObjectPos pos, uint serverTick)
    {
        // 내 캐릭터의 경우 위치 동기화(Snap)는 하지 않지만, 속도(Speed) 보너스는 상시 동기화합니다.
        if (pos.ObjectId == NetworkManager.Instance.MyPlayerId)
        {
            GameObject myPlayer = GetMyPlayer();
            if (myPlayer != null)
            {
                var csp = myPlayer.GetComponent<ClientSidePredictionController>();
                if (csp != null)
                {
                    // 서버에서 보낸 vx, vy의 크기(magnitude)를 현재 이동 속도로 취급합니다.
                    float serverSpeed = new Vector2(pos.Vx, pos.Vy).magnitude;
                    if (serverSpeed > 0.01f) // 0일 때는(정지 시) 마지막 속도를 유지합니다.
                    {
                        csp.moveSpeed = serverSpeed;
                    }
                }
            }
            return;
        }

        if (_objects.TryGetValue(pos.ObjectId, out GameObject go))
        {
            var dr = go.GetComponent<DeadReckoning>();
            if (dr != null)
            {
                // VX, VY 포함하여 전달 (Hermite Spline 지원)
                dr.UpdateFromServer(pos.X, pos.Y, pos.Vx, pos.Vy, pos.LookLeft, serverTick);
            }
        }
    }

    public void OnDamage(int targetId, int damage, bool isCritical = false)
    {
        if (_objects.TryGetValue(targetId, out GameObject go))
        {
            // 1. Flash Effect
            var flash = go.GetComponent<SimpleFlash>();
            if (flash == null)
                flash = go.AddComponent<SimpleFlash>();

            flash.Flash(isCritical ? Color.yellow : Color.red, _flashMaterial, 0.1f);

            // 데미지 텍스트 생성 (실무에서는 오브젝트 풀링 권장)
            GameObject dmgTextObj = _resourceManager.Instantiate("Prefabs/DamageText");
            if (dmgTextObj != null)
            {
                dmgTextObj.transform.position = go.transform.position + Vector3.up * 1.0f; // Above the unit
                DamageText dt = dmgTextObj.GetComponent<DamageText>();
                if (dt != null)
                {
                    dt.Setup(damage, isCritical);
                }
            }

            Debug.Log(
                $"{(isCritical ? "[Critical] " : "")}[ObjectManager] Object {targetId} took {damage} damage."
            );

            // [Sound] Play Hit Sound (Fallback if resource missing)
            if (SoundManager.Instance != null)
                SoundManager.Instance.Play("Hit");
        }
    }

    public void PlaySkillEffect(
        int skillId,
        float x,
        float y,
        float radius,
        float duration,
        float arcDegrees = 0,
        float rotationDegrees = 0
    )
    {
        // [코드 기반 연출]
        // 1. Frost Nova (ID 3): 원형 AoE
        if (skillId == 3)
        {
            Utils.AoEUtils.DrawAoE(
                worldPos: new Vector2(x, y),
                radius: radius,
                color: new Color(0.4f, 0.7f, 1.0f, 0.5f), // 시원한 하늘색 반투명
                duration: duration
            );
            return;
        }
        // 2. Greatsword (ID 4): 부채꼴 Arc AoE
        else if (skillId == 4 || arcDegrees > 0)
        {
            float finalArc = arcDegrees > 0 ? arcDegrees : 30.0f; // 기본값
            Utils.AoEUtils.DrawArcAoE(
                worldPos: new Vector2(x, y),
                radius: radius,
                arcDegrees: finalArc,
                rotationDegrees: rotationDegrees,
                color: new Color(1.0f, 1.0f, 1.0f, 0.6f), // 휘두르기 하얀색 잔상
                duration: duration > 0 ? duration : 0.3f
            );
            return;
        }

        // 그 외 프리팹이 필요한 스킬들 처리
        string resourcePath = $"Prefabs/SkillEffect_{skillId}";
        GameObject effectObj = _resourceManager.Instantiate(resourcePath);
        if (effectObj != null)
        {
            effectObj.transform.position = new Vector3(x, y, 0);
            effectObj.transform.rotation = Quaternion.Euler(0, 0, rotationDegrees);
            // 반지름을 지름으로 변환하여 스케일 적용 (기본 크기가 1x1인 프리팹 기준)
            effectObj.transform.localScale = Vector3.one * (radius * 2.0f);

            Debug.Log(
                $"[ObjectManager] Played skill effect {skillId} at ({x}, {y}) (R:{radius}, D:{duration}, Rot:{rotationDegrees})"
            );
        }
        else
        {
            // 프리팹이 없으면 기본 원형 이펙트로 대체 (Placeholder)
            Utils.AoEUtils.DrawAoE(
                worldPos: new Vector2(x, y),
                radius: radius,
                color: new Color(1.0f, 0.5f, 0.0f, 0.3f), // 주황색 경고 구역
                duration: duration > 0 ? duration : 1.0f
            );
            Debug.LogWarning(
                $"[ObjectManager] Missing prefab {resourcePath}, fallback to placeholder AoE."
            );
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
