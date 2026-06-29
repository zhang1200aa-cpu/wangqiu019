using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TennisTraining.Core;
using Task = System.Threading.Tasks.Task;

namespace TennisTraining.Vision
{
    /// <summary>
    /// 摄像头服务：协调“取帧→检测→追踪→数据回传”的视觉管线。
    /// 取帧在摄像头线程，检测/追踪在独立消费线程，避免阻塞 UI 与采集。
    /// </summary>
    public sealed class CameraService : TennisModuleBase
    {
        private readonly ICameraSource _source;
        private readonly IBallDetector _detector;
        private readonly BallTrackingEngine _tracker;
        private readonly EventBus _bus;
        private readonly ConcurrentQueue<FrameData> _queue = new();
        private readonly CancellationTokenSource _cts = new();
        private Task _loop;
        private int _dropped;
        private const int MaxQueue = 8;

        public ICameraSource Source => _source;
        public BallTrackingEngine Tracker => _tracker;
        public int QueueDepth => _queue.Count;
        public int DroppedFrames => _dropped;

        public CameraService(ICameraSource source, IBallDetector detector, BallTrackingEngine tracker,
            EventBus bus = null, Action<string> logger = null)
            : base(bus, logger)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _detector = detector ?? throw new ArgumentNullException(nameof(detector));
            _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
            _bus = bus;
        }

        public override string Name => "视觉识别";

        public override Task<SelfTestResult> SelfTestAsync()
            => new VisionModule(this, Bus, Logger).SelfTestAsync();

        public override Task StartAsync()
        {
            _source.FrameReady += OnFrame;
            _source.Start();
            _loop = Task.Run(() => ProcessLoop(_cts.Token));
            SetStatus(ModuleStatus.Running);
            return Task.CompletedTask;
        }

        public override async Task StopAsync()
        {
            _source.FrameReady -= OnFrame;
            _source.Stop();
            _cts.Cancel();
            try { await _loop; } catch { }
            SetStatus(ModuleStatus.Stopped);
        }

        private void OnFrame(FrameData frame)
        {
            _bus?.Publish(EventNames.FrameReceived, frame);
            if (_queue.Count >= MaxQueue)
            {
                if (_queue.TryDequeue(out _)) Interlocked.Increment(ref _dropped);
            }
            _queue.Enqueue(frame);
        }

        private void ProcessLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_queue.TryDequeue(out var frame))
                {
                    try
                    {
                        var det = _detector.Detect(frame);
                        if (det != null && det.Detected)
                            _bus?.Publish(EventNames.BallDetected, det);
                        var traj = _tracker.Process(det);
                        if (traj != null)
                            _bus?.Publish(EventNames.TrajectoryUpdated, traj);
                    }
                    catch (Exception ex) { Log("处理异常: " + ex.Message); }
                }
                else
                {
                    try { Task.Delay(2, token).Wait(); } catch { }
                }
            }
        }

        public void ResetTracking() => _tracker.Reset();

        public override void Dispose()
        {
            try { _cts.Cancel(); } catch { }
            _source?.Dispose();
            _cts?.Dispose();
            base.Dispose();
        }
    }
}
