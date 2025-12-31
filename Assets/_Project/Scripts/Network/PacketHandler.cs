using System;
using Google.Protobuf;
using Network;
using Protocol;
using UnityEngine;
using Core;

public class PacketHandler
{
    public static void Handle_S_Login(IMessage packet)
    {
        S_Login res = (S_Login)packet;
        if (res.Success)
        {
            Debug.Log($"Login Success! MyPlayerId: {res.MyPlayerId}");
            
            // 정보 저장
            NetworkManager.Instance.MyPlayerId = res.MyPlayerId;
            NetworkManager.Instance.MapWidth = res.MapWidth;
            NetworkManager.Instance.MapHeight = res.MapHeight;

            // GameManager를 통한 상태 전이
            GameManager.Instance.TriggerEvent(StateEvent.LoginSuccess);
        }
        else
        {
            Debug.Log("Login Failed.");
            GameManager.Instance.TriggerEvent(StateEvent.LoginFail);
        }
    }

    public static void Handle_S_CreateRoom(IMessage packet)
    {
        S_CreateRoom res = (S_CreateRoom)packet;
        if (res.Success)
        {
            Debug.Log($"[PacketHandler] Room Created! ID: {res.RoomId}");
            if (LobbyUI.Instance != null)
            {
                LobbyUI.Instance.OnCreateRoomSuccess(res.RoomId);
            }
        }
        else
        {
            Debug.LogError("[PacketHandler] Failed to create room.");
        }
    }

    public static void Handle_S_RoomList(IMessage packet)
    {
        // 상태 체크 (Lobby에서만 처리)
        if (GameManager.Instance != null && 
            !GameManager.Instance.IsPacketAllowed((int)MsgId.SRoomList, new[] { GameState.Lobby }))
        {
            return;
        }

        S_RoomList res = (S_RoomList)packet;
        Debug.Log($"[PacketHandler] Received Room List. Count: {res.Rooms.Count}");
        
        if (LobbyUI.Instance != null)
        {
            LobbyUI.Instance.UpdateRoomList(res.Rooms);
        }
    }

    public static void Handle_S_MoveObjectBatch(IMessage packet)
    {
        // 상태 체크 (InGame에서만 처리)
        if (GameManager.Instance != null && 
            !GameManager.Instance.IsPacketAllowed((int)MsgId.SMoveObjectBatch, new[] { GameState.InGame }))
        {
            return;
        }

        S_MoveObjectBatch res = (S_MoveObjectBatch)packet;
        if (ObjectManager.Instance == null) return;

        foreach (ObjectPos pos in res.Moves)
        {
            ObjectManager.Instance.UpdatePos(pos);
        }
    }

    public static void Handle_S_JoinRoom(IMessage packet)
    {
        S_JoinRoom res = (S_JoinRoom)packet;
        if (res.Success)
        {
            Debug.Log($"Entered Room: {res.RoomId}");
            
            // GameManager를 통한 상태 전이 (Loading -> InGame)
            GameManager.Instance.TriggerEvent(StateEvent.JoinRoomSuccess);
        }
        else
        {
            Debug.LogError("Failed to join room.");
            GameManager.Instance.TriggerEvent(StateEvent.JoinRoomFail);
        }
    }

    public static void Handle_S_SpawnObject(IMessage packet)
    {
        // 상태 체크 (InGame에서만 처리 - C_GameReady 이후에만 서버가 전송)
        if (GameManager.Instance != null && 
            !GameManager.Instance.IsPacketAllowed((int)MsgId.SSpawnObject, new[] { GameState.InGame }))
        {
            return;
        }

        S_SpawnObject res = (S_SpawnObject)packet;
        
        if (ObjectManager.Instance == null)
        {
            Debug.LogError("[PacketHandler] ObjectManager.Instance is NULL! Cannot spawn objects.");
            return;
        }

        Debug.Log($"[PacketHandler] S_SpawnObject received. Total objects: {res.Objects.Count}");
        
        foreach (ObjectInfo obj in res.Objects)
        {
            Debug.Log($"[PacketHandler] Spawning - Type: {obj.Type}, ID: {obj.ObjectId}, Pos: ({obj.X}, {obj.Y})");
            ObjectManager.Instance.Spawn(obj);
        }
    }

    public static void Handle_S_DespawnObject(IMessage packet)
    {
        // 상태 체크 (InGame에서만 처리)
        if (GameManager.Instance != null && 
            !GameManager.Instance.IsPacketAllowed((int)MsgId.SDespawnObject, new[] { GameState.InGame }))
        {
            return;
        }

        S_DespawnObject res = (S_DespawnObject)packet;
        if (ObjectManager.Instance == null) return;

        foreach (int id in res.ObjectIds)
        {
            ObjectManager.Instance.Despawn(id);
        }
    }

    public static void Handle_S_Ping(IMessage packet)
    {
        // Ping/Pong은 모든 상태에서 처리
        S_Ping ping = (S_Ping)packet;
        C_Pong pong = new C_Pong();
        pong.Timestamp = ping.Timestamp;
        NetworkManager.Instance.Send(pong);
    }
}
