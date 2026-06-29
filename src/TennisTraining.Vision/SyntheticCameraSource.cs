using System;
using System.Collections.Generic;
using System.Threading;
using TennisTraining.Core;

namespace TennisTraining.Vision
{
    /// <summary>
    /// 合成摄像头源：无真实硬件时生成带“运动网球”的合成帧，驱动整个视觉管线做独立自检。
    /// 球在像素空间沿抛物线运动，颜色为荧光黄绿（HSV 黄绿区间）。
    /// </summary>
    public sealed class SyntheticCameraSource : ICameraSource
    {
        private readonly int _width, _height, _fps;
        private System.Threading.Timer _timer;
        private volatile bool _running;
        private long _virtualMs;
        private int _seq;

        // 网球荧光黄（BGR），HSV 色相约 64°，落在检测区间内
        private const byte BallB = 0;
        private const byte BallG = 240;
        private const byte BallR = 220;
        private const int BallRadius = 14;

        public string Name => "Synthetic";
        public bool IsRunning { get; set; }
        public int FrameRate => _fps;
        public event Action<FrameData> FrameReady;

        public SyntheticCameraSource(int width = 640, int height = 360, int fps = 30)
        {
            _width = width; _height = height; _fps = fps;
        }

        public IEnumerable<string> EnumerateDevices() => new[] { "Synthetic-0" };

        public void Start()
        {
            if (_running) return;
            _running = IsRunning = true;
            _virtualMs = 0;
            _seq = 0;
            int interval = 1000 / Math.Max(1, _fps);
            _timer = new System.Threading.Timer(_ => Emit(), null, 0, interval);
        }

        public void Stop()
        {
            _running = IsRunning = false;
            _timer?.Dispose(); _timer = null;
        }

        private void Emit()
        {
            if (!_running) return;
            var frame = GenerateFrame();
            try { FrameReady?.Invoke(frame); } catch { }
        }

        /// <summary>生成一帧（也可单独调用做自检）。每次调用推进一个帧时长，保证时间戳递增。</summary>
        public FrameData GenerateFrame()
        {
            _virtualMs += 1000 / Math.Max(1, _fps);
            int t = (int)_virtualMs;
            double phase = (t % 2000) / 2000.0; // 2 秒一个抛物线周期
            double cx = phase * _width;
            // 抛物线：从左下到右下，中间最高
            double h = _height * 0.55;
            double cy = _height * 0.85 - 4 * h * phase * (1 - phase);
            int ix = (int)cx, iy = (int)cy;
            var frame = new FrameData
            {
                Width = _width, Height = _height,
                Data = new byte[_width * _height * 3],
                TimestampMs = t,
                Sequence = _seq++
            };
            // 暗背景
            for (int i = 0; i < frame.Data.Length; i += 3)
            {
                frame.Data[i] = 16;     // B
                frame.Data[i + 1] = 24; // G
                frame.Data[i + 2] = 32; // R
            }
            FillCircle(frame.Data, _width, _height, ix, iy, BallRadius, BallB, BallG, BallR);
            return frame;
        }

        internal static void FillCircle(byte[] data, int w, int h, int cx, int cy, int r, byte b, byte g, byte rd)
        {
            int x0 = Math.Max(0, cx - r), x1 = Math.Min(w - 1, cx + r);
            int y0 = Math.Max(0, cy - r), y1 = Math.Min(h - 1, cy + r);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= r * r)
                    {
                        int idx = (y * w + x) * 3;
                        data[idx] = b; data[idx + 1] = g; data[idx + 2] = rd;
                    }
                }
            }
        }

        public void Dispose() => Stop();
    }
}
