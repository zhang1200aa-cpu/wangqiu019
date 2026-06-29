using System.Drawing;
using System.Windows.Forms;

namespace TennisTraining.App
{
    public partial class MainForm
    {
        private TabPage BuildDataTab()
        {
            var tp = new TabPage("数据记录与分析");
            var panel = new Panel { Dock = DockStyle.Top, Height = 44 };
            var bTest = new Button { Text = "数据自检", Left = 10, Top = 10, Width = 100 };
            bTest.Click += async (s, e) => await RunSelfTest(_data);
            var bRefresh = new Button { Text = "刷新历史", Left = 120, Top = 10, Width = 90 };
            bRefresh.Click += (s, e) => RefreshSessions();
            panel.Controls.AddRange(new Control[] { bTest, bRefresh });
            _lvSessions = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
            AddSessionColumns(_lvSessions);
            tp.Controls.Add(_lvSessions);
            tp.Controls.Add(panel);
            return tp;
        }

        private static void AddSessionColumns(ListView lv)
        {
            lv.Columns.Add("ID", 50); lv.Columns.Add("用户", 80); lv.Columns.Add("模式", 70);
            lv.Columns.Add("位置", 70); lv.Columns.Add("球数", 50); lv.Columns.Add("命中率%", 70);
            lv.Columns.Add("均速m/s", 70); lv.Columns.Add("开始时间", 150);
        }

        private TabPage BuildCoordinationTab()
        {
            var tp = new TabPage("系统协调");
            var panel = new Panel { Dock = DockStyle.Top, Height = 44 };
            var bTest = new Button { Text = "协调自检(端到端)", Left = 10, Top = 10, Width = 140 };
            bTest.Click += async (s, e) => await RunSelfTest(_orch);
            var bStart = new Button { Text = "开始训练", Left = 160, Top = 10, Width = 100 };
            bStart.Click += (s, e) => StartTraining();
            var bStop = new Button { Text = "停止训练", Left = 270, Top = 10, Width = 100 };
            bStop.Click += (s, e) => StopTraining();
            panel.Controls.AddRange(new Control[] { bTest, bStart, bStop });
            var lbl = new Label { Top = 50, Left = 10, Width = 900, Height = 60,
                Text = "协调自检会运行约 2.6 秒端到端闭环（合成视觉+Mock发球机+内存数据+投影）。\n训练状态见底部状态栏。" };
            tp.Controls.Add(lbl);
            tp.Controls.Add(panel);
            return tp;
        }
    }
}
