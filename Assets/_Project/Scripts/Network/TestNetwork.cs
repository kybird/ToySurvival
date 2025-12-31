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
            Debug.Log("Sending C_Login...");
            C_Login req = new C_Login()
            {
                Username = "TestUser",
                Password = "Password123"
            };
            NetworkManager.Instance.Send(req);
        }

        if (Input.GetKeyDown(KeyCode.M))
        {
            Debug.Log("Sending C_Move...");
            C_Move move = new C_Move()
            {
                DirX = 1.0f,
                DirY = 0.0f
            };
            NetworkManager.Instance.Send(move);
        }
    }
}
