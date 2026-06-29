using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using TennisTraining.Core;
using TennisTraining.Launcher;
using TennisTraining.Coordination;

namespace TennisTraining.App
{
    public partial class MainForm
    {
        private async Task RunSelfTest(ITennisModule module)
        {
            Log($"运行自检：{module.Name} ...");
            Enabled = false;
            try
            {
                var r = await module.SelfTestAsync();
                Log(r.ToString());
                foreach (var d in r.Details) Log("  " + d);
            }
            catch (Exception ex) { Log("自检异常: " + ex.Message); }
            finally { Enabled = true; }
        }

        private async void RunAllSelfTests()
        {
            Log("===== 全部模块自检 =====");
            await RunSelfTest(_launcher);
            await RunSelfTest(_vision);
            await RunSelfTest(_data);
            await RunSelfTest(_projection);
            await RunSelfTest(_orch);
            Log("===== 全部自检完成 =====");
        }

        private void ConnectLauncher()
        {
            var sel = _cbTransport.SelectedItem?.ToString() ?? "Mock 模拟";
            if (sel.StartsWith("串口"))
            {
                var port = sel.Replace("串口 ", "").Trim();
                var t = new SerialLauncherTransport(port);
                if (t.Connect()) Log($"已连接串口 {port}");
                else { Log($"串口 {port} 连接失败"); return; }
                // 用新传输重建引擎
                _launcher.Dispose();
                var eng = new LauncherControlEngine(t, _bus, Log);
                RebindLauncher(eng);
            }
            else
            {
                Log("已使用 Mock 传输（无需真实硬件）");
            }
        }

        private void LaunchOne()
        {
            var ball = new BallParameter
            {
                SpinType = (SpinType)(_cbSpin.SelectedIndex + 1),
                Speed = (int)_numSpeed.Value,
                Height = (int)_numHeight.Value,
                LeftRight = (int)_numLeftRight.Value,
                Rate = (int)_numFreq.Value,
                Spin = 1
            };
            var param = new LaunchParameter
            {
                Name = "单球测试",
                Position = DevicePosition.BaselineCenter,
                Mode = ServeMode.Group,
                Order = ServeOrder.Sequential,
                LoopGroupCount = 1,
                LoopGroupIntervalMs = 1000,
                Balls = { ball }
            };
            bool ok = _launcher.Launch(param);
            Log($"发球指令 -> {(ok ? "已发送" : "失败")}");
        }

        private void StartTraining()
        {
            var ball = new BallParameter
            {
                SpinType = (SpinType)(_cbSpin.SelectedIndex + 1),
                Speed = (int)_numSpeed.Value,
                Height = (int)_numHeight.Value,
                LeftRight = (int)_numLeftRight.Value,
                Rate = (int)_numFreq.Value,
                Spin = 1
            };
            var param = new LaunchParameter
            {
                Name = "训练",
                Position = DevicePosition.BaselineCenter,
                Mode = ServeMode.Group,
                Order = ServeOrder.Sequential,
                LoopGroupCount = 5,
                LoopGroupIntervalMs = 1000,
                Balls = { ball }
            };
            try { _orch.StartTraining(param); }
            catch (Exception ex) { Log("启动训练异常: " + ex.Message); }
        }

        private void StopTraining()
        {
            try { _orch.StopTraining(); }
            catch (Exception ex) { Log("停止训练异常: " + ex.Message); }
        }

        private void RefreshSessions()
        {
            _lvSessions.Items.Clear();
            foreach (var s in _data.History(50))
            {
                var item = new ListViewItem(s.Id.ToString());
                item.SubItems.Add(s.UserName);
                item.SubItems.Add(s.Mode.ToString());
                item.SubItems.Add(s.Position.ToString());
                item.SubItems.Add(s.Stats.TotalBalls.ToString());
                item.SubItems.Add(s.Stats.HitRate.ToString("0.0"));
                item.SubItems.Add(s.Stats.AvgSpeed.ToString("0.0"));
                item.SubItems.Add(s.StartTime.ToLocalTime().ToString("MM-dd HH:mm"));
                _lvSessions.Items.Add(item);
            }
            Log($"已刷新历史（{_lvSessions.Items.Count} 条）");
        }

        // 切换传输后重建编排器对发球机的引用
        private void RebindLauncher(LauncherControlEngine eng)
        {
            var orch = new TrainingOrchestrator(_vision, eng, _projection, _data, _bus, Log);
            orch.StartAsync().Wait();
            // 用反射替换字段（保持简单：直接重建并发布提示）
            GetType().GetField("_launcher", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(this, eng);
            GetType().GetField("_orch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(this, orch);
            Log("发球机传输已切换并重建编排器");
        }
    }
}
