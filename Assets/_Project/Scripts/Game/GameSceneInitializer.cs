using Core;
using Protocol;
using UnityEngine;

/// <summary>
/// GameScene 전용 초기화 스크립트
/// GameScene 로드 완료 시 호출됩니다.
/// </summary>
public class GameSceneInitializer : MonoBehaviour
{
    void Start()
    {
        Debug.Log("[GameSceneInitializer] Initializing GameScene...");

        // 배경 그리드 생성
        if (FindObjectOfType<GridVisualizer>() == null)
        {
            GameObject gridObj = new GameObject("GridManager");
            gridObj.AddComponent<GridVisualizer>();
        }

        // [C2] 인벤토리 HUD 정리 후 생성
        // 기존 InventoryHUD 파괴 (static 참조도 클리어됨)
        var existingHUDs = FindObjectsOfType<InventoryHUD>();
        foreach (var hud in existingHUDs)
        {
            Destroy(hud.gameObject);
        }
        
        GameObject hudObj = new GameObject("InventoryHUD");
        hudObj.AddComponent<InventoryHUD>();
        Debug.Log("[GameSceneInitializer] InventoryHUD created procedurally.");
        
        // [Fix] 생성 직후 pending 데이터가 있으면 즉시 적용 (타이밍 문제 해결)
        // AddComponent가 실행되면 Awake()가 호출되는데, 그 안에서 pending 처리됨

        // 이전 게임 오브젝트 정리
        if (ObjectManager.Instance != null)
        {
            ObjectManager.Instance.Clear();
        }

        // [중요] 틱 앵커 리셋 - 새 게임에서 올바른 틱 동기화를 위해 필수
        if (TickManager.Instance != null)
        {
            TickManager.Instance.ResetGameAnchor();
        }

        // 씬 로드 완료 이벤트 트리거 (Forced)
        if (GameManager.Instance != null)
        {
            Debug.Log(
                $"[GameSceneInitializer] Request Transition: {GameManager.Instance.CurrentState} -> InGame"
            );
            GameManager.Instance.TriggerEvent(StateEvent.SceneLoadComplete);
        }

        // 상태 전환 대기 후 GameReady 전송
        StartCoroutine(WaitAndSendGameReady());
    }

    private System.Collections.IEnumerator WaitAndSendGameReady()
    {
        // GameManager가 InGame 상태가 될 때까지 대기
        // (안전장치: 최대 5초 대기)
        float timeout = 5.0f;
        while (
            GameManager.Instance != null
            && GameManager.Instance.CurrentState != GameState.InGame
            && timeout > 0
        )
        {
            yield return null;
            timeout -= Time.deltaTime;
        }

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.InGame)
        {
            SendGameReady();
        }
        else
        {
            Debug.LogError(
                $"[GameSceneInitializer] State transition failed. Current: {GameManager.Instance?.CurrentState}"
            );
        }
    }

    private void SendGameReady()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            C_GameReady ready = new C_GameReady();
            NetworkManager.Instance.Send(ready);
            Debug.Log("[GameSceneInitializer] Sent C_GameReady to server");
        }
        else
        {
            Debug.LogWarning("[GameSceneInitializer] Cannot send C_GameReady - Not connected");
        }
    }
}