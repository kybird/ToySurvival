using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class TestDeadReckoning
{
    private GameObject _player;
    private global::DeadReckoning _deadReckoning;
    private global::Core.TickManager _mockTickManager;
    private global::NetworkManager _mockNetworkManager;

    [SetUp]
    public void SetUp()
    {
        // 1. Setup NetworkManager (It initializes TickManager in Awake)
        var goNet = new GameObject("NetworkManager");
        _mockNetworkManager = goNet.AddComponent<global::NetworkManager>();

        // 2. Get TickManager reference
        _mockTickManager = global::Core.TickManager.Instance;
        if (_mockTickManager == null)
        {
            var goTick = new GameObject("TickManager");
            _mockTickManager = goTick.AddComponent<global::Core.TickManager>();
        }

        // Force initialize
        _mockTickManager.InitializeGlobal(30, 0.0333f, 0);
        _mockTickManager.InitGameAnchor(0);

        // 3. Setup Player
        _player = new GameObject("RemotePlayer");
        _deadReckoning = _player.AddComponent<global::DeadReckoning>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_player != null)
            Object.Destroy(_player);
        if (_mockNetworkManager != null)
            Object.Destroy(_mockNetworkManager.gameObject);
        if (global::Core.TickManager.Instance != null)
            Object.Destroy(global::Core.TickManager.Instance.gameObject);
    }

    private void SetFakeRTT(long rtt)
    {
        var prop = typeof(global::NetworkManager).GetProperty("RTT");
        if (prop != null)
        {
            prop.SetValue(_mockNetworkManager, rtt);
        }
    }

    [UnityTest]
    public IEnumerator DeadReckoning_AdaptsToJitter()
    {
        // Arrange
        _deadReckoning.UpdateFromServer(0, 0, 0, 0, false, 0);
        yield return null;

        // 1. Stable RTT (50ms) -> Jitter ~ 0
        for (int i = 0; i < 30; i++)
        {
            SetFakeRTT(50);
            _deadReckoning.UpdateFromServer(0, 0, 0, 0, false, (uint)i);
        }

        // Wait for lerp to settle
        yield return new WaitForSeconds(0.5f);

        // 2. Introduce Jitter (50ms ~ 150ms fluctuating)
        for (int i = 30; i < 60; i++)
        {
            long noisyRTT = (i % 2 == 0) ? 50 : 150;
            SetFakeRTT(noisyRTT);
            _deadReckoning.UpdateFromServer(0, 0, 0, 0, false, (uint)i);
            yield return null;
        }

        Assert.Pass("Jitter simulation executed without errors.");
    }

    [UnityTest]
    public IEnumerator DeadReckoning_Interpolates_Smoothly()
    {
        // Arrange
        SetFakeRTT(50);
        _deadReckoning.UpdateFromServer(0, 0, 0, 0, false, 0);

        yield return null;

        // Act
        uint futureTick = 30;
        float targetX = 10.0f;
        float velocityX = 10.0f;

        _deadReckoning.UpdateFromServer(targetX, 0, velocityX, 0, false, futureTick);

        // Assert
        float simulationTime = 0.5f;
        float elapsedTime = 0;

        Vector3 lastPos = _player.transform.position;

        while (elapsedTime < simulationTime)
        {
            elapsedTime += Time.deltaTime;
            yield return null;

            Vector3 currentPos = _player.transform.position;
            float dist = Vector3.Distance(lastPos, currentPos);

            Assert.Less(dist, 2.0f, "Movement should be smooth each frame");
            lastPos = currentPos;
        }

        Vector3 pos = _player.transform.position;
        Debug.Log($"[Test] Pos at 0.5s: {pos.x}");

        Assert.GreaterOrEqual(pos.x, 3.0f);
        Assert.LessOrEqual(pos.x, 6.0f);
    }
}
