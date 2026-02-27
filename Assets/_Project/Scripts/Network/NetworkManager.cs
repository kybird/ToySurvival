using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Core;
using Network;
using Protocol;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkManager : MonoBehaviour
{
    static NetworkManager _instance;
    public static NetworkManager Instance
    {
        get { return _instance; }
    }

    ServerSession _session = new ServerSession();
    public ServerSession Session
    {
        get { return _session; }
    }

    public bool IsConnected
    {
        get { return _session != null && _session.IsConnected(); }
    }
    public int MyPlayerId { get; set; }

    // 서버에서 S_Login으로 받아올 Tick 설정 (기본값은 GameConstants 사용)
    public int ServerTickRate { get; set; } = GameConstants.DEFAULT_TICK_RATE;
    public float ServerTickInterval { get; set; } = GameConstants.DEFAULT_SERVER_DT;

    // [Fix] Use events instead of Action properties to allow += and -=
    // This prevents MissingReferenceException when LoginUI is destroyed
    public event Action OnConnected;
    public event Action OnDisconnected;

    private string _lastHost;
    private int _lastPort;
    private bool _isConnecting = false;
    private bool _isRetrying = false;
    private float _retryInterval = 2.0f;
    private Queue<Action> _jobQueue = new Queue<Action>();
    private object _lock = new object();
    private Coroutine _pingCoroutine;

    void Awake()
    {
        Time.timeScale = 1f;
        Debug.Log(
            $"[NetworkManager] Awake called. Current instance: {(_instance != null ? "EXISTS" : "NULL")}"
        );

        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[NetworkManager] Instance created and set to DontDestroyOnLoad");

            // 핵심 매니저들 생성
            EnsureCoreManagers();
        }
        else
        {
            Debug.LogWarning(
                "[NetworkManager] Duplicate instance detected - Destroying this GameObject"
            );
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        Debug.Log("[NetworkManager] OnDestroy - Cleaning up session");
        _session?.Dispose();
    }

    /// <summary>
    /// 핵심 매니저들이 존재하는지 확인하고 없으면 생성합니다.
    /// </summary>
    private void EnsureCoreManagers()
    {
        // GameManager
        if (GameManager.Instance == null)
        {
            GameObject go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
            Debug.Log("[NetworkManager] GameManager created");
        }

        // TickManager (필수!)
        if (TickManager.Instance == null)
        {
            GameObject go = new GameObject("TickManager");
            go.AddComponent<TickManager>();
            Debug.Log(
                $"[NetworkManager] TickManager created. Instance is now: {(TickManager.Instance != null ? "VALID" : "NULL")}"
            );
        }

        // ObjectManager
        if (ObjectManager.Instance == null)
        {
            GameObject go = new GameObject("ObjectManager");
            go.AddComponent<ObjectManager>();
            Debug.Log("[NetworkManager] ObjectManager created");
        }

        // LoadingManager
        if (LoadingManager.Instance == null)
        {
            GameObject go = new GameObject("LoadingManager");
            go.AddComponent<LoadingManager>();
            Debug.Log("[NetworkManager] LoadingManager created");
        }
    }

    public void Connect(string host, int port)
    {
        if (IsConnected || _isConnecting)
            return;

        _isConnecting = true;
        _lastHost = host;
        _lastPort = port;

        IPAddress ipAddr;
        if (IPAddress.TryParse(host, out ipAddr) == false)
        {
            try
            {
                IPHostEntry hostEntry = Dns.GetHostEntry(host);
                foreach (IPAddress address in hostEntry.AddressList)
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipAddr = address;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"DNS Resolution Failed for {host}: {e.Message}");
                _isConnecting = false;
                if (!_isRetrying)
                    StartCoroutine(CoRetryConnect());
                return;
            }
        }

        if (ipAddr == null)
        {
            Debug.LogError($"Could not resolve IP for {host}");
            _isConnecting = false;
            if (!_isRetrying)
                StartCoroutine(CoRetryConnect());
            return;
        }

        IPEndPoint endPoint = new IPEndPoint(ipAddr, port);

        Connector connector = new Connector();
        connector.OnFailed = () =>
        {
            PushJob(() =>
            {
                _isConnecting = false;
                if (!_isRetrying && !IsConnected)
                    StartCoroutine(CoRetryConnect());
            });
        };
        connector.Connect(
            endPoint,
            () =>
            {
                _session.OnConnectedCallback = () => PushJob(HandleConnected);
                _session.OnDisconnectedCallback = () => PushJob(HandleDisconnected);
                return _session;
            }
        );
    }

    void HandleConnected()
    {
        Debug.Log("Connected to Server");
        // IsConnected is now dynamic property
        _isConnecting = false;
        _isRetrying = false;

        Debug.Log(
            $"[NetworkManager] OnConnected has {(OnConnected != null ? "subscribers" : "NO subscribers")}"
        );
        OnConnected?.Invoke();

        // [Fix] 직접 LoginUI를 찾아서 갱신 (현재 활성 씬만 검색)
        // FindObjectsOfType은 모든 씬에서 찾으므로, 활성 씬의 GameObject만 필터링
        Scene activeScene = SceneManager.GetActiveScene();
        LoginUI[] loginUIs = GameObject.FindObjectsOfType<LoginUI>(true);

        // [Debug] 모든 로드된 씬과 찾은 LoginUI 로깅
        Debug.Log(
            $"[NetworkManager] ActiveScene: '{activeScene.name}' (loaded={activeScene.isLoaded})"
        );
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            Debug.Log($"[NetworkManager] Loaded Scene[{i}]: '{s.name}' (loaded={s.isLoaded})");
        }
        Debug.Log($"[NetworkManager] Found {loginUIs.Length} LoginUI(s)");
        foreach (var ui in loginUIs)
        {
            Debug.Log(
                $"[NetworkManager]   - LoginUI on GameObject '{ui.gameObject.name}', scene='{ui.gameObject.scene.name}', activeInHierarchy={ui.gameObject.activeInHierarchy}"
            );
        }

        LoginUI loginUI = null;
        foreach (var ui in loginUIs)
        {
            if (ui.gameObject.activeInHierarchy && ui.gameObject.scene == activeScene)
            {
                loginUI = ui;
                break;
            }
        }

        if (loginUI != null)
        {
            loginUI.UpdateStatusText();
            Debug.Log(
                $"[NetworkManager] Directly updated LoginUI status. IsConnected={IsConnected}, UI on '{loginUI.gameObject.name}'"
            );
        }
        else
        {
            Debug.LogWarning($"[NetworkManager] LoginUI not found in any scene!");
        }

        // [Fix] Do NOT start pinging immediately.
        // Wait for S_Login success to ensure we are authenticated.
        // StartCoroutine(CoSendPing());
    }

    void HandleDisconnected()
    {
        Debug.Log("Disconnected from Server");
        // IsConnected is now dynamic property
        _isConnecting = false;

        Debug.Log(
            $"[NetworkManager] OnDisconnected has {(OnDisconnected != null ? "subscribers" : "NO subscribers")}"
        );
        OnDisconnected?.Invoke();

        // [Fix] 직접 LoginUI를 찾아서 갱신 (현재 활성 씬만 검색)
        // FindObjectsOfType은 모든 씬에서 찾으므로, 활성 씬의 GameObject만 필터링
        Scene activeScene = SceneManager.GetActiveScene();
        LoginUI[] loginUIs = GameObject.FindObjectsOfType<LoginUI>(true);

        // [Debug] 모든 로드된 씬과 찾은 LoginUI 로깅
        Debug.Log(
            $"[NetworkManager] ActiveScene: '{activeScene.name}' (loaded={activeScene.isLoaded})"
        );
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            Debug.Log($"[NetworkManager] Loaded Scene[{i}]: '{s.name}' (loaded={s.isLoaded})");
        }
        Debug.Log($"[NetworkManager] Found {loginUIs.Length} LoginUI(s)");
        foreach (var ui in loginUIs)
        {
            Debug.Log(
                $"[NetworkManager]   - LoginUI on GameObject '{ui.gameObject.name}', scene='{ui.gameObject.scene.name}', activeInHierarchy={ui.gameObject.activeInHierarchy}"
            );
        }

        LoginUI loginUI = null;
        foreach (var ui in loginUIs)
        {
            if (ui.gameObject.activeInHierarchy && ui.gameObject.scene == activeScene)
            {
                loginUI = ui;
                break;
            }
        }

        if (loginUI != null)
        {
            loginUI.UpdateStatusText();
            Debug.Log(
                $"[NetworkManager] Directly updated LoginUI status. IsConnected={IsConnected}, UI on '{loginUI.gameObject.name}'"
            );
        }
        else
        {
            Debug.LogWarning($"[NetworkManager] LoginUI not found in any scene!");
        }

        if (_pingCoroutine != null)
        {
            StopCoroutine(_pingCoroutine);
            _pingCoroutine = null;
        }

        // GameManager에 Disconnect 이벤트 전달
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerEvent(StateEvent.Disconnect);
        }

        // Auto-Reconnect
        if (!_isRetrying)
        {
            StartCoroutine(CoRetryConnect());
        }
    }

    public void StartPingCoroutine()
    {
        // Prevent duplicate coroutines if called multiple times
        if (_pingCoroutine != null)
        {
            StopCoroutine(_pingCoroutine);
            _pingCoroutine = null;
        }
        _pingCoroutine = StartCoroutine(CoSendPing());
    }

    IEnumerator CoSendPing()
    {
        while (IsConnected)
        {
            // Do NOT send ping if we are in Login state (unauthenticated)
            if (
                GameManager.Instance != null
                && GameManager.Instance.CurrentState != GameState.Login
            )
            {
                SendPing();
            }
            yield return new WaitForSeconds(1.0f);
        }
        _pingCoroutine = null;
    }

    IEnumerator CoRetryConnect()
    {
        _isRetrying = true;
        while (!IsConnected)
        {
            if (!_isConnecting)
            {
                Debug.Log($"Retrying to connect to {_lastHost}:{_lastPort}...");
                Connect(_lastHost, _lastPort);
            }
            yield return new WaitForSeconds(_retryInterval);
        }
        _isRetrying = false;
    }

    void Update()
    {
        // Job Queue 처리 (스레드 안전)
        lock (_lock)
        {
            while (_jobQueue.Count > 0)
            {
                Action action = _jobQueue.Dequeue();
                action.Invoke();
            }
        }

        // 패킷 처리 (C_GameReady 방식으로 타이밍 제어하므로 항상 Flush)
        PacketManager.Instance.Flush();
    }

    public void PushJob(Action action)
    {
        lock (_lock)
        {
            _jobQueue.Enqueue(action);
        }
    }

    public void Send(Google.Protobuf.IMessage packet)
    {
        if (IsConnected)
            _session.Send(packet);
        else
            Debug.LogWarning("Cannot send packet - Not connected to server");
    }

    // ===================================
    // RTT Calculation
    // ===================================

    public long RTT { get; private set; } = 0; // ms
    private long _lastPingTime = 0;

    public void SendPing()
    {
        if (!IsConnected)
            return;

        C_Ping pingPacket = new C_Ping();
        pingPacket.Timestamp = (long)(Time.realtimeSinceStartupAsDouble * 1000);
        Send(pingPacket);

        _lastPingTime = pingPacket.Timestamp;
    }

    public void UpdateRTT(long serverTimestamp)
    {
        long current = (long)(Time.realtimeSinceStartupAsDouble * 1000);
        long rtt = current - serverTimestamp;

        if (RTT == 0)
            RTT = rtt;
        else
            RTT = (long)(RTT * 0.8f + rtt * 0.2f); // EMA smoothing

        // Debug.Log($"[NetworkManager] RTT Updated: {RTT}ms");
    }
}
