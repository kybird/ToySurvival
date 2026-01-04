using System;
using Core;
using Google.Protobuf;
using Network;
using Protocol;
using UnityEngine;

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

            Debug.Log($"[PacketHandler] MyPlayerId SET to: {NetworkManager.Instance.MyPlayerId}");

            // 동적 서버 Tick 설정 저장
            if (res.ServerTickRate > 0)
            {
                NetworkManager.Instance.ServerTickRate = (int)res.ServerTickRate;
                NetworkManager.Instance.ServerTickInterval = res.ServerTickInterval;

                // TickManager 초기화 (Global Anchor 설정)
                if (TickManager.Instance != null)
                {
                    // Global Timer 시작 (로비 시간)
                    TickManager.Instance.InitializeGlobal(
                        (int)res.ServerTickRate,
                        res.ServerTickInterval,
                        res.ServerTick
                    );
                }

                Debug.Log(
                    $"[PacketHandler] S_Login Received. Rate: {res.ServerTickRate}, Interval: {res.ServerTickInterval:F6}. Global Tick Started."
                );
            }

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
        if (
            GameManager.Instance != null
            && !GameManager.Instance.IsPacketAllowed(
                (int)MsgId.SRoomList,
                new[] { GameState.Lobby }
            )
        )
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
        if (
            GameManager.Instance != null
            && !GameManager.Instance.IsPacketAllowed(
                (int)MsgId.SMoveObjectBatch,
                new[] { GameState.InGame }
            )
        )
        {
            // 1. 여기서 확실하게 에러를 던져서 서버 개발자(나 자신)에게 경고를 줍니다.
            Debug.LogError(
                $"[Protocol Violation] SMoveObjectBatch received in {GameManager.Instance.CurrentState} state!"
            );
            return;
        }

        S_MoveObjectBatch res = (S_MoveObjectBatch)packet;
        if (ObjectManager.Instance == null)
            return;

        foreach (ObjectPos pos in res.Moves)
        {
            if (pos.ObjectId == NetworkManager.Instance.MyPlayerId)
                continue;

            ObjectManager.Instance.UpdatePos(pos, res.ServerTick);
        }
    }

    public static void Handle_S_PlayerStateAck(IMessage packet)
    {
        S_PlayerStateAck res = (S_PlayerStateAck)packet;

        // 서버의 위치 보정/검증 패킷
        if (ObjectManager.Instance != null)
        {
            // 로컬 플레이어 객체 찾기
            GameObject myPlayer = ObjectManager.Instance.GetMyPlayer();
            if (myPlayer != null)
            {
                // myPlayer.transform.position = new Vector3(res.X, res.Y, 0);

                // Reconciliation 현상태에선 무시
                // 현재 클라이언트 위치
                // Vector3 clientPos = myPlayer.transform.position;
                // Vector2 serverPos = new Vector2(res.X, res.Y);
                // float distance = Vector2.Distance(clientPos, serverPos);

                // Debug.Log(
                //     $"[PlayerPos] Server: ({res.X:F2}, {res.Y:F2}) | Client: ({clientPos.x:F2}, {clientPos.y:F2}) | Diff: {distance:F2} | ClientTick: {res.ClientTick} | ServerTick: {res.ServerTick}"
                // );

                // ClientSidePredictionController csp =
                //     myPlayer.GetComponent<ClientSidePredictionController>();
                // if (csp != null)
                // {
                //     csp.OnServerCorrection(res.X, res.Y, res.ServerTick, res.ClientTick);
                // }
            }
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
        if (
            GameManager.Instance != null
            && !GameManager.Instance.IsPacketAllowed(
                (int)MsgId.SSpawnObject,
                new[] { GameState.InGame }
            )
        )
        {
            return;
        }

        S_SpawnObject res = (S_SpawnObject)packet;

        if (ObjectManager.Instance == null)
        {
            Debug.LogError("[PacketHandler] ObjectManager.Instance is NULL! Cannot spawn objects.");
            return;
        }

        Debug.Log(
            $"[PacketHandler] S_SpawnObject received. Total objects: {res.Objects.Count}, ServerTick: {res.ServerTick}"
        );

        // [Tick Sync] 게임 진입 후 첫 스폰 패킷으로 "게임 시간" 앵커링
        if (TickManager.Instance != null && res.ServerTick > 0)
        {
            TickManager.Instance.InitGameAnchor(res.ServerTick);
        }

        foreach (ObjectInfo obj in res.Objects)
        {
            Debug.Log(
                $"[PacketHandler] Spawning - Type: {obj.Type}, ID: {obj.ObjectId}, Pos: ({obj.X}, {obj.Y})"
            );
            ObjectManager.Instance.Spawn(obj);
        }
    }

    public static void Handle_S_DespawnObject(IMessage packet)
    {
        // 상태 체크 (InGame에서만 처리)
        if (
            GameManager.Instance != null
            && !GameManager.Instance.IsPacketAllowed(
                (int)MsgId.SDespawnObject,
                new[] { GameState.InGame }
            )
        )
        {
            return;
        }

        S_DespawnObject res = (S_DespawnObject)packet;
        if (ObjectManager.Instance == null)
            return;

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

    public static void Handle_S_Pong(IMessage packet)
    {
        S_Pong pong = (S_Pong)packet;
        NetworkManager.Instance.UpdateRTT(pong.Timestamp);
    }

    public static void Handle_S_DebugServerTick(IMessage packet)
    {
        S_DebugServerTick res = (S_DebugServerTick)packet;

        if (TickManager.Instance != null)
        {
            // 비상용 드리프트 체크 (RTT 기반 Soft Sync) - 더 이상 InitGameAnchor는 여기서 하지 않음
            float rtt = NetworkManager.Instance != null ? NetworkManager.Instance.RTT : 0;
            TickManager.Instance.CheckAndCorrectGameAnchor(res.ServerTick, rtt);

            int currentTick = TickManager.Instance.GetCurrentTick();
            int estimatedTick = (int)TickManager.Instance.EstimateGameTick();

            // 디버깅: 실제 시간 경과 추적
            double currentTime = Time.realtimeSinceStartupAsDouble;

            Debug.Log(
                $"[TickSync] ServerTick: {res.ServerTick} | GameTick: {currentTick} (Est: {estimatedTick}) | Diff: {estimatedTick - (int)res.ServerTick} | RTT: {rtt:F1}ms | RealTime: {currentTime:F3}s"
            );
        }
        else
        {
            Debug.LogError(
                $"[TickSync] Critical Error: TickManager.Instance is NULL! ServerTick: {res.ServerTick}"
            );
        }
    }
}
