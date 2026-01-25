using System;
using System.Net;
using System.Net.Sockets;
using Google.Protobuf;
using Network;
using Protocol;

namespace Network
{
    public class ServerSession : PacketSession
    {
        public Action OnConnectedCallback { get; set; }
        public Action OnDisconnectedCallback { get; set; }

        public override void OnConnected(ArraySegment<byte> buffer)
        {
            OnConnectedCallback?.Invoke();
        }

        public override void OnDisconnected()
        {
            OnDisconnectedCallback?.Invoke();
        }

        public override void OnRecvPacket(ArraySegment<byte> buffer)
        {
            // Decrypt packet (Simple XOR-CBC Feedback)
            byte[] data = buffer.Array;
            int offset = buffer.Offset;
            int count = buffer.Count;

            // Skip first 4 bytes (Header: Size + ID) to allow server to parse packet size
            byte key = 0xA5;
            for (int i = 4; i < count; i++)
            {
                byte cipher = data[offset + i];
                byte plain = (byte)(cipher ^ key);
                data[offset + i] = plain;
                key = cipher;
            }

            // Push to queue for main thread processing
            PacketManager.Instance.OnRecvPacket(buffer);
        }

        public override void OnSend(int numOfBytes) { }

        public void Send(IMessage packet)
        {
            string name = packet.Descriptor.Name.Replace("_", "");
            Protocol.MsgId msgId = (Protocol.MsgId)Enum.Parse(typeof(Protocol.MsgId), name, true);
            ushort id = (ushort)msgId;

            // [DEBUG] Log Packet Sent
            UnityEngine.Debug.Log(
                $"[Client] Sending Packet: {packet.Descriptor.Name} -> MappedName: {name} -> ID: {id}"
            );

            ushort size = (ushort)packet.CalculateSize();
            byte[] sendBuffer = new byte[size + 4];
            Array.Copy(BitConverter.GetBytes((ushort)(size + 4)), 0, sendBuffer, 0, 2);
            Array.Copy(BitConverter.GetBytes(id), 0, sendBuffer, 2, 2);
            packet.WriteTo(new ArraySegment<byte>(sendBuffer, 4, size));

            // Encrypt packet (Simple XOR-CBC Feedback)
            // Skip first 4 bytes (Header: Size + ID) to allow server to parse packet size
            byte key = 0xA5;
            for (int i = 4; i < sendBuffer.Length; i++)
            {
                byte plain = sendBuffer[i];
                byte cipher = (byte)(plain ^ key);
                sendBuffer[i] = cipher;
                key = cipher;
            }

            Send(new ArraySegment<byte>(sendBuffer));
        }
    }
}
