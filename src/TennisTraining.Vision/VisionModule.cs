using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using TennisTraining.Core;

namespace TennisTraining.Vision
{
    /// <summary>
    /// 视觉模块外观：默认装配 合成源 + HSV 检测器 + 追踪引擎，可独立自检。
    /// 真实部署时替换 ICameraSource/IBallDetector 实现即可。
    /// </summary>
    public sealed class VisionModule : TennisModuleBase
    {
        public CameraService Service { get; }

        public VisionModule(CameraService service, EventBus bus = null, Action<string> logger = null)
            : base(bus, logger)
        {
            Service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public override string Name => "视觉识别与追踪";

        public override Task StartAsync() => Service.StartAsync();
        public override Task StopAsync() => Service.StopAsync();

        public override async Task<SelfTestResult> SelfTestAsync()
        {
            var r = new SelfTestResult { Module = Name };
            var sw = Stopwatch.StartNew();
            await Task.Yield();
            try
            {
                // 离线自检：不依赖运行中的摄像头，直接驱动管线
                var src = new SyntheticCameraSource(320, 180, 60);
                var det = new ManagedColorBallDetector(step: 1);
                var tracker = new BallTrackingEngine();
                int detectedCount = 0;
                Trajectory lastTraj = null;
                var samples = new List<FrameData>();
                for (int i = 0; i < 30; i++)
                {
                    // 推进时间，保证抛物线运动
                    var f = src.GenerateFrame();
                    samples.Add(f);
                    var d = det.Detect(f);
                    if (d != null && d.Detected) detectedCount++;
                    var t = tracker.Process(d);
                    if (t != null) lastTraj = t;
                }

                r.Details.Add($"生成帧数: {samples.Count}");
                r.Details.Add($"检测命中帧: {detectedCount}");
                r.Details.Add($"轨迹点数: {lastTraj?.Points.Count ?? 0}");
                r.Details.Add($"瞬时速度(m/s): {lastTraj?.Speed:0.00}");
                r.Details.Add($"预测落点(m): {lastTraj?.PredictedLanding?.Y:0.00}");
                r.Success = detectedCount >= 20 && lastTraj != null && lastTraj.Points.Count >= 2 && lastTraj.Speed > 0;
                r.Message = r.Success ? "视觉模块自检通过（球检测+轨迹+落点预测）" : "视觉模块自检失败";
            }
            catch (Exception ex)
            {
                r.Success = false;
                r.Message = ex.Message;
            }
            sw.Stop();
            r.ElapsedMs = sw.ElapsedMilliseconds;
            return r;
        }

        public override void Dispose() => Service?.Dispose();
    }
}
