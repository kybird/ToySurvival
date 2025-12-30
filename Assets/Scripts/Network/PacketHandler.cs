using System;
using Google.Protobuf;
using Network;
using Protocol;
using UnityEngine;

public class PacketHandler
{
    public static void Handle_LoginResponse(IMessage packet)
    {
        LoginResponse res = (LoginResponse)packet;
        if (res.Success)
        {
            Debug.Log($"Login Success! SessionId: {res.SessionId}");
            // Handle spawn or transition
            UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyScene");
        }
        else
        {
            Debug.Log("Login Failed.");
        }
    }

    public static void Handle_SC_Move(IMessage packet)
    {
        SC_Move res = (SC_Move)packet;
        // Update entity position in game world
    }

    public static void Handle_SC_EnterRoom(IMessage packet)
    {
        SC_EnterRoom res = (SC_EnterRoom)packet;
        Debug.Log($"Entered Room: {res.RoomId}");
        UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
    }
}
