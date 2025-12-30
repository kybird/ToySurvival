using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using Network;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    static NetworkManager _instance;
    public static NetworkManager Instance { get { return _instance; } }

    ServerSession _session = new ServerSession();
    public ServerSession Session { get { return _session; } }

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        // PacketManager.Instance.Register(); // 싱글톤 생성자에서 이미 호출됨
    }

    void Start()
    {
        // For testing, we connect immediately. In a real game, you might call this from a UI.
        // Connect("127.0.0.1", 7777); 
    }

    public void Connect(string host, int port)
    {
        IPAddress ipAddr = IPAddress.Parse(host);
        IPEndPoint endPoint = new IPEndPoint(ipAddr, port);

        Connector connector = new Connector();
        connector.Connect(endPoint, () => _session);
    }

    void Update()
    {
        PacketManager.Instance.Flush();
    }

    public void Send(Google.Protobuf.IMessage packet)
    {
        _session.Send(packet);
    }
}
