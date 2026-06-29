using System;
using System.Collections.Generic;
using TennisTraining.Core;

namespace TennisTraining.Launcher
{
    /// <summary>设备控制值（寄存器 0x0003），与 serveControl.vue changeDeviceState 一致。</summary>
    public enum DeviceControl : ushort
    {
        Stop = 0x0000,   // 停止
        Start = 0x0001,  // 开始
        Fault = 0x0002,  // 故障
        Pause = 0x0003   // 暂停
    }

    /// <summary>微调值（寄存器 0x001D），与 serveControl.vue 微调指令一致。</summary>
    public enum FineTuneType : ushort
    {
        HeightPlus = 0x0001,      // 弧度+
        HeightMinus = 0x0002,     // 弧度-
        HorizontalPlus = 0x0003,  // 水平+
        HorizontalMinus = 0x0004, // 水平-
        RatePlus = 0x0005,        // 球频+
        RateMinus = 0x0006        // 球频-
    }

    /// <summary>
    /// HJSE-1001 发球机命令集。所有寄存器地址/取值均来自小程序 serveControl.vue，
    /// 通过 Modbus RTU（BLE 透传）下发。
    /// </summary>
    public static class LauncherCommands
    {
        public const byte Address = ModbusFrameBuilder.DefaultAddress;

        /// <summary>设备控制寄存器（功能码 0x06）。</summary>
        public const ushort RegDeviceControl = 0x0003;
        /// <summary>微调寄存器（功能码 0x06）。</summary>
        public const ushort RegFineTune = 0x001D;
        /// <summary>会话配置起始寄存器（功能码 0x10，5 个寄存器）。</summary>
        public const ushort RegSessionConfig = 0x001A;
        /// <summary>球参数起始寄存器（功能码 0x10）。</summary>
        public const ushort RegBallParam = ModbusFrameBuilder.BallParamStartAddress;

        /// <summary>状态/故障码寄存器（查询用，地址需与下位机确认；默认 0x0000）。</summary>
        public const ushort RegStatus = 0x0000;
        public const ushort RegFaultCode = 0x0001;

        /// <summary>设备控制帧：开始/停止/暂停/故障。</summary>
        public static byte[] BuildDeviceControl(DeviceControl ctrl)
            => ModbusFrameBuilder.BuildWriteSingleRegister(Address, RegDeviceControl, (ushort)ctrl);

        /// <summary>微调帧。</summary>
        public static byte[] BuildFineTune(FineTuneType tune)
            => ModbusFrameBuilder.BuildWriteSingleRegister(Address, RegFineTune, (ushort)tune);

        /// <summary>
        /// 会话配置帧（5 寄存器 @0x001A）：[总时间, 课程循环数, 组循环数, 微调, 组间隔]。
        /// 组循环数 0xFFFF 表示不限制（计时/自由模式）。
        /// </summary>
        public static byte[] BuildSessionConfig(
            ushort totalTimeSec, ushort courseLoopCount, ushort groupLoopCount,
            ushort fineTune = 0, ushort groupInterval = 1)
        {
            var regs = new ushort[5];
            regs[0] = totalTimeSec;
            regs[1] = courseLoopCount;
            regs[2] = groupLoopCount;
            regs[3] = fineTune;
            regs[4] = groupInterval;
            return ModbusFrameBuilder.BuildWriteMultipleRegisters(Address, RegSessionConfig, regs);
        }

        /// <summary>根据发球模式生成会话配置帧。</summary>
        public static byte[] BuildSessionConfigForMode(
            ServeMode mode, int totalTimeSec, int courseLoopCount, int groupLoopCount, int groupInterval)
        {
            ushort total = mode == ServeMode.Timing ? (ushort)Clamp(totalTimeSec, 0, 65535) : (ushort)0;
            ushort course = (ushort)Clamp(courseLoopCount, 0, 65535);
            ushort group = mode == ServeMode.Group ? (ushort)Clamp(groupLoopCount, 0, 65535) : (ushort)0xFFFF;
            ushort interval = (ushort)Clamp(groupInterval, 0, 65535);
            return BuildSessionConfig(total, course, group, 0, interval);
        }

        /// <summary>球参数帧序列（每包 15 球，最多 45 球）。</summary>
        public static List<byte[]> BuildBallFrames(byte[][] encodedBalls)
            => BallFramePacker.Pack(encodedBalls, RegBallParam);

        /// <summary>查询故障码帧（读寄存器 0x0001）。</summary>
        public static byte[] BuildReadFaultCode()
            => ModbusFrameBuilder.BuildReadHoldingRegisters(Address, RegFaultCode, 1);

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
    }
}
