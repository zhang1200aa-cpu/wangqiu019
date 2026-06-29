using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using TennisTraining.Core;
using TennisTraining.Vision;
using TennisTraining.Launcher;
using TennisTraining.Projection;
using TennisTraining.Data;

namespace TennisTraining.Coordination
{
    /// <summary>编排器自检：端到端闭环验证。</summary>
    public sealed partial class TrainingOrchestrator
    {
        public override async Task<SelfTestResult> SelfTestAsync()
        {
            var r = new SelfTestResult { Module = Name };
            var sw = Stopwatch.StartNew();
            await Task.Yield();
            VisionModule vision = null;
            LauncherControlEngine launcher = null;
            DataModule data = null;
            ProjectionModule proj = null;
            TrainingOrchestrator orch = null;
            try
            {
                var bus = Bus ?? new EventBus();
                var cam = new CameraService(new SyntheticCameraSource(320, 180, 60),
                    new ManagedColorBallDetector(1), new BallTrackingEngine(), bus, Logger);
                vision = new VisionModule(cam, bus, Logger);
                launcher = new LauncherControlEngine(new MockLauncherTransport(), bus, Logger);
                data = new DataModule(Path.Combine(Path.GetTempPath(),
                    "tt_coord_" + Guid.NewGuid().ToString("N") + ".db"), bus, Logger);
                proj = new ProjectionModule(640, 360, bus, Logger);
                orch = new TrainingOrchestrator(vision, launcher, proj, data, bus, Logger);
                orch.StartAsync().Wait();

                var param = new LaunchParameter
                {
                    Name = "协调自检",
                    Mode = ServeMode.Group,
                    Order = ServeOrder.Sequential,
                    LoopGroupCount = 1,
                    LoopGroupIntervalMs = 500,
                    Balls = { new BallParameter { SpinType = SpinType.Topspin, Speed = 40, Height = 50, Rate = 5 } }
                };
                orch.StartTraining(param);
                // 让合成视觉跑 ~2.6s（一个抛物线周期 2s）以产生至少一段完整轨迹
                await Task.Delay(2600);
                orch.StopTraining();

                int recorded = data.Repository.CountSessions();
                r.Details.Add($"FSM 终态: {orch.Fsm.State}");
                r.Details.Add($"会话数: {recorded}");
                r.Success = orch.Fsm.State == TrainingState.Idle && recorded >= 1;
                r.Message = r.Success ? "协调模块自检通过（端到端闭环）" : "协调模块自检失败";
            }
            catch (Exception ex) { r.Success = false; r.Message = ex.Message; }
            finally
            {
                orch?.Dispose(); data?.Dispose(); proj?.Dispose(); vision?.Dispose(); launcher?.Dispose();
            }
            sw.Stop();
            r.ElapsedMs = sw.ElapsedMilliseconds;
            return r;
        }
    }
}
