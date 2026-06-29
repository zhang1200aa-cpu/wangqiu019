using System;
using System.Drawing;
using TennisTraining.Core;

namespace TennisTraining.Projection
{
    /// <summary>
    /// 网球场几何与坐标映射（ITF 标准场地，俯视图）。
    /// 世界坐标：X=沿底线方向长度（中线为0），Y=横向宽度（中线为0），单位米。
    /// </summary>
    public class TennisCourt
    {
        // ITF 标准尺寸（米）
        public const double HalfLength = 11.885; // 半场长（底线到中线）
        public const double SinglesHalfWidth = 4.115;
        public const double DoublesHalfWidth = 5.485;
        public const double ServiceLineX = 6.4;     // 发球线距网
        public const double NetHalfWidth = DoublesHalfWidth;
        public const double BaselineToService = 5.485;

        public double MinX => -HalfLength;
        public double MaxX => HalfLength;
        public double MinY => -DoublesHalfWidth;
        public double MaxY => DoublesHalfWidth;

        public int SurfaceWidth { get; }
        public int SurfaceHeight { get; }
        public int Margin { get; set; } = 40;

        public TennisCourt(int surfaceWidth, int surfaceHeight)
        {
            SurfaceWidth = surfaceWidth;
            SurfaceHeight = surfaceHeight;
        }

        /// <summary>世界坐标 → 屏幕像素（俯视图，长度方向水平）。</summary>
        public PointF CourtToScreen(double x, double y)
        {
            double w = MaxX - MinX, h = MaxY - MinY;
            double sx = Margin + (x - MinX) / w * (SurfaceWidth - 2 * Margin);
            double sy = Margin + (y - MinY) / h * (SurfaceHeight - 2 * Margin);
            return new PointF((float)sx, (float)sy);
        }

        public PointF CourtToScreen(Point3D p) => CourtToScreen(p.X, p.Y);

        /// <summary>球场线条矩形（用于绘制）。</summary>
        public RectangleF CourtBounds()
        {
            var tl = CourtToScreen(MinX, MinY);
            var br = CourtToScreen(MaxX, MaxY);
            return new RectangleF(tl.X, tl.Y, br.X - tl.X, br.Y - tl.Y);
        }

        /// <summary>单打边线、双打边线、发球线、网线等关键线段。</summary>
        public (PointF a, PointF b)[] GetLines()
        {
            return new[] {
                (CourtToScreen(MinX, -DoublesHalfWidth), CourtToScreen(MaxX, -DoublesHalfWidth)), // 下双打边线
                (CourtToScreen(MinX, DoublesHalfWidth), CourtToScreen(MaxX, DoublesHalfWidth)),   // 上双打边线
                (CourtToScreen(MinX, -SinglesHalfWidth), CourtToScreen(MaxX, -SinglesHalfWidth)), // 下单打边线
                (CourtToScreen(MinX, SinglesHalfWidth), CourtToScreen(MaxX, SinglesHalfWidth)),   // 上单打边线
                (CourtToScreen(MinX, -DoublesHalfWidth), CourtToScreen(MinX, DoublesHalfWidth)),  // 左底线
                (CourtToScreen(MaxX, -DoublesHalfWidth), CourtToScreen(MaxX, DoublesHalfWidth)),  // 右底线
                (CourtToScreen(-ServiceLineX, -SinglesHalfWidth), CourtToScreen(-ServiceLineX, SinglesHalfWidth)), // 左发球线
                (CourtToScreen(ServiceLineX, -SinglesHalfWidth), CourtToScreen(ServiceLineX, SinglesHalfWidth)),  // 右发球线
                (CourtToScreen(0, -DoublesHalfWidth), CourtToScreen(0, DoublesHalfWidth)),        // 网
                (CourtToScreen(0, 0), CourtToScreen(-ServiceLineX, 0)), // 中线左
                (CourtToScreen(0, 0), CourtToScreen(ServiceLineX, 0))   // 中线右
            };
        }
    }
}
