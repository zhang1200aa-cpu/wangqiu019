using System;
using System.Collections.Generic;
using TennisTraining.Core;

namespace TennisTraining.Launcher
{
    /// <summary>
    /// 球参数编码器：把 BallParameter 编码为下位机 6 字节/球格式，
    /// 完全移植自小程序 util.js 的 processBalls。
    /// 字节序：[speed, height, leftRight(已映射), rate, spin(已映射), 0x00]
    /// </summary>
    public static class BallParameterEncoder
    {
        /// <summary>左右旋映射：-15..15 → 30..60（中心 45）。</summary>
        public static int MapLeftRight(int leftRight)
        {
            if (leftRight < -15) leftRight = -15;
            if (leftRight > 15) leftRight = 15;
            return leftRight + 45;
        }

        /// <summary>旋转值映射：上旋→{4,3,2}，平击→5，下旋→{6,7,8}。</summary>
        public static int MapSpin(SpinType spinType, int spinRaw)
        {
            if (spinRaw < 1) spinRaw = 1;
            switch (spinType)
            {
                case SpinType.Topspin:
                    return 4 - (int)Math.Floor((spinRaw - 1) / 2.0);
                case SpinType.Flat:
                    return 5;
                case SpinType.Slice:
                    return 6 + (int)Math.Floor((spinRaw - 1) / 2.0);
                default:
                    return spinRaw;
            }
        }

        /// <summary>编码单球为 6 字节。</summary>
        public static byte[] Encode(BallParameter b)
        {
            if (b == null) return new byte[6];
            int speed = b.Speed < 0 ? 0 : (b.Speed > 255 ? 255 : b.Speed);
            int height = b.Height < 0 ? 0 : (b.Height > 255 ? 255 : b.Height);
            int lr = MapLeftRight(b.LeftRight);
            int rate = b.Rate < 0 ? 0 : (b.Rate > 255 ? 255 : b.Rate);
            int spin = MapSpin(b.SpinType, b.Spin);
            if (spin < 0) spin = 0;
            if (spin > 255) spin = 255;

            return new byte[6]
            {
                (byte)speed,
                (byte)height,
                (byte)lr,
                (byte)rate,
                (byte)spin,
                0x00
            };
        }

        /// <summary>批量编码。</summary>
        public static byte[][] EncodeAll(IEnumerable<BallParameter> balls)
        {
            var list = new List<byte[]>();
            if (balls == null) return list.ToArray();
            foreach (var b in balls) list.Add(Encode(b));
            return list.ToArray();
        }
    }
}
