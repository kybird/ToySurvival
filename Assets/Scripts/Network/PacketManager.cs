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
            _onRecv.Add((ushort)MsgId.LoginResponse, MakePacket<LoginResponse>);
            _handler.Add((ushort)MsgId.LoginResponse, PacketHandler.Handle_LoginResponse);

            _onRecv.Add((ushort)MsgId.ScMove, MakePacket<SC_Move>);
            _handler.Add((ushort)MsgId.ScMove, PacketHandler.Handle_SC_Move);

            _onRecv.Add((ushort)MsgId.ScEnterRoom, MakePacket<SC_EnterRoom>);
            _handler.Add((ushort)MsgId.ScEnterRoom, PacketHandler.Handle_SC_EnterRoom);
        }

        public void OnRecvPacket(ArraySegment<byte> buffer, Action<IMessage> onRecvCallback = null)
        {
            ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
            ushort id = BitConverter.ToUInt16(buffer.Array, buffer.Offset + 2);

            Action<ArraySegment<byte>, ushort> action = null;
            if (_onRecv.TryGetValue(id, out action))
                action.Invoke(buffer, id);
        }

        void MakePacket<T>(ArraySegment<byte> buffer, ushort id) where T : IMessage, new()
        {
            T pkt = new T();
            pkt.MergeFrom(new ArraySegment<byte>(buffer.Array, buffer.Offset + 4, buffer.Count - 4));

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
                        _packetQueue.Enqueue(() => action.Invoke(pkt));
                    }
                }
            }
        }

        public void Flush()
        {
            lock (_lock)
            {
                while (_packetQueue.Count > 0)
                {
                    Action action = _packetQueue.Dequeue();
                    action.Invoke();
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

    public enum MsgId : ushort
    {
        LoginRequest = 1,
        LoginResponse = 2,
        CsMove = 3,
        ScMove = 4,
        ScEnterRoom = 5,
    }
}
