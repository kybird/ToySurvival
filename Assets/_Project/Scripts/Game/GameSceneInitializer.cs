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
