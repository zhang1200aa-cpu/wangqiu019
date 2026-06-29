using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using TennisTraining.Core;
using TennisTraining.Vision;
using TennisTraining.Launcher;
using TennisTraining.Projection;
using TennisTraining.Data;
using TennisTraining.Coordination;

namespace TennisTraining.App
{
    /// <summary>Windows 主窗口：分 Tab 组织各功能模块，每模块可独立自检与运行。</summary>
    public partial class MainForm : Form
    {
        private readonly EventBus _bus;
        private readonly VisionModule _vision;
        private readonly LauncherControlEngine _launcher;
        private readonly ProjectionModule _projection;
        private readonly DataModule _data;
        private readonly TrainingOrchestrator _orch;

        // UI 控件
        private TabControl _tabs;
        private TextBox _log;
        private PictureBox _projBox;
        private System.Windows.Forms.Timer _uiTimer;
        private ToolStripStatusLabel _lblState, _lblVision, _lblLauncher, _lblProj, _lblData, _lblCoord;


        // 发球机控件
        private ComboBox _cbTransport, _cbSpin;
        private NumericUpDown _numSpeed, _numHeight, _numLeftRight, _numFreq;

        // 数据历史
        private ListView _lvSessions;

        public MainForm()
        {
            Text = "HJSE 网球训练馆系统 v1.0";
            ClientSize = new Size(1280, 800);
            StartPosition = FormStartPosition.CenterScreen;

            // 使用 UI 同步上下文构建事件总线，保证事件在 UI 线程触发
            _bus = new EventBus(SynchronizationContext.Current);
            _vision = new VisionModule(
                new CameraService(new SyntheticCameraSource(640, 360, 60),
                    new ManagedColorBallDetector(2), new BallTrackingEngine(), _bus, Log), _bus, Log);
            _launcher = new LauncherControlEngine(new MockLauncherTransport(), _bus, Log);
            _projection = new ProjectionModule(960, 540, _bus, Log);
            _data = new DataModule(null, _bus, Log);
            _orch = new TrainingOrchestrator(_vision, _launcher, _projection, _data, _bus, Log);

            BuildUi();
            _bus.Subscribe<string>(EventNames.Log, m => Log(m));
            _bus.Subscribe<string>(EventNames.Error, m => Log("[错误] " + m));

            _uiTimer = new System.Windows.Forms.Timer { Interval = 150 };
            _uiTimer.Tick += OnUiTick;
            _uiTimer.Start();
            Log("系统就绪。默认发球机为 Mock 模拟，可在“发球机”页切换串口。");
        }

        private void OnUiTick(object s, EventArgs e)
        {
            _lblVision.Text = $"视觉: {_vision.Status} 队列={((_vision.Service as CameraService)?.QueueDepth ?? 0)} 丢帧={((_vision.Service as CameraService)?.DroppedFrames ?? 0)}";
            _lblLauncher.Text = $"发球机: {_launcher.GetStatus()}";
            _lblProj.Text = $"投影: {_projection.Status}";
            _lblData.Text = $"数据: {_data.Status}";
            _lblCoord.Text = $"协调: {_orch.Status}";
            _lblState.Text = $"训练状态: {_orch.Fsm.State}";
            // 投影预览刷新
            if (_projBox.Image != _projection.View.Bitmap)
                _projBox.Image = _projection.View.Bitmap;
        }

        internal void Log(string msg)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke(() => Log(msg)); } catch { } return; }
            if (_log == null) return; // BuildUi 前收到的日志丢弃
            _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
        }
    }
}
