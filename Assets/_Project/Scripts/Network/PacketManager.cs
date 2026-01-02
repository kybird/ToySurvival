using System;
using System.Collections.Generic;
using Google.Protobuf;
using Protocol;

namespace Network
{
    public class PacketManager
    {
        #region Singleton
        static PacketManager _instance = new PacketManager();
        public static PacketManager Instance { get { return _instance; } }
        #endregion

        PacketManager()
        {
            Register();
        }

        Dictionary<ushort, Action<ArraySegment<byte>, ushort>> _onRecv = new Dictionary<ushort, Action<ArraySegment<byte>, ushort>>();
        Dictionary<ushort, Action<IMessage>> _handler = new Dictionary<ushort, Action<IMessage>>();

        Queue<Action> _packetQueue = new Queue<Action>();
        object _lock = new object();

        public Action<IMessage> CustomHandler { get; set; }

        public void Register()
        {
            // Register parsers and handlers
            _onRecv.Add((ushort)Protocol.MsgId.SLogin, MakePacket<S_Login>);
            _handler.Add((ushort)Protocol.MsgId.SLogin, PacketHandler.Handle_S_Login);

            _onRecv.Add((ushort)Protocol.MsgId.SMoveObjectBatch, MakePacket<S_MoveObjectBatch>);
            _handler.Add((ushort)Protocol.MsgId.SMoveObjectBatch, PacketHandler.Handle_S_MoveObjectBatch);

            _onRecv.Add((ushort)Protocol.MsgId.SCreateRoom, MakePacket<S_CreateRoom>);
            _handler.Add((ushort)Protocol.MsgId.SCreateRoom, PacketHandler.Handle_S_CreateRoom);

            _onRecv.Add((ushort)Protocol.MsgId.SRoomList, MakePacket<S_RoomList>);
            _handler.Add((ushort)Protocol.MsgId.SRoomList, PacketHandler.Handle_S_RoomList);

            _onRecv.Add((ushort)Protocol.MsgId.SJoinRoom, MakePacket<S_JoinRoom>);
            _handler.Add((ushort)Protocol.MsgId.SJoinRoom, PacketHandler.Handle_S_JoinRoom);

            _onRecv.Add((ushort)Protocol.MsgId.SSpawnObject, MakePacket<S_SpawnObject>);
            _handler.Add((ushort)Protocol.MsgId.SSpawnObject, PacketHandler.Handle_S_SpawnObject);

            _onRecv.Add((ushort)Protocol.MsgId.SDespawnObject, MakePacket<S_DespawnObject>);
            _handler.Add((ushort)Protocol.MsgId.SDespawnObject, PacketHandler.Handle_S_DespawnObject);

            _onRecv.Add((ushort)Protocol.MsgId.SPing, MakePacket<S_Ping>);
            _handler.Add((ushort)Protocol.MsgId.SPing, PacketHandler.Handle_S_Ping);

            _onRecv.Add((ushort)Protocol.MsgId.SPlayerStateAck, MakePacket<S_PlayerStateAck>);
            _handler.Add((ushort)Protocol.MsgId.SPlayerStateAck, PacketHandler.Handle_S_PlayerStateAck);
        }

        public void OnRecvPacket(ArraySegment<byte> buffer, Action<IMessage> onRecvCallback = null)
        {
            ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
            ushort id = BitConverter.ToUInt16(buffer.Array, buffer.Offset + 2);

            UnityEngine.Debug.Log($"[PacketManager] OnRecvPacket - ID: {id}, Size: {size}");

            Action<ArraySegment<byte>, ushort> action = null;
            if (_onRecv.TryGetValue(id, out action))
                action.Invoke(buffer, id);
            else
                UnityEngine.Debug.LogWarning($"[PacketManager] No parser for packet ID: {id}");
        }

        void MakePacket<T>(ArraySegment<byte> buffer, ushort id) where T : IMessage, new()
        {
            T pkt = new T();
            pkt.MergeFrom(new ArraySegment<byte>(buffer.Array, buffer.Offset + 4, buffer.Count - 4));

            // UnityEngine.Debug.Log($"[PacketManager] MakePacket - Type: {typeof(T).Name}, ID: {id}");

            if (CustomHandler != null)
            {
                lock (_lock)
                {
                    _packetQueue.Enqueue(() => CustomHandler.Invoke(pkt));
                }
            }
            else
            {
                Action<IMessage> action = null;
                if (_handler.TryGetValue(id, out action))
                {
                    lock (_lock)
                    {
                        // UnityEngine.Debug.Log($"[PacketManager] Enqueuing handler for {typeof(T).Name}");
                        _packetQueue.Enqueue(() => action.Invoke(pkt));
                    }
                }
            }
        }

        public void Flush()
        {
            lock (_lock)
            {
                int count = _packetQueue.Count;
                if (count > 0)
                    UnityEngine.Debug.Log($"[PacketManager] Flush - Processing {count} packets");
                
                while (_packetQueue.Count > 0)
                {
                    Action action = _packetQueue.Dequeue();
                    try
                    {
                        action.Invoke();
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogError($"[PacketManager] Exception in handler: {e.Message}\n{e.StackTrace}");
                    }
                }
            }
        }

        public Action<IMessage> GetPacketHandler(ushort id)
        {
            Action<IMessage> action = null;
            if (_handler.TryGetValue(id, out action))
                return action;
            return null;
        }
    }
}
