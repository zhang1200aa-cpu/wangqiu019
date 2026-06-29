using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TennisTraining.Core;

namespace TennisTraining.Launcher
{
    /// <summary>
    /// 发球机控制引擎：参数计算 + 硬件通信 + 动作执行。
    /// 下发流程与小程序 serveControl.vue 一致：
    ///   1) 会话配置（0x10 @0x001A）
    ///   2) 球参数（0x10 @0x0021，分 3 包）
    ///   3) 设备控制（0x06 @0x0003）开始
    /// </summary>
    public sealed partial class LauncherControlEngine : TennisModuleBase
    {
        public override string Name => "发球机控制";

        private readonly ILauncherTransport _transport;
        private readonly int _interFrameDelayMs;
        private readonly System.Threading.Timer _heartbeat;
        private volatile bool _disposed;
        private LauncherStatus _status = new();

        public ILauncherTransport Transport => _transport;
        public LauncherStatus CurrentStatus => _status;

        public LauncherControlEngine(ILauncherTransport transport, EventBus bus = null, Action<string> logger = null, int interFrameDelayMs = 20)
            : base(bus, logger)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _interFrameDelayMs = interFrameDelayMs;
            _heartbeat = new System.Threading.Timer(_ => HeartbeatTick(), null, Timeout.Infinite, Timeout.Infinite);
            _status.Transport = transport.Name;
        }

        public override Task StartAsync()
        {
            if (!_transport.IsConnected) _transport.Connect();
            SetStatus(_transport.IsConnected ? ModuleStatus.Running : ModuleStatus.Faulted);
            if (_transport.IsConnected) _heartbeat.Change(2000, 2000);
            return Task.CompletedTask;
        }

        public override Task StopAsync()
        {
            _heartbeat.Change(Timeout.Infinite, Timeout.Infinite);
            if (_transport.IsConnected) SendDeviceControl(DeviceControl.Stop);
            SetStatus(ModuleStatus.Stopped);
            return Task.CompletedTask;
        }

        /// <summary>下发会话配置 + 球参数，并启动发球。</summary>
        public bool Launch(LaunchParameter param)
        {
            if (param == null || param.Balls.Count == 0) { Log("发球参数为空，取消"); return false; }
            if (!_transport.IsConnected)
            {
                Log("发球机未连接，尝试连接");
                _transport.Connect();
                if (!_transport.IsConnected) { Bus?.Publish(EventNames.LauncherStatus, GetStatus()); return false; }
            }
            var cfg = LauncherCommands.BuildSessionConfigForMode(
                param.Mode, 0, param.LoopGroupCount, param.LoopGroupCount, param.LoopGroupIntervalMs / 1000);
            if (!SendFrame(cfg)) return false;
            Sleep(_interFrameDelayMs);
            var ordered = param.Order == ServeOrder.Random ? Shuffle(param.Balls) : param.Balls;
            var encoded = BallParameterEncoder.EncodeAll(ordered);
            var ballFrames = LauncherCommands.BuildBallFrames(encoded);
            foreach (var f in ballFrames) { if (!SendFrame(f)) return false; Sleep(_interFrameDelayMs); }
            var start = LauncherCommands.BuildDeviceControl(DeviceControl.Start);
            if (!SendFrame(start)) return false;
            Log($"已启动发球：{param.Name} {param.Balls.Count}球 模式{param.Mode}");
            Bus?.Publish(EventNames.LauncherLaunched, param.Balls.FirstOrDefault());
            return true;
        }

        public bool Pause() { Log("暂停"); return SendDeviceControl(DeviceControl.Pause); }
        public bool Resume() { Log("继续"); return SendDeviceControl(DeviceControl.Start); }
        public bool Stop() { Log("停止"); return SendDeviceControl(DeviceControl.Stop); }
        public bool FineTune(FineTuneType tune) { Log($"微调 {tune}"); return SendFrame(LauncherCommands.BuildFineTune(tune)); }
        public bool SendDeviceControl(DeviceControl ctrl) => SendFrame(LauncherCommands.BuildDeviceControl(ctrl));

        private bool SendFrame(byte[] frame)
        {
            bool ok = _transport.Send(frame);
            Log($"发送 {ModbusCrc16.ToHex(frame)} -> {(ok ? "OK" : "FAIL")}");
            if (!ok) { SetStatus(ModuleStatus.Degraded); Bus?.Publish(EventNames.LauncherStatus, GetStatus()); }
            return ok;
        }

        public LauncherStatus GetStatus()
        {
            _status.Connected = _transport.IsConnected;
            _status.Transport = _transport.Name;
            _status.LastHeartbeat = DateTime.UtcNow;
            return _status;
        }

        private void HeartbeatTick()
        {
            if (_disposed || !_transport.IsConnected) return;
            try
            {
                _transport.Send(LauncherCommands.BuildReadFaultCode());
                var resp = _transport.Receive(300);
                _status.LastHeartbeat = DateTime.UtcNow;
                if (resp != null && resp.Length >= 5 && resp[1] == 0x03)
                {
                    int code = resp[3] * 256 + resp[4];
                    _status.ErrorCode = (LauncherErrorCode)code;
                    _status.Status = code == 0 ? ModuleStatus.Running : ModuleStatus.Faulted;
                }
                Bus?.Publish(EventNames.LauncherStatus, GetStatus());
            }
            catch (Exception ex) { Log("心跳异常: " + ex.Message); }
        }

        private static List<T> Shuffle<T>(IEnumerable<T> src)
        {
            var rnd = new Random();
            var list = src.ToList();
            int n = list.Count;
            while (n > 1) { int k = rnd.Next(n--); (list[n], list[k]) = (list[k], list[n]); }
            return list;
        }
        private static void Sleep(int ms) { try { Thread.Sleep(ms); } catch { } }

        public override void Dispose()
        {
            _disposed = true;
            _heartbeat?.Dispose();
            _transport?.Dispose();
            base.Dispose();
        }
    }
}
