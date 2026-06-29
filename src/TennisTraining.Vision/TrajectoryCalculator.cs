using System;
using System.Collections.Generic;
using TennisTraining.Core;

namespace TennisTraining.Vision
{
    /// <summary>
    /// 轨迹计算器：2D 像素 → 3D 世界（地面）坐标转换、速度计算、落地点预测。
    /// 采用单摄像头 + 地面平面假设的简化模型。
    /// </summary>
    public class TrajectoryCalculator
    {
        private const double G = 9.81; // 重力加速度 m/s²
        public CameraCalibration Calibration { get; set; } = new();

        /// <summary>像素坐标 → 地面 3D 坐标（米）。Z 为离地高度，简化为按检测半径估算。</summary>
        public Point3D PixelToWorld(Point2D pixel, double radiusPx, long ts)
        {
            double H = Calibration.Height;
            double pitch = Calibration.PitchDeg * Math.PI / 180.0;
            double v = pixel.Y - Calibration.Cy;
            // 像素 v 对应的光线相对光轴角度
            double rayAngle = Math.Atan2(v, Calibration.Fy);
            // 相对水平面的俯角：光轴俯仰 - rayAngle（y 向下为正）
            double downAngle = pitch - rayAngle;
            if (downAngle <= 1e-3) downAngle = 1e-3; // 避免水平/向上时无地面交点
            double forward = H / Math.Tan(downAngle); // 沿光轴水平距离
            double u = pixel.X - Calibration.Cx;
            double side = forward * (u / Calibration.Fx);
            // Z 估算：用半径做近似（球越远半径越小），给一个合理的离地高度
            double z = Math.Clamp(0.067 / (radiusPx / 14.0), 0.05, 3.0); // 球径约 6.7cm
            return new Point3D { X = side, Y = forward, Z = z, TimestampMs = ts };
        }

        /// <summary>由最近两个点计算速度（m/s）。</summary>
        public Point3D ComputeVelocity(IList<Point3D> pts)
        {
            if (pts == null || pts.Count < 2) return new Point3D();
            var a = pts[pts.Count - 2];
            var b = pts[pts.Count - 1];
            double dt = (b.TimestampMs - a.TimestampMs) / 1000.0;
            if (dt <= 0) return new Point3D();
            return new Point3D
            {
                X = (b.X - a.X) / dt,
                Y = (b.Y - a.Y) / dt,
                Z = (b.Z - a.Z) / dt,
                TimestampMs = b.TimestampMs
            };
        }

        /// <summary>预测落地点（米）。基于当前 3D 位置 + 速度，做抛体运动直至 Z=0。</summary>
        public Point3D PredictLanding(Point3D pos, Point3D vel)
        {
            double z0 = Math.Max(0.01, pos.Z);
            double vz0 = vel.Z;
            // z(t) = z0 + vz0 t - 0.5 g t² = 0
            double a = 0.5 * G;
            double b = -vz0;
            double c = z0;
            double disc = b * b - 4 * a * c;
            if (disc < 0) return new Point3D(pos.X, pos.Y, 0);
            double t = (-b + Math.Sqrt(disc)) / (2 * a);
            return new Point3D
            {
                X = pos.X + vel.X * t,
                Y = pos.Y + vel.Y * t,
                Z = 0,
                TimestampMs = pos.TimestampMs + (long)(t * 1000)
            };
        }

        public double SpeedMps(Point3D vel)
            => Math.Sqrt(vel.X * vel.X + vel.Y * vel.Y + vel.Z * vel.Z);

        /// <summary>球径 m/s → km/h。</summary>
        public static double MpsToKmh(double mps) => mps * 3.6;
    }
}
