using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using TennisTraining.Core;

namespace TennisTraining.Launcher
{
    /// <summary>
    /// 串口传输层：通过 RS-232/485 串口与发球机下位机通信（Modbus RTU）。
    /// 真实硬件可经 USB-串口转接或蓝牙串口模块接入。
    /// </summary>
    public sealed class SerialLauncherTransport : ILauncherTransport
    {
        private readonly object _lock = new();
        private readonly string _portName;
        private SerialPort _port;

        public SerialLauncherTransport(string portName)
        {
            _portName = portName ?? throw new ArgumentNullException(nameof(portName));
        }

        public string Name => $"Serial:{_portName}";
        public bool IsConnected { get; private set; }

        public bool Connect()
        {
            lock (_lock)
            {
                try
                {
                    if (_port != null && _port.IsOpen) { IsConnected = true; return true; }
                    _port = new SerialPort(_portName, 115200, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout = 500,
                        WriteTimeout = 500,
                        Handshake = Handshake.None,
                        DtrEnable = false,
                        RtsEnable = false
                    };
                    _port.Open();
                    IsConnected = true;
                    return true;
                }
                catch
                {
                    IsConnected = false;
                    return false;
                }
            }
        }

        public void Disconnect()
        {
            lock (_lock)
            {
                try { _port?.Close(); _port?.Dispose(); } catch { }
                _port = null;
                IsConnected = false;
            }
        }

        public bool Send(byte[] frame)
        {
            if (frame == null || frame.Length == 0) return false;
            lock (_lock)
            {
                if (!IsConnected || _port == null || !_port.IsOpen) return false;
                try
                {
                    _port.Write(frame, 0, frame.Length);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public byte[] Receive(int timeoutMs = 200)
        {
            lock (_lock)
            {
                if (!IsConnected || _port == null || !_port.IsOpen) return Array.Empty<byte>();
                int oldTo = _port.ReadTimeout;
                _port.ReadTimeout = timeoutMs;
                try
                {
                    if (_port.BytesToRead <= 0) return Array.Empty<byte>();
                    var buf = new byte[Math.Min(_port.BytesToRead, 256)];
                    int read = _port.Read(buf, 0, buf.Length);
                    var result = new byte[read];
                    Buffer.BlockCopy(buf, 0, result, 0, read);
                    return result;
                }
                catch (TimeoutException) { return Array.Empty<byte>(); }
                catch { return Array.Empty<byte>(); }
                finally { _port.ReadTimeout = oldTo; }
            }
        }

        /// <summary>枚举本机可用串口。</summary>
        public static IEnumerable<string> EnumeratePorts()
        {
            try { return SerialPort.GetPortNames(); }
            catch { return Array.Empty<string>(); }
        }

        public void Dispose() => Disconnect();
    }
}
