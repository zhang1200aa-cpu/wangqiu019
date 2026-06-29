using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TennisTraining.Core
{
    /// <summary>自检结果。</summary>
    public class SelfTestResult
    {
        public string Module { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<string> Details { get; set; } = new();
        public long ElapsedMs { get; set; }

        public override string ToString()
            => $"[{Module}] {(Success ? "通过" : "失败")} {Message} ({ElapsedMs}ms)";
    }

    /// <summary>所有功能模块的统一契约：名称、启动、停止、状态、自检。</summary>
    public interface ITennisModule : IDisposable
    {
        string Name { get; }
        ModuleStatus Status { get; }
        Task StartAsync();
        Task StopAsync();
        /// <summary>独立自检：不依赖其它模块即可验证本模块核心逻辑是否可用。</summary>
        Task<SelfTestResult> SelfTestAsync();
    }

    /// <summary>抽象基类：提供状态管理与日志输出样板。</summary>
    public abstract class TennisModuleBase : ITennisModule
    {
        public abstract string Name { get; }
        public ModuleStatus Status { get; protected set; } = ModuleStatus.Uninitialized;
        protected EventBus Bus { get; set; }
        protected Action<string> Logger { get; }

        protected TennisModuleBase(EventBus bus = null, Action<string> logger = null)
        {
            Bus = bus;
            Logger = logger ?? (s => System.Diagnostics.Debug.WriteLine(s));
        }

        protected void Log(string msg)
        {
            Logger($"[{Name}] {msg}");
            Bus?.Publish(EventNames.Log, $"[{Name}] {msg}");
        }

        protected void SetStatus(ModuleStatus s)
        {
            if (Status == s) return;
            Status = s;
            Log($"状态 -> {s}");
        }

        public virtual Task StartAsync() { SetStatus(ModuleStatus.Running); return Task.CompletedTask; }
        public virtual Task StopAsync() { SetStatus(ModuleStatus.Stopped); return Task.CompletedTask; }
        public abstract Task<SelfTestResult> SelfTestAsync();

        public virtual void Dispose() { }
    }

    // ---------- 各模块对外接口 ----------

    /// <summary>摄像头源：实时获取视频帧。</summary>
    public interface ICameraSource : IDisposable
    {
        string Name { get; }
        bool IsRunning { get; set; }
        int FrameRate { get; }          // 标称帧率
        event Action<FrameData> FrameReady;
        void Start();
        void Stop();
        IEnumerable<string> EnumerateDevices();
    }

    /// <summary>球检测器：从一帧中检测球的位置。</summary>
    public interface IBallDetector
    {
        BallDetectionResult Detect(FrameData frame);
    }

    /// <summary>发球机传输层：把字节帧发送到硬件（串口/BLE/TCP/Mock）。</summary>
    public interface ILauncherTransport : IDisposable
    {
        string Name { get; }
        bool IsConnected { get; }
        bool Connect();
        void Disconnect();
        /// <summary>发送一帧，返回是否成功。</summary>
        bool Send(byte[] frame);
        /// <summary>读取应答/上行数据（用于查询状态、故障码）。</summary>
        byte[] Receive(int timeoutMs = 200);
    }

    /// <summary>投影视图：把渲染内容输出到某画布/控件/文件。</summary>
    public interface IProjectorView : IDisposable
    {
        int Width { get; }
        int Height { get; }
        void BeginDraw();
        void EndDraw();
        /// <summary>清空画布并绘制底色。</summary>
        void Clear(int argb = unchecked((int)0xFF101820));
    }
}
