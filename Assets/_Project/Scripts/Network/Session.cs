using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using Google.Protobuf;
using UnityEngine;

namespace Network
{
    public abstract class Session : IDisposable
    {
        protected Socket _socket;
        int _disconnected = 0;
        bool _onCompletedRegistered = false;
        private bool _disposed = false;

        object _lock = new object();
        Queue<ArraySegment<byte>> _sendQueue = new Queue<ArraySegment<byte>>();
        List<ArraySegment<byte>> _pendingList = new List<ArraySegment<byte>>();
        SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs();
        SocketAsyncEventArgs _recvArgs = new SocketAsyncEventArgs();

        RecvBuffer _recvBuffer = new RecvBuffer(65535);

        public abstract void OnConnected(ArraySegment<byte> buffer);
        public abstract int OnRecv(ArraySegment<byte> buffer);
        public abstract void OnSend(int numOfBytes);
        public abstract void OnDisconnected();

        public void Start(Socket socket)
        {
            _socket = socket;
            _disconnected = 0;

            if (_onCompletedRegistered == false)
            {
                _recvArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnRecvCompleted);
                _sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSendCompleted);
                _onCompletedRegistered = true;
            }

            lock (_lock)
            {
                _sendQueue.Clear();
                _pendingList.Clear();
                _sendArgs.BufferList = null;
            }

            _recvBuffer.OnRead(_recvBuffer.DataSize);
            _recvBuffer.Clean();

            RegisterRecv();
        }

        public void Send(ArraySegment<byte> sendBuff)
        {
            lock (_lock)
            {
                _sendQueue.Enqueue(sendBuff);
                if (_pendingList.Count == 0)
                    RegisterSend();
            }
        }

        public bool IsConnected()
        {
            if (_disconnected == 1)
            {
                // Debug.Log("[Session] IsConnected=false (_disconnected=1)");
                return false;
            }

            if (_socket == null)
            {
                // Debug.Log("[Session] IsConnected=false (_socket=null)");
                return false;
            }

            try
            {
                // Poll check: if SelectRead returns true and Available is 0, connection is closed.
                bool pollResult = _socket.Poll(1000, SelectMode.SelectRead);
                bool availableZero = _socket.Available == 0;
                bool connected = !(pollResult && availableZero);
                // Debug.Log($"[Session] IsConnected={connected} (Poll={pollResult}, AvailableZero={availableZero})");
                return connected;
            }
            catch (Exception e)
            {
                Debug.Log($"[Session] IsConnected=false (Exception: {e.Message})");
                return false;
            }
        }

        public void Disconnect()
        {
            if (Interlocked.Exchange(ref _disconnected, 1) == 1)
                return;

            OnDisconnected();
            try
            {
                if (_socket != null)
                {
                    _socket.Shutdown(SocketShutdown.Both);
                    _socket.Close();
                    _socket = null;
                }
            }
            catch { }

            Clear();
        }

        void Clear()
        {
            lock (_lock)
            {
                _sendQueue.Clear();
                _pendingList.Clear();
            }
        }

        #region Network Communication

        void RegisterSend()
        {
            if (_disconnected == 1)
                return;

            while (_sendQueue.Count > 0)
            {
                ArraySegment<byte> buff = _sendQueue.Dequeue();
                _pendingList.Add(buff);
            }
            _sendArgs.BufferList = _pendingList;

            try
            {
                bool pending = _socket.SendAsync(_sendArgs);
                if (pending == false)
                    OnSendCompleted(null, _sendArgs);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"RegisterSend Failed {e}");
            }
        }

        void OnSendCompleted(object sender, SocketAsyncEventArgs args)
        {
            lock (_lock)
            {
                if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
                {
                    _sendArgs.BufferList = null;
                    _pendingList.Clear();

                    OnSend(_sendArgs.BytesTransferred);

                    if (_sendQueue.Count > 0)
                        RegisterSend();
                }
                else
                {
                    Disconnect();
                }
            }
        }

        void RegisterRecv()
        {
            if (_disconnected == 1)
                return;

            _recvBuffer.Clean();
            ArraySegment<byte> segment = _recvBuffer.WriteSegment;
            _recvArgs.SetBuffer(segment.Array, segment.Offset, segment.Count);

            try
            {
                bool pending = _socket.ReceiveAsync(_recvArgs);
                if (pending == false)
                    OnRecvCompleted(null, _recvArgs);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"RegisterRecv Failed {e}");
            }
        }

        void OnRecvCompleted(object sender, SocketAsyncEventArgs args)
        {
            if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
            {
                if (_recvBuffer.OnWrite(args.BytesTransferred) == false)
                {
                    UnityEngine.Debug.LogError(
                        $"[Session] Disconnected: RecvBuffer OnWrite failed (Size: {args.BytesTransferred})"
                    );
                    Disconnect();
                    return;
                }

                int processLen = OnRecv(_recvBuffer.ReadSegment);
                if (processLen < 0 || _recvBuffer.DataSize < processLen)
                {
                    UnityEngine.Debug.LogError(
                        $"[Session] Disconnected: OnRecv ProcessLen error ({processLen})"
                    );
                    Disconnect();
                    return;
                }

                if (_recvBuffer.OnRead(processLen) == false)
                {
                    UnityEngine.Debug.LogError($"[Session] Disconnected: RecvBuffer OnRead failed");
                    Disconnect();
                    return;
                }

                RegisterRecv();
            }
            else
            {
                UnityEngine.Debug.LogError(
                    $"[Session] Disconnected: Recv Failed (Bytes: {args.BytesTransferred}, Error: {args.SocketError})"
                );
                Disconnect();
            }
        }

        #endregion

        #region Dispose Pattern

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Disconnect first
                Disconnect();

                // Dispose SocketAsyncEventArgs
                if (_sendArgs != null)
                {
                    _sendArgs.Dispose();
                    _sendArgs = null;
                }

                if (_recvArgs != null)
                {
                    _recvArgs.Dispose();
                    _recvArgs = null;
                }

                // Clear collections
                lock (_lock)
                {
                    _sendQueue?.Clear();
                    _pendingList?.Clear();
                }
            }

            _disposed = true;
        }

        #endregion
    }

    public abstract class PacketSession : Session
    {
        public static readonly int HeaderSize = 4;

        // [size(2)][id(2)]
        public sealed override int OnRecv(ArraySegment<byte> buffer)
        {
            int processLen = 0;

            while (true)
            {
                if (buffer.Count < HeaderSize)
                    break;

                ushort dataSize = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
                if (buffer.Count < dataSize)
                    break;

                OnRecvPacket(new ArraySegment<byte>(buffer.Array, buffer.Offset, dataSize));

                processLen += dataSize;
                buffer = new ArraySegment<byte>(
                    buffer.Array,
                    buffer.Offset + dataSize,
                    buffer.Count - dataSize
                );
            }

            return processLen;
        }

        public abstract void OnRecvPacket(ArraySegment<byte> buffer);
    }
}
