using System;
using System.Text;

namespace TennisTraining.Core
{
    /// <summary>
    /// Modbus RTU CRC16 工具。与 HJSE-1001 下位机及小程序 util.js 的 CRCMB16
    /// 完全等价（标准 Modbus CRC16，多项式 0xA001，初值 0xFFFF，结果低字节在前）。
    /// </summary>
    public static class ModbusCrc16
    {
        /// <summary>计算 Modbus CRC16。</summary>
        public static ushort Compute(byte[] data, int offset = 0, int length = -1)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (length < 0) length = data.Length - offset;
            ushort crc = 0xFFFF;
            for (int i = 0; i < length; i++)
            {
                crc ^= data[offset + i];
                for (int b = 0; b < 8; b++)
                {
                    bool lsb = (crc & 1) != 0;
                    crc >>= 1;
                    if (lsb) crc ^= 0xA001;
                }
            }
            return crc;
        }

        /// <summary>返回带 CRC 的完整帧（低字节在前、高字节在后）。</summary>
        public static byte[] AppendCrc(byte[] data)
        {
            ushort crc = Compute(data);
            byte[] result = new byte[data.Length + 2];
            Buffer.BlockCopy(data, 0, result, 0, data.Length);
            result[data.Length] = (byte)(crc & 0xFF);
            result[data.Length + 1] = (byte)((crc >> 8) & 0xFF);
            return result;
        }

        /// <summary>校验一帧（含末尾 2 字节 CRC）是否正确。</summary>
        public static bool Validate(byte[] frame)
        {
            if (frame == null || frame.Length < 3) return false;
            ushort crc = Compute(frame, 0, frame.Length - 2);
            return frame[frame.Length - 2] == (byte)(crc & 0xFF)
                && frame[frame.Length - 1] == (byte)((crc >> 8) & 0xFF);
        }

        public static string ToHex(byte[] data)
        {
            var sb = new StringBuilder(data.Length * 2);
            for (int i = 0; i < data.Length; i++)
                sb.Append(data[i].ToString("X2"));
            return sb.ToString();
        }
    }
}
