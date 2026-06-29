using System;
using System.Collections.Generic;
using TennisTraining.Core;

namespace TennisTraining.Launcher
{
    /// <summary>
    /// Mock 传输层：无真实硬件时使用。记录所有收发帧，便于自检与回放。
    /// 通过 <see cref="Inject"/> 可注入下位机应答字节。
    /// </summary>
    public sealed class MockLauncherTransport : ILauncherTransport
    {
        private readonly object _lock = new();
        private readonly Queue<byte[]> _inbox = new();
        private readonly List<byte[]> _sent = new();

        public string Name => "Mock";
        public bool IsConnected { get; private set; }
        public IReadOnlyList<byte[]> SentFrames => _sent;

        public bool Connect()
        {
            lock (_lock) { IsConnected = true; }
            return true;
        }

        public void Disconnect()
        {
            lock (_lock) { IsConnected = false; }
        }

        public bool Send(byte[] frame)
        {
            lock (_lock)
            {
                if (!IsConnected) return false;
                var copy = new byte[frame.Length];
                Buffer.BlockCopy(frame, 0, copy, 0, frame.Length);
                _sent.Add(copy);
                return true;
            }
        }

        public byte[] Receive(int timeoutMs = 200)
        {
            lock (_lock)
            {
                return _inbox.Count > 0 ? _sent.Count > 0 ? _inbox.Dequeue() : Array.Empty<byte>() : Array.Empty<byte>();
            }
        }

        /// <summary>注入下位机应答，供 Receive 取出。</summary>
        public void Inject(byte[] data)
        {
            lock (_lock) _inbox.Enqueue(data);
        }

        public void Reset()
        {
            lock (_lock) { _sent.Clear(); _inbox.Clear(); }
        }

        public void Dispose() => Disconnect();
    }
}
