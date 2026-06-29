using System.Drawing;
using System.Windows.Forms;

namespace TennisTraining.App
{
    public partial class MainForm
    {
        private void BuildUi()
        {
            var menu = new MenuStrip();
            var miRun = new ToolStripMenuItem("运行(&R)");
            miRun.DropDownItems.Add("全部自检(&A)", null, (s, e) => RunAllSelfTests());
            miRun.DropDownItems.Add("启动训练(&S)", null, (s, e) => StartTraining());
            miRun.DropDownItems.Add("停止训练(&T)", null, (s, e) => StopTraining());
            miRun.DropDownItems.Add("退出(&X)", null, (s, e) => Close());
            menu.Items.Add(miRun);
            MainMenuStrip = menu;
            Controls.Add(menu);

            _tabs = new TabControl { Dock = DockStyle.Fill };
            _tabs.TabPages.Add(BuildOverviewTab());
            _tabs.TabPages.Add(BuildVisionTab());
            _tabs.TabPages.Add(BuildLauncherTab());
            _tabs.TabPages.Add(BuildProjectionTab());
            _tabs.TabPages.Add(BuildDataTab());
            _tabs.TabPages.Add(BuildCoordinationTab());
            Controls.Add(_tabs);
            _tabs.BringToFront();

            var status = new StatusStrip();
            _lblVision = new ToolStripStatusLabel("视觉: -") { Spring = true };
            _lblLauncher = new ToolStripStatusLabel("发球机: -") { Spring = true };
            _lblProj = new ToolStripStatusLabel("投影: -") { Spring = true };
            _lblData = new ToolStripStatusLabel("数据: -") { Spring = true };
            _lblCoord = new ToolStripStatusLabel("协调: -") { Spring = true };
            _lblState = new ToolStripStatusLabel("训练状态: Idle");
            status.Items.AddRange(new ToolStripItem[] { _lblVision, _lblLauncher, _lblProj, _lblData, _lblCoord, new ToolStripSeparator(), _lblState });
            Controls.Add(status);
        }

        private TabPage BuildOverviewTab()
        {
            var tp = new TabPage("总览");
            var panel = new Panel { Dock = DockStyle.Top, Height = 44 };
            var bAll = new Button { Text = "全部自检", Left = 10, Top = 10, Width = 100 };
            bAll.Click += (s, e) => RunAllSelfTests();
            var bStart = new Button { Text = "启动训练", Left = 120, Top = 10, Width = 100 };
            bStart.Click += (s, e) => StartTraining();
            var bStop = new Button { Text = "停止训练", Left = 230, Top = 10, Width = 100 };
            bStop.Click += (s, e) => StopTraining();
            panel.Controls.AddRange(new Control[] { bAll, bStart, bStop });
            _log = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9),
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.LightGreen
            };
            tp.Controls.Add(_log);
            tp.Controls.Add(panel);
            return tp;
        }

        private TabPage BuildVisionTab()
        {
            var tp = new TabPage("视觉识别与追踪");
            var panel = new Panel { Dock = DockStyle.Top, Height = 44 };
            var bTest = new Button { Text = "视觉自检", Left = 10, Top = 10, Width = 100 };
            bTest.Click += async (s, e) => await RunSelfTest(_vision);
            var bStart = new Button { Text = "启动", Left = 120, Top = 10, Width = 80 };
            bStart.Click += async (s, e) => await _vision.StartAsync();
            var bStop = new Button { Text = "停止", Left = 210, Top = 10, Width = 80 };
            bStop.Click += async (s, e) => await _vision.StopAsync();
            panel.Controls.AddRange(new Control[] { bTest, bStart, bStop });
            var lbl = new Label { Dock = DockStyle.Fill, Text = "合成摄像头源 + HSV 球检测 + 轨迹追踪。\n状态见底部状态栏。", Top = 50 };
            tp.Controls.Add(lbl);
            tp.Controls.Add(panel);
            return tp;
        }
    }
}
