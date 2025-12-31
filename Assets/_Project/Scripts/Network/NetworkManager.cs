using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Network;
using UnityEngine;
using Core;

public class NetworkManager : MonoBehaviour
{
    static NetworkManager _instance;
    public static NetworkManager Instance { get { return _instance; } }

    ServerSession _session = new ServerSession();
    public ServerSession Session { get { return _session; } }

    public bool IsConnected { get; private set; } = false;
    public int MyPlayerId { get; set; }
    public float MapWidth { get; set; }
    public float MapHeight { get; set; }
    public Action OnConnected { get; set; }
    public Action OnDisconnected { get; set; }

    private string _lastHost;
    private int _lastPort;
    private bool _isConnecting = false;
    private bool _isRetrying = false;
    private float _retryInterval = 2.0f;
    private Queue<Action> _jobQueue = new Queue<Action>();
    private object _lock = new object();

    void Awake()
    {
        Debug.Log($"[NetworkManager] Awake called. Current instance: {(_instance != null ? "EXISTS" : "NULL")}");
        
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
            Debug.LogWarning("[NetworkManager] Duplicate instance detected - Destroying this GameObject");
            Destroy(gameObject);
        }
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
        connector.Connect(endPoint, () => 
        {
            _session.OnConnectedCallback = () => PushJob(HandleConnected);
            _session.OnDisconnectedCallback = () => PushJob(HandleDisconnected);
            return _session;
        });
    }

    void HandleConnected()
    {
        Debug.Log("Connected to Server");
        IsConnected = true;
        _isConnecting = false;
        _isRetrying = false;
        OnConnected?.Invoke();
    }

    void HandleDisconnected()
    {
        Debug.Log("Disconnected from Server");
        IsConnected = false;
        _isConnecting = false;
        OnDisconnected?.Invoke();

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
}
