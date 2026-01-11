using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class TestPrediction
{
    private GameObject _player;
    private global::ClientSidePredictionController _csp;
    private global::Core.TickManager _mockTickManager;
    private global::NetworkManager _mockNetworkManager;

    [SetUp]
    public void SetUp()
    {
        // 1. Setup Mock Tick/Network Managers
        var goGlobal = new GameObject("GlobalManagers");
        _mockTickManager = goGlobal.AddComponent<global::Core.TickManager>();
        _mockNetworkManager = goGlobal.AddComponent<global::NetworkManager>();

        // 2. Setup Player
        _player = new GameObject("LocalPlayer");

        // PlayerInput이 필수라면 추가 (패키지 의존성 필요)
        var input = _player.AddComponent<UnityEngine.InputSystem.PlayerInput>();
        input.actions = ScriptableObject.CreateInstance<UnityEngine.InputSystem.InputActionAsset>(); // 빈 액션

        _csp = _player.AddComponent<global::ClientSidePredictionController>();
        _csp.moveSpeed = 10.0f;
    }

    [TearDown]
    public void TearDown()
    {
        if (_player != null)
            Object.Destroy(_player);
        if (_mockTickManager != null)
            Object.Destroy(_mockTickManager.gameObject);
    }

    [UnityTest]
    public IEnumerator SoftCorrection_SmoothlyCorrectsError()
    {
        // Arrange
        _player.transform.position = new Vector3(5.0f, 0, 0); // 클라 위치: 5.0

        // Act
        // 서버에서 (4.8, 0) 위치라고 패킷 옴 (오차 -0.2)
        global::Protocol.S_PlayerStateAck ack = new global::Protocol.S_PlayerStateAck();
        ack.X = 4.8f;
        ack.Y = 0f;
        ack.ClientTick = 100; // 임의 틱

        _csp.OnPlayerStateAck(ack);

        // Assert 1: 즉시 튀지 않아야 함 (Soft Correction)
        Assert.That(
            _player.transform.position.x,
            Is.Not.EqualTo(4.8f).Within(0.01f),
            "Should not snap immediately"
        );

        // Act 2: 5 프레임 대기
        for (int i = 0; i < 5; i++)
            yield return null;

        // Assert 2: 위치가 4.8 방향으로 이동했으나 아직 완전히 도달하진 않았을 수 있음
        // 5.0 -> 4.8 방향이므로 x 값은 작아져야 함
        Assert.Less(_player.transform.position.x, 5.0f);
        Assert.Greater(_player.transform.position.x, 4.7f); // 오버슈팅 방지
    }

    [UnityTest]
    public IEnumerator HardSnap_OnLargeError()
    {
        // Arrange
        _player.transform.position = new Vector3(5.0f, 0, 0);

        // Act
        // 서버에서 (10.0, 0) 위치라고 패킷 옴 (오차 5.0 -> 매우 큼)
        global::Protocol.S_PlayerStateAck ack = new global::Protocol.S_PlayerStateAck();
        ack.X = 10.0f;
        ack.Y = 0f;
        ack.ClientTick = 100;

        _csp.OnPlayerStateAck(ack);

        // Assert: 즉시 이동해야 함
        yield return null;
        Assert.AreEqual(
            10.0f,
            _player.transform.position.x,
            0.01f,
            "Should snap immediately on large error"
        );
    }
}
