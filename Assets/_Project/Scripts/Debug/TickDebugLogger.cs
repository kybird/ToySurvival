using UnityEngine;
using System.Collections;

public class TickDebugLogger : MonoBehaviour
{
    void Start()
    {
        //StartCoroutine(DebugTickLog());
    }

    IEnumerator DebugTickLog()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            if (TickManager.Instance == null)
                continue;

            int clientTick = TickManager.Instance.GetCurrentTick();
            // ServerTick을 외부에서 가져오기 어려우므로 TickManager에 LastReceivedServerTick 프로퍼티 추가 필요하지만
            // 일단 EstimateServerTick와 비교
            uint estServerTick = TickManager.Instance.EstimateServerTick();
            
            Debug.Log($"[TickCheck] ClientTick: {clientTick}, EstimatedServerTick: {estServerTick}, Diff: {clientTick - (int)estServerTick}");
        }
    }
}
