using System.Drawing;
using System.Windows.Forms;
using TennisTraining.Launcher;

namespace TennisTraining.App
{
    public partial class MainForm
    {
        private TabPage BuildLauncherTab()
        {
            var tp = new TabPage("发球机控制");
            var panel = new Panel { Dock = DockStyle.Top, Height = 130 };
            int x = 10, y = 10;
            var lblT = new Label { Text = "传输:", Left = x, Top = y + 3, Width = 40 };
            _cbTransport = new ComboBox { Left = x + 45, Top = y, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            _cbTransport.Items.AddRange(new object[] { "Mock 模拟", "串口 COM1", "串口 COM3", "串口 COM5" });
            _cbTransport.SelectedIndex = 0;
            var bConn = new Button { Text = "连接", Left = x + 215, Top = y, Width = 70 };
            bConn.Click += (s, e) => ConnectLauncher();
            panel.Controls.AddRange(new Control[] { lblT, _cbTransport, bConn });

            y = 40;
            var lSpin = new Label { Text = "球性:", Left = x, Top = y + 3, Width = 40 };
            _cbSpin = new ComboBox { Left = x + 45, Top = y, Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
            _cbSpin.Items.AddRange(new object[] { "上旋", "平击", "下旋" });
            _cbSpin.SelectedIndex = 0;
            LabeledNumeric(panel, "球速", x + 145, y, out _numSpeed, 40, 20, 80, 1);
            LabeledNumeric(panel, "弧度", x + 145 + 130, y, out _numHeight, 50, 10, 90, 1);
            LabeledNumeric(panel, "左右", x + 145 + 260, y, out _numLeftRight, 0, -15, 15, 1);
            LabeledNumeric(panel, "球频", x + 145 + 390, y, out _numFreq, 5, 1, 20, 1);
            panel.Controls.AddRange(new Control[] { lSpin, _cbSpin });

            var bLaunch = new Button { Text = "发送并启动", Left = 10, Top = 90, Width = 120 };
            bLaunch.Click += (s, e) => LaunchOne();
            var bStop = new Button { Text = "停止", Left = 140, Top = 90, Width = 80 };
            bStop.Click += (s, e) => _launcher.Stop();
            var bTune = new Button { Text = "弧度+(微调)", Left = 230, Top = 90, Width = 100 };
            bTune.Click += (s, e) => _launcher.FineTune(FineTuneType.HeightPlus);
            panel.Controls.AddRange(new Control[] { bLaunch, bStop, bTune });
            tp.Controls.Add(panel);
            var note = new Label { Top = 140, Left = 10, Width = 900, Height = 60,
                Text = "协议：Modbus RTU（BLE透传）。球数据 0x10@0x0021；控制 0x06@0x0003；微调 0x06@0x001D。\nMock 模式不会真正发球，仅记录帧并自检协议。" };
            tp.Controls.Add(note);
            return tp;
        }

        private void LabeledNumeric(Control parent, string name, int x, int y, out NumericUpDown num, int value, int min, int max, int step)
        {
            var l = new Label { Text = name + ":", Left = x, Top = y + 3, Width = 45 };
            num = new NumericUpDown { Left = x + 50, Top = y, Width = 55, Minimum = min, Maximum = max, Value = value, Increment = step };
            parent.Controls.Add(l); parent.Controls.Add(num);
        }

        private TabPage BuildProjectionTab()
        {
            var tp = new TabPage("投影显示");
            var panel = new Panel { Dock = DockStyle.Top, Height = 44 };
            var bTest = new Button { Text = "投影自检", Left = 10, Top = 10, Width = 100 };
            bTest.Click += async (s, e) => await RunSelfTest(_projection);
            var bPng = new Button { Text = "保存PNG", Left = 120, Top = 10, Width = 90 };
            bPng.Click += (s, e) =>
            {
                using var d = new SaveFileDialog { Filter = "PNG|*.png" };
                if (d.ShowDialog() == DialogResult.OK) { _projection.SaveSnapshot(d.FileName); Log("已保存: " + d.FileName); }
            };
            panel.Controls.AddRange(new Control[] { bTest, bPng });
            _projBox = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
            tp.Controls.Add(_projBox);
            tp.Controls.Add(panel);
            return tp;
        }
    }
}
