using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TennisTraining.Core;

namespace TennisTraining.Launcher
{
    /// <summary>发球机引擎的自检实现（partial）。</summary>
    public sealed partial class LauncherControlEngine
    {
        public override async Task<SelfTestResult> SelfTestAsync()
        {
            var r = new SelfTestResult { Module = Name };
            var sw = Stopwatch.StartNew();
            await Task.Yield();
            try
            {
                // 用 Mock 传输做协议自检，不影响真实硬件
                using var mock = new MockLauncherTransport();
                mock.Connect();
                var eng = new LauncherControlEngine(mock, logger: Logger);
                var param = new LaunchParameter
                {
                    Name = "自检",
                    Mode = ServeMode.Group,
                    Order = ServeOrder.Sequential,
                    LoopGroupCount = 1,
                    LoopGroupIntervalMs = 1000,
                    Balls = new()
                    {
                        new BallParameter { SpinType = SpinType.Topspin, Speed=40, Height=50, LeftRight=0,  Rate=5, Spin=1 },
                        new BallParameter { SpinType = SpinType.Flat,    Speed=55, Height=45, LeftRight=10, Rate=5, Spin=1 },
                        new BallParameter { SpinType = SpinType.Slice,   Speed=40, Height=60, LeftRight=-10,Rate=6, Spin=3 }
                    }
                };
                bool ok = eng.Launch(param);
                r.Details.Add($"发球启动: {ok}");
                r.Details.Add($"发送帧数: {mock.SentFrames.Count}");
                bool framesOk = mock.SentFrames.Count >= 5; // 配置1 + 球3 + 启动1
                bool crcOk = mock.SentFrames.All(ModbusCrc16.Validate);
                r.Details.Add($"CRC 全部有效: {crcOk}");
                var startFrame = mock.SentFrames.Last();
                bool startOk = startFrame[1] == 0x06 && startFrame[2] == 0x00 && startFrame[3] == 0x03
                               && startFrame[4] == 0x00 && startFrame[5] == 0x01;
                r.Details.Add($"启动帧正确: {startOk}");
                r.Success = ok && framesOk && crcOk && startOk;
                r.Message = r.Success ? "发球机模块自检通过" : "发球机模块自检失败";
                eng.Dispose();
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
    }
}
