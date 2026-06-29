using System;
using System.Collections.Generic;

namespace TennisTraining.Core
{
    /// <summary>
    /// Modbus RTU 帧构造器。HJSE 发球机用功能码 0x10（写多个寄存器）下发球参数。
    /// 帧格式：[地址][功能码][起始地址 H/L][寄存器数 H/L][字节数][数据...][CRC L][CRC H]
    /// </summary>
    public static class ModbusFrameBuilder
    {
        public const byte DefaultAddress = 0x01;
        public const byte FuncWriteMultiple = 0x10;
        /// <summary>球参数起始寄存器地址（与小程序 buildSendList 一致）。</summary>
        public const ushort BallParamStartAddress = 0x0021;

        /// <summary>构造“写多个寄存器”帧（返回含 CRC 的完整帧）。</summary>
        public static byte[] BuildWriteMultipleRegisters(
            byte address, ushort startAddress, ushort[] registers)
        {
            if (registers == null || registers.Length == 0)
                throw new ArgumentException("寄存器列表不能为空", nameof(registers));
            if (registers.Length > 123)
                throw new ArgumentOutOfRangeException(nameof(registers), "单帧寄存器数量不能超过 123");

            int byteCount = registers.Length * 2;
            byte[] body = new byte[6 + 1 + byteCount];
            body[0] = address;
            body[1] = FuncWriteMultiple;
            body[2] = (byte)((startAddress >> 8) & 0xFF);
            body[3] = (byte)(startAddress & 0xFF);
            body[4] = (byte)((registers.Length >> 8) & 0xFF);
            body[5] = (byte)(registers.Length & 0xFF);
            body[6] = (byte)byteCount;
            for (int i = 0; i < registers.Length; i++)
            {
                body[7 + i * 2] = (byte)((registers[i] >> 8) & 0xFF);
                body[8 + i * 2] = (byte)(registers[i] & 0xFF);
            }
            return ModbusCrc16.AppendCrc(body);
        }

        /// <summary>读保持寄存器帧（功能码 0x03），用于查询状态/故障码。</summary>
        public static byte[] BuildReadHoldingRegisters(byte address, ushort startAddress, ushort quantity)
        {
            byte[] body = new byte[6];
            body[0] = address;
            body[1] = 0x03;
            body[2] = (byte)((startAddress >> 8) & 0xFF);
            body[3] = (byte)(startAddress & 0xFF);
            body[4] = (byte)((quantity >> 8) & 0xFF);
            body[5] = (byte)(quantity & 0xFF);
            return ModbusCrc16.AppendCrc(body);
        }

        /// <summary>写单个寄存器帧（功能码 0x06）。HJSE 用它做设备控制/微调。</summary>
        public static byte[] BuildWriteSingleRegister(byte address, ushort register, ushort value)
        {
            byte[] body = new byte[6];
            body[0] = address;
            body[1] = 0x06;
            body[2] = (byte)((register >> 8) & 0xFF);
            body[3] = (byte)(register & 0xFF);
            body[4] = (byte)((value >> 8) & 0xFF);
            body[5] = (byte)(value & 0xFF);
            return ModbusCrc16.AppendCrc(body);
        }
    }

    /// <summary>
    /// HJSE 球参数打包器：把已编码球数据按“每球 3 个寄存器=6 字节”分组成多个 Modbus 帧。
    /// 与小程序 util.js buildSendList 逻辑一致（每包 15 球，最多 45 球，不足补空球）。
    /// </summary>
    public static class BallFramePacker
    {
        public const int MaxBalls = 45;
        public const int BytesPerBall = 6;
        public const int GroupSize = 15;

        /// <summary>
        /// 将已编码（6 字节/球）的球数据打包成若干 Modbus 帧。
        /// </summary>
        /// <param name="encodedBalls">每个元素长度 6：[speed,height,leftRight,rate,spin,reserved]。</param>
        public static List<byte[]> Pack(byte[][] encodedBalls, ushort startAddress = ModbusFrameBuilder.BallParamStartAddress)
        {
            var result = new List<byte[]>();
            if (encodedBalls == null || encodedBalls.Length == 0) return result;

            var all = new List<byte[]>(MaxBalls);
            for (int i = 0; i < MaxBalls; i++)
                all.Add(i < encodedBalls.Length ? encodedBalls[i] : new byte[BytesPerBall]);

            ushort addr = startAddress;
            for (int i = 0; i < all.Count; i += GroupSize)
            {
                int count = GroupSize;
                var regs = new ushort[count * 3];
                for (int j = 0; j < count; j++)
                {
                    var b = all[i + j];
                    regs[j * 3 + 0] = (ushort)((b[0] << 8) | b[1]);
                    regs[j * 3 + 1] = (ushort)((b[2] << 8) | b[3]);
                    regs[j * 3 + 2] = (ushort)((b[4] << 8) | b[5]);
                }
                var frame = ModbusFrameBuilder.BuildWriteMultipleRegisters(
                    ModbusFrameBuilder.DefaultAddress, addr, regs);
                result.Add(frame);
                addr += (ushort)(count * 3);
            }
            return result;
        }
    }
}
