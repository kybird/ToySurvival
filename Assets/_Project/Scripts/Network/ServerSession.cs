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
            // [수신] 원본 버퍼는 공유 RecvBuffer를 가리키므로 복사 후 복호화
            byte[] recvBuffer = new byte[buffer.Count];
            Buffer.BlockCopy(buffer.Array, buffer.Offset, recvBuffer, 0, buffer.Count);

            // XOR-CBC 복호화, 헤더 4바이트 스킵
            byte key = 0xA5;
            for (int i = 4; i < recvBuffer.Length; i++)
            {
                byte cipher = recvBuffer[i];
                recvBuffer[i] = (byte)(cipher ^ key);
                key = cipher;
            }

            PacketManager.Instance.OnRecvPacket(new ArraySegment<byte>(recvBuffer));
        }

        public override void OnSend(int numOfBytes) { }

        public void Send(IMessage packet)
        {
            // Protobuf 클래스명 -> MsgId Enum 변환
            // 예: C_GetRoomList -> CGetRoomList -> MsgId.CGetRoomList
            string name = packet.Descriptor.Name.Replace("_", "");
            if (!Enum.TryParse(typeof(Protocol.MsgId), name, true, out object msgIdObj))
            {
                UnityEngine.Debug.LogError(
                    $"[ServerSession] MsgId 매핑 실패: '{name}' (원본: {packet.Descriptor.Name})"
                );
                return;
            }

            ushort id = (ushort)(Protocol.MsgId)msgIdObj;

            // 바디를 안전하게 직렬화 후 복사
            byte[] body = packet.ToByteArray();
            ushort totalSize = (ushort)(4 + body.Length);
            byte[] sendBuffer = new byte[totalSize];

            // 헤더 기록 (Plain)
            Array.Copy(BitConverter.GetBytes(totalSize), 0, sendBuffer, 0, 2);
            Array.Copy(BitConverter.GetBytes(id), 0, sendBuffer, 2, 2);

            // 바디 복사
            Array.Copy(body, 0, sendBuffer, 4, body.Length);

            // XOR-CBC 암호화, 헤더 4바이트 스킵
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
