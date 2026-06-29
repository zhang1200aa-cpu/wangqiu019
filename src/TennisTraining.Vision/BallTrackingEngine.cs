using System;
using System.Collections.Generic;
using TennisTraining.Core;

namespace TennisTraining.Vision
{
    /// <summary>
    /// 球追踪引擎：跨帧维护轨迹、速度、旋转估算、落地点预测。
    /// 当某帧未检测到球时，依靠短期预测继续工作（容错降级）。
    /// </summary>
    public class BallTrackingEngine
    {
        private readonly TrajectoryCalculator _calc;
        private readonly List<Point3D> _points = new();
        private readonly List<BallDetectionResult> _raw = new();
        private readonly int _maxPoints;
        private readonly double _maxGapMs;
        private long _lastTs = -1;

        public TrajectoryCalculator Calculator => _calc;
        public IReadOnlyList<Point3D> Points => _points;

        public BallTrackingEngine(TrajectoryCalculator calc = null, int maxPoints = 64, double maxGapMs = 500)
        {
            _calc = calc ?? new TrajectoryCalculator();
            _maxPoints = maxPoints;
            _maxGapMs = maxGapMs;
        }

        /// <summary>处理一帧检测结果，返回更新后的轨迹（可能为 null）。</summary>
        public Trajectory Process(BallDetectionResult det)
        {
            if (det == null) return null;

            // 丢球过久 → 重置轨迹
            if (_lastTs >= 0 && det.TimestampMs - _lastTs > _maxGapMs)
            {
                _points.Clear(); _raw.Clear();
            }
            _lastTs = det.TimestampMs;

            if (det.Detected)
            {
                _raw.Add(det);
                var p = _calc.PixelToWorld(det.Center, det.Radius, det.TimestampMs);
                _points.Add(p);
                while (_points.Count > _maxPoints) _points.RemoveAt(0);
            }
            // 未检测到时，不新增点，但保留已有轨迹供预测

            if (_points.Count == 0) return null;

            var vel = _calc.ComputeVelocity(_points);
            var last = _points[_points.Count - 1];
            var landing = _calc.PredictLanding(last, vel);

            return new Trajectory
            {
                Points = new List<Point3D>(_points),
                Velocity = vel,
                Speed = _calc.SpeedMps(vel),
                PredictedLanding = landing,
                Spin = EstimateSpin(),
                StartMs = _points[0].TimestampMs,
                EndMs = last.TimestampMs
            };
        }

        /// <summary>旋转估算（简化：依据轨迹曲率粗判上/下旋）。</summary>
        private SpinInfo EstimateSpin()
        {
            if (_points.Count < 4) return new SpinInfo { Type = SpinType.Flat };
            // 用中段垂直加速度符号近似：Z 持续上升→上旋趋势；下降快→下旋
            int n = _points.Count;
            var mid = _points[n / 2];
            var last = _points[n - 1];
            double dz = last.Z - mid.Z;
            var spin = new SpinInfo { Confidence = 0.4 };
            if (dz > 0.1) spin.Type = SpinType.Topspin;
            else if (dz < -0.2) spin.Type = SpinType.Slice;
            else spin.Type = SpinType.Flat;
            spin.Rpm = Math.Abs(dz) * 600;
            return spin;
        }

        public void Reset()
        {
            _points.Clear(); _raw.Clear(); _lastTs = -1;
        }
    }
}
