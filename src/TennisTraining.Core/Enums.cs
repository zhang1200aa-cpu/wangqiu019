using System;

namespace TennisTraining.Core
{
    /// <summary>球性：与发球机下位机协议一致（1上旋 / 2平击 / 3下旋）。</summary>
    public enum SpinType
    {
        Topspin = 1,   // 上旋
        Flat = 2,       // 平击
        Slice = 3       // 下旋
    }

    /// <summary>设备摆放位置：与小程序 courseData 的 devPosition 一致。</summary>
    public enum DevicePosition
    {
        BaselineLeft = 1,    // 底线左侧
        BaselineCenter = 2,  // 底线中线
        BaselineRight = 3,   // 底线右侧
        TLine = 4,           // T 字线
        LeftServe = 5,       // 左抛球位
        RightServe = 6       // 右抛球位
    }

    /// <summary>持拍手。</summary>
    public enum Hand
    {
        Left = 1,
        Right = 2
    }

    /// <summary>训练模式。</summary>
    public enum TrainingMode
    {
        FixedPoint,          // 定点
        Random,              // 随机
        Sequence,            // 序列
        Custom,              // 自定义
        OpponentSimulation   // 对抗模拟
    }

    /// <summary>发球模式：1 组模式 / 2 计时模式 / 3 自由模式。</summary>
    public enum ServeMode
    {
        Group = 1,
        Timing = 2,
        Free = 3
    }

    /// <summary>发球顺序：1 顺序 / 2 随机。</summary>
    public enum ServeOrder
    {
        Sequential = 1,
        Random = 2
    }

    /// <summary>训练状态机状态。</summary>
    public enum TrainingState
    {
        Idle,             // 待机
        WaitingForServe,  // 等待发球
        Serving,          // 发球中
        Tracking,         // 球路追踪
        Analyzing,        // 数据分析
        Recording,        // 记录
        Paused,           // 暂停
        Stopped,          // 停止
        Error             // 异常
    }

    /// <summary>下位机故障码（与 bleUtil.js getErrorCodeTable 一致）。</summary>
    public enum LauncherErrorCode
    {
        None = 0,
        UpperFeedMotorStall = 1,   // 上发球电机堵转
        LowerFeedMotorStall = 2,   // 下发球电机堵转
        FeedMotorStall = 3,        // 拨球电机堵转
        InletJam = 4,              // 进球口堵球
        RotationMotorStall = 5,    // 旋转电机堵转
        RotationMotorFault = 6,    // 旋转电机故障
        PitchMotorStall = 7,       // 俯仰电机堵转
        PitchMotorFault = 8        // 俯仰电机故障
    }

    /// <summary>模块运行状态。</summary>
    public enum ModuleStatus
    {
        Uninitialized,
        Ready,
        Running,
        Degraded,
        Faulted,
        Stopped
    }

    public static class LauncherErrorExtensions
    {
        public static string ToMessage(this LauncherErrorCode code)
        {
            return code switch
            {
                LauncherErrorCode.UpperFeedMotorStall => "上发球电机堵转",
                LauncherErrorCode.LowerFeedMotorStall => "下发球电机堵转",
                LauncherErrorCode.FeedMotorStall => "拨球电机堵转",
                LauncherErrorCode.InletJam => "进球口堵球",
                LauncherErrorCode.RotationMotorStall => "旋转电机堵转",
                LauncherErrorCode.RotationMotorFault => "旋转电机故障",
                LauncherErrorCode.PitchMotorStall => "俯仰电机堵转",
                LauncherErrorCode.PitchMotorFault => "俯仰电机故障",
                _ => "未定义故障 " + (int)code
            };
        }
    }
}
