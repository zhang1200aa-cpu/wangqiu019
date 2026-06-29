using System;
using System.Collections.Generic;

namespace TennisTraining.Core
{
    /// <summary>
    /// 单球发球参数。字段含义与小程序 courseData.js 完全对齐：
    /// spinType 球性、leftRight 左右旋、speed 球速、rate 球频、height 弧度、hand 持拍手、spin 上下旋强度。
    /// </summary>
    [Serializable]
    public class BallParameter
    {
        public SpinType SpinType { get; set; } = SpinType.Topspin;
        /// <summary>左右旋转，范围 -15..15（下位机映射为 30..60）。</summary>
        public int LeftRight { get; set; } = 0;
        /// <summary>球速。</summary>
        public int Speed { get; set; } = 40;
        /// <summary>球频（两次发球间隔相关）。</summary>
        public int Rate { get; set; } = 5;
        /// <summary>弧度/高度。</summary>
        public int Height { get; set; } = 50;
        public Hand Hand { get; set; } = Hand.Right;
        /// <summary>上下旋强度原始值（1..n）。</summary>
        public int Spin { get; set; } = 1;
        /// <summary>该球与上一球之间的间隔（毫秒），仅序列/随机模式使用。</summary>
        public int DelayMs { get; set; } = 1500;

        public BallParameter Clone() => (BallParameter)MemberwiseClone();

        public override string ToString()
            => $"[{SpinType} 速{Speed} 频{Rate} 高{Height} 左右{LeftRight} 旋{Spin} {Hand}]";
    }

    /// <summary>一次发球任务：一个或多个球（多球课程）+ 摆放位置 + 模式信息。</summary>
    [Serializable]
    public class LaunchParameter
    {
        public string Name { get; set; } = "未命名";
        public DevicePosition Position { get; set; } = DevicePosition.BaselineCenter;
        public ServeMode Mode { get; set; } = ServeMode.Group;
        public ServeOrder Order { get; set; } = ServeOrder.Sequential;
        public int LoopGroupCount { get; set; } = 1;        // 循环组数
        public int LoopGroupIntervalMs { get; set; } = 1000; // 组间隔
        public int Difficulty { get; set; } = 1;            // 1/2/3
        public List<BallParameter> Balls { get; set; } = new();

        public LaunchParameter Clone()
        {
            var c = (LaunchParameter)MemberwiseClone();
            c.Balls = new List<BallParameter>(Balls.Count);
            foreach (var b in Balls) c.Balls.Add(b.Clone());
            return c;
        }
    }

    /// <summary>原始视频帧（BGR 字节流 + 元数据），不依赖 System.Drawing。</summary>
    public class FrameData
    {
        /// <summary>BGR 交织字节，长度 = Width*Height*3。</summary>
        public byte[] Data { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        /// <summary>帧时间戳（毫秒）。</summary>
        public long TimestampMs { get; set; }
        public int Sequence { get; set; }
    }

    /// <summary>摄像头标定：用于 2D 像素 → 3D 世界（地面）坐标转换。</summary>
    [Serializable]
    public class CameraCalibration
    {
        public double Fx { get; set; } = 800;
        public double Fy { get; set; } = 800;
        public double Cx { get; set; } = 640;
        public double Cy { get; set; } = 360;
        /// <summary>摄像头离地高度（米）。</summary>
        public double Height { get; set; } = 3.0;
        /// <summary>光轴俯仰角（度）。</summary>
        public double PitchDeg { get; set; } = 35;
        /// <summary>畸变系数（k1,k2,p1,p2,k3），可空。</summary>
        public double[] Distortion { get; set; }
    }

    /// <summary>2D 像素坐标。</summary>
    public struct Point2D
    {
        public double X; public double Y;
        public Point2D(double x, double y) { X = x; Y = y; }
    }

    /// <summary>3D 世界坐标（米）。</summary>
    [Serializable]
    public class Point3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public long TimestampMs { get; set; }
        public Point3D() { }
        public Point3D(double x, double y, double z) { X = x; Y = y; Z = z; }
    }
}
