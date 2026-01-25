using System;
using System.Collections.Generic;
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

        GameObject myPlayer = ObjectManager.Instance.GetMyPlayer();
        if (myPlayer == null)
            return;

        ClientSidePredictionController csp =
            myPlayer.GetComponent<ClientSidePredictionController>();
        if (csp != null)
        {
            csp.OnPlayerStateAck(res);
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
        Debug.Log("[PacketHandler] Handle_S_SpawnObject Called (Raw)");

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
            ObjectManager.Instance.Spawn(obj, res.ServerTick);

            // [MyPlayer Initial HP Sync]
            if (obj.ObjectId == NetworkManager.Instance.MyPlayerId)
            {
                if (PlayerHUD.Instance != null)
                {
                    PlayerHUD.Instance.UpdateHP(obj.Hp, obj.MaxHp);
                }
            }
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

        for (int i = 0; i < res.ObjectIds.Count; i++)
        {
            int id = res.ObjectIds[i];
            int pickerId = (i < res.PickerIds.Count) ? res.PickerIds[i] : 0;

            Debug.Log($"[PacketHandler] Despawning Object ID: {id}, PickerId: {pickerId}");
            ObjectManager.Instance.Despawn(id, pickerId);
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
        // 이건 디버깅전용 패킷이다.

        // if (TickManager.Instance != null)
        // {
        //     // 비상용 드리프트 체크 (RTT 기반 Soft Sync) - 더 이상 InitGameAnchor는 여기서 하지 않음
        //     float rtt = NetworkManager.Instance != null ? NetworkManager.Instance.RTT : 0;
        //     TickManager.Instance.CheckAndCorrectGameAnchor(res.ServerTick, rtt);

        //     int currentTick = TickManager.Instance.GetCurrentTick();
        //     int estimatedTick = (int)TickManager.Instance.EstimateGameTick();

        //     // 디버깅: 실제 시간 경과 추적
        //     double currentTime = Time.realtimeSinceStartupAsDouble;

        //     Debug.Log(
        //         $"[TickSync] ServerTick: {res.ServerTick} | GameTick: {currentTick} (Est: {estimatedTick}) | Diff: {estimatedTick - (int)res.ServerTick} | RTT: {rtt:F1}ms | RealTime: {currentTime:F3}s"
        //     );
        // }
        // else
        // {
        //     Debug.LogError(
        //         $"[TickSync] Critical Error: TickManager.Instance is NULL! ServerTick: {res.ServerTick}"
        //     );
        // }
    }

    public static void Handle_S_DamageEffect(IMessage packet)
    {
        S_DamageEffect res = (S_DamageEffect)packet;
        for (int i = 0; i < res.TargetIds.Count; i++)
        {
            int targetId = res.TargetIds[i];
            int damage = res.DamageValues[i];

            // ObjectManager를 통해 해당 오브젝트에 데미지 이펙트 출력
            if (ObjectManager.Instance != null)
            {
                ObjectManager.Instance.OnDamage(targetId, damage);
            }
        }
    }

    public static void Handle_S_Knockback(IMessage packet)
    {
        S_Knockback res = (S_Knockback)packet;
        Debug.Log(
            $"[PacketHandler] Knockback: ObjectID={res.ObjectId}, Dir=({res.DirX:F2},{res.DirY:F2}), Force={res.Force}"
        );

        if (ObjectManager.Instance != null)
        {
            ObjectManager.Instance.ApplyKnockback(
                res.ObjectId,
                res.DirX,
                res.DirY,
                res.Force,
                res.Duration
            );
        }
    }

    public static void Handle_S_PlayerDowned(IMessage packet)
    {
        S_PlayerDowned res = (S_PlayerDowned)packet;
        Debug.Log($"[PacketHandler] Player Downed: {res.PlayerId}");

        // 1. 오브젝트 상태 변경 (반투명 등)
        if (ObjectManager.Instance != null)
        {
            ObjectManager.Instance.SetObjectState(res.PlayerId, ObjectState.Downed);
        }

        // 2. 상단 알림 UI 출력
        if (GameUI.Instance != null)
        {
            bool isMe = (res.PlayerId == NetworkManager.Instance.MyPlayerId);
            string msg = isMe ? "You are DOWNED!" : $"Player {res.PlayerId} is DOWNED!";
            GameUI.Instance.ShowNotification(msg, Color.red);

            // 내가 다운되었으면 부활 대기 화면 표시
            if (isMe)
            {
                GameUI.Instance.ShowPlayerDowned();
            }
        }
    }

    public static void Handle_S_PlayerRevive(IMessage packet)
    {
        S_PlayerRevive res = (S_PlayerRevive)packet;
        Debug.Log($"[PacketHandler] Player Revived: {res.PlayerId}");

        // 1. 오브젝트 상태 복구
        if (ObjectManager.Instance != null)
        {
            ObjectManager.Instance.SetObjectState(res.PlayerId, ObjectState.Idle);
        }

        // 2. 상단 알림 UI 출력
        if (GameUI.Instance != null)
        {
            bool isMe = (res.PlayerId == NetworkManager.Instance.MyPlayerId);
            string msg = isMe ? "You are REVIVED!" : $"Player {res.PlayerId} is REVIVED!";
            GameUI.Instance.ShowNotification(msg, Color.green);

            // 내가 부활했으면 부활 대기 화면 숨기기
            if (isMe)
            {
                GameUI.Instance.HidePlayerDowned();
            }
        }
    }

    public static void Handle_S_GameOver(IMessage packet)
    {
        S_GameOver res = (S_GameOver)packet;
        Debug.Log(
            $"[PacketHandler] Game Over! IsWin: {res.IsWin}, SurvivedTime: {res.SurvivedTimeMs}ms"
        );

        // UI 처리 (게임 결과창 띄우기)
        if (GameUI.Instance != null)
        {
            GameUI.Instance.ShowGameOver(res.IsWin, res.SurvivedTimeMs);
        }
    }

    public static void Handle_S_PlayerDead(IMessage packet)
    {
        S_PlayerDead res = (S_PlayerDead)packet;
        Debug.Log($"[PacketHandler] Player Dead: {res.PlayerId}");

        if (NetworkManager.Instance.MyPlayerId == res.PlayerId)
        {
            Debug.Log("[PacketHandler] I died. Triggering Game Over UI.");
            if (GameUI.Instance != null)
            {
                // Show Game Over UI (False = Lose)
                GameUI.Instance.ShowGameOver(false, 0);
            }
            else
            {
                Debug.LogError(
                    "[PacketHandler] CRITICAL: GameUI.Instance is NULL! Cannot show Game Over UI."
                );
            }
        }

        if (ObjectManager.Instance != null)
        {
            ObjectManager.Instance.OnPlayerDead(res.PlayerId);
        }
    }

    public static void Handle_S_ExpChange(IMessage packet)
    {
        S_ExpChange res = (S_ExpChange)packet;
        Debug.Log($"[PacketHandler] Exp Change: {res.CurrentExp}/{res.MaxExp} Lv.{res.Level}");

        if (PlayerHUD.Instance != null)
        {
            PlayerHUD.Instance.UpdateExp(res.CurrentExp, res.MaxExp, res.Level);
        }
    }

    public static void Handle_S_HpChange(IMessage packet)
    {
        S_HpChange res = (S_HpChange)packet;
        Debug.Log(
            $"[PacketHandler] HP Change: {res.CurrentHp}/{res.MaxHp} for ObjectID: {res.ObjectId}"
        );

        // 1. Update UI (My Player Only)
        if (res.ObjectId == NetworkManager.Instance.MyPlayerId)
        {
            if (PlayerHUD.Instance != null)
            {
                PlayerHUD.Instance.UpdateHP((int)res.CurrentHp, (int)res.MaxHp);
            }
        }

        // 2. Update World Object (Effect & HP Bar) - For Everyone (including me)
        if (ObjectManager.Instance != null)
        {
            ObjectManager.Instance.UpdateHp(res.ObjectId, res.CurrentHp, res.MaxHp);
        }
    }

    public static void Handle_S_LevelUpOption(IMessage packet)
    {
        S_LevelUpOption res = (S_LevelUpOption)packet;
        Debug.Log($"[PacketHandler] Level Up Options Received! Count: {res.Options.Count}");

        foreach (var opt in res.Options)
        {
            Debug.Log($"- [{opt.OptionId}] {opt.Name}: {opt.Desc} (New: {opt.IsNew})");
        }

        float timeout = res.TimeoutSeconds > 0 ? res.TimeoutSeconds : 30f;

        if (LevelUpUI.Instance != null)
        {
            var options = new List<Protocol.LevelUpOption>(res.Options);
            float slowRadius = res.SlowRadius > 0 ? res.SlowRadius : 5.0f;
            LevelUpUI.Instance.Show(options, timeout, slowRadius);
        }
        else
        {
            Debug.LogError(
                "[PacketHandler] CRITICAL: LevelUpUI.Instance is NULL! Cannot show Level Up Options."
            );

            // Auto-select option 0 as failsafe
            Debug.LogWarning("[PacketHandler] Auto-selecting Option 0 due to missing UI...");
            C_SelectLevelUp selectPkt = new C_SelectLevelUp();
            selectPkt.OptionIndex = 0;
            NetworkManager.Instance.Send(selectPkt);
        }
    }

    public static void Handle_S_WaveNotify(IMessage packet)
    {
        S_WaveNotify res = (S_WaveNotify)packet;
        Debug.Log($"[PacketHandler] Wave Notify: {res.Title}, Duration: {res.DurationSeconds}s");

        if (WaveNotificationUI.Instance != null)
        {
            WaveNotificationUI.Instance.Show(res.Title, res.DurationSeconds);
        }
        else if (GameUI.Instance != null)
        {
            GameUI.Instance.ShowNotification(res.Title, Color.cyan);
        }
    }
}
