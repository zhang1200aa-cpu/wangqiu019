using System;
using TennisTraining.Core;

namespace TennisTraining.Vision
{
    /// <summary>
    /// 基于 HSV 颜色阈值的球检测器（纯托管，无 OpenCV 依赖）。
    /// 检测荧光黄绿色网球：H∈[25,70]°，S≥0.35，V≥0.4。
    /// 通过降采样扫描像素，统计质心与半径，给出置信度。
    /// 可被 OpenCvSharp/YOLO 实现替换（实现 IBallDetector 即可）。
    /// </summary>
    public class ManagedColorBallDetector : IBallDetector
    {
        private readonly int _step; // 降采样步长
        public double MinArea { get; set; } = 8;

        public ManagedColorBallDetector(int step = 2)
        {
            _step = Math.Max(1, step);
        }

        public BallDetectionResult Detect(FrameData frame)
        {
            var r = new BallDetectionResult { FrameSequence = frame.Sequence, TimestampMs = frame.TimestampMs };
            if (frame?.Data == null || frame.Width <= 0 || frame.Height <= 0) return r;

            double sx = 0, sy = 0, count = 0;
            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;

            var data = frame.Data;
            int w = frame.Width, h = frame.Height;
            for (int y = 0; y < h; y += _step)
            {
                int row = y * w * 3;
                for (int x = 0; x < w; x += _step)
                {
                    int idx = row + x * 3;
                    byte b = data[idx], g = data[idx + 1], rd = data[idx + 2];
                    if (IsTennisBall(b, g, rd))
                    {
                        sx += x; sy += y; count++;
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
                }
            }

            if (count < MinArea)
            {
                r.Detected = false;
                r.Confidence = 0;
                return r;
            }

            double cx = sx / count, cy = sy / count;
            double rx = (maxX - minX) * 0.5, ry = (maxY - minY) * 0.5;
            double radius = Math.Max(rx, ry);
            // 置信度：面积与圆形度综合（简化）
            double area = count * _step * _step;
            double conf = Math.Min(1.0, area / (Math.PI * radius * radius + 1));
            r.Detected = true;
            r.Center = new Point2D(cx, cy);
            r.Radius = radius;
            r.Confidence = conf;
            return r;
        }

        /// <summary>BGR → 是否网球色（HSV 黄绿）。</summary>
        public static bool IsTennisBall(byte b, byte g, byte r)
        {
            // 归一化
            double bn = b / 255.0, gn = g / 255.0, rn = r / 255.0;
            double mx = Math.Max(rn, Math.Max(gn, bn));
            double mn = Math.Min(rn, Math.Min(gn, bn));
            double delta = mx - mn;
            double v = mx;
            if (v < 0.4) return false;
            double s = mx == 0 ? 0 : delta / mx;
            if (s < 0.35) return false;
            double h;
            if (delta == 0) h = 0;
            else if (mx == rn) h = 60 * (((gn - bn) / delta) % 6);
            else if (mx == gn) h = 60 * (((bn - rn) / delta) + 2);
            else h = 60 * (((rn - gn) / delta) + 4);
            if (h < 0) h += 360;
            return h >= 20 && h <= 80;
        }
    }
}
