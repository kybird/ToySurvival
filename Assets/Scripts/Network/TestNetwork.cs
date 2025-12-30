using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Protocol;

public class TestNetwork : MonoBehaviour
{
    public string ip = "127.0.0.1";
    public int port = 7777;

    void Start()
    {
        // Try to connect to server
        Debug.Log($"Connecting to {ip}:{port}...");
        NetworkManager.Instance.Connect(ip, port);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            Debug.Log("Sending LoginRequest...");
            LoginRequest req = new LoginRequest()
            {
                Username = "TestUser",
                Password = "Password123"
            };
            NetworkManager.Instance.Send(req);
        }

        if (Input.GetKeyDown(KeyCode.M))
        {
            Debug.Log("Sending CS_Move...");
            CS_Move move = new CS_Move()
            {
                X = 10.0f,
                Y = 20.0f
            };
            NetworkManager.Instance.Send(move);
        }
    }
}
