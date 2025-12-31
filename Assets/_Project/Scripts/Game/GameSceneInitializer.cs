using UnityEngine;
using Core;
using Protocol;

/// <summary>
/// GameScene 전용 초기화 스크립트
/// GameScene 로드 완료 시 호출됩니다.
/// </summary>
public class GameSceneInitializer : MonoBehaviour
{
    void Awake()
    {
        Debug.Log("[GameSceneInitializer] Initializing GameScene...");
        
        // 이전 게임 오브젝트 정리
        if (ObjectManager.Instance != null)
        {
            ObjectManager.Instance.Clear();
        }

        // 씬 로드 완료 이벤트 트리거 (Loading -> InGame 전이)
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Loading)
        {
            GameManager.Instance.TriggerEvent(StateEvent.SceneLoadComplete);
        }

        // 서버에 준비 완료 알림 (중요!)
        SendGameReady();
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
