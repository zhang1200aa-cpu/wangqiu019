using System;
using System.Collections.Generic;

namespace TennisTraining.Core
{
    /// <summary>单帧球检测结果。</summary>
    public class BallDetectionResult
    {
        public bool Detected { get; set; }
        public Point2D Center { get; set; }
        public double Radius { get; set; }
        public double Confidence { get; set; }
        public long TimestampMs { get; set; }
        public int FrameSequence { get; set; }
    }

    /// <summary>旋转信息估算。</summary>
    [Serializable]
    public class SpinInfo
    {
        public SpinType Type { get; set; } = SpinType.Flat;
        /// <summary>转速 RPM（估算）。</summary>
        public double Rpm { get; set; }
        public double Confidence { get; set; }
    }

    /// <summary>一段轨迹。</summary>
    [Serializable]
    public class Trajectory
    {
        public List<Point3D> Points { get; set; } = new();
        public Point3D Velocity { get; set; }          // m/s
        public double Speed { get; set; }              // m/s 标量
        public Point3D PredictedLanding { get; set; }   // 预测落地点（米）
        public SpinInfo Spin { get; set; } = new();
        public long StartMs { get; set; }
        public long EndMs { get; set; }
    }

    /// <summary>一球完整处理结果（视觉→分析输出）。</summary>
    [Serializable]
    public class ProcessedBallData
    {
        public long Id { get; set; }
        public Trajectory Trajectory { get; set; } = new();
        public BallParameter LaunchParam { get; set; }
        /// <summary>实际落地点（米）。</summary>
        public Point3D ActualLanding { get; set; }
        /// <summary>击球点（米）。</summary>
        public Point3D HitPoint { get; set; }
        public bool Hit { get; set; }
        /// <summary>质量评分 0..100。</summary>
        public double QualityScore { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Note { get; set; }
    }

    /// <summary>训练统计。</summary>
    [Serializable]
    public class TrainingStats
    {
        public int TotalBalls { get; set; }
        public int Hits { get; set; }
        public int Misses { get; set; }
        public double AvgSpeed { get; set; }
        public double MaxSpeed { get; set; }
        public double AvgQuality { get; set; }
        public double Consistency { get; set; }   // 一致性 0..100
        public TimeSpan Duration { get; set; }
        public double HitRate => TotalBalls == 0 ? 0 : (double)Hits / TotalBalls * 100;
    }

    /// <summary>一次训练会话。</summary>
    [Serializable]
    public class TrainingSession
    {
        public long Id { get; set; }
        public string UserName { get; set; } = "默认学员";
        public TrainingMode Mode { get; set; }
        public DevicePosition Position { get; set; }
        public int Difficulty { get; set; } = 1;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TrainingStats Stats { get; set; } = new();
        public List<ProcessedBallData> Balls { get; set; } = new();
    }

    /// <summary>发球机状态。</summary>
    [Serializable]
    public class LauncherStatus
    {
        public ModuleStatus Status { get; set; } = ModuleStatus.Uninitialized;
        public bool Connected { get; set; }
        public string Transport { get; set; }
        public LauncherErrorCode ErrorCode { get; set; } = LauncherErrorCode.None;
        public DateTime LastHeartbeat { get; set; }
        public override string ToString()
            => $"发球机[{Transport}] 连接={Connected} 状态={Status} 故障={ErrorCode.ToMessage()}";
    }
}
