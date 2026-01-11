using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

public class TestPerformance
{
    private GameObject _player;
    private global::DeadReckoning _deadReckoning;

    [SetUp]
    public void SetUp()
    {
        // NetworkManager/TickManager Mocking might be needed if Awake calls them
        if (global::NetworkManager.Instance == null)
        {
            var go = new GameObject("NetworkManager");
            go.AddComponent<global::NetworkManager>();
        }
        if (global::Core.TickManager.Instance == null)
        {
            var go = new GameObject("TickManager");
            go.AddComponent<global::Core.TickManager>();
        }

        _player = new GameObject("RemotePlayer");
        _deadReckoning = _player.AddComponent<global::DeadReckoning>();
        // 초기화 데이터 주입
        _deadReckoning.UpdateFromServer(0, 0, 1, 0, 0);
    }

    [TearDown]
    public void TearDown()
    {
        if (_player != null)
            Object.Destroy(_player);
    }

    [UnityTest]
    public IEnumerator DeadReckoning_Update_DoesNotAlloc()
    {
        // Warmup (첫 실행 시 캐싱 등으로 인한 할당 제외)
        yield return null;

        // Assert
        // Update() 루프가 돌아가는 동안 GC 할당이 없는지 검사
        // 10 프레임 동안 검사
        for (int i = 0; i < 10; i++)
        {
            Assert.That(
                () => {
                    // 강제로 Update 호출 효과를 내거나 그냥 대기
                },
                Is.Not.AllocatingGCMemory()
            );

            yield return null;
        }
    }
}
