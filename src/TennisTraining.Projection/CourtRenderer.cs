using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using TennisTraining.Core;

namespace TennisTraining.Projection
{
    /// <summary>
    /// GDI+ 渲染器：在 Graphics 上绘制球场、轨迹、落点、热力图、靶点、评分。
    /// 输出可送至投影（第二屏无边框窗体）或主程序预览。
    /// </summary>
    public class CourtRenderer
    {
        public TennisCourt Court { get; }
        public Brush CourtBrush { get; set; } = new SolidBrush(Color.FromArgb(255, 34, 70, 45));
        public Pen LinePen { get; set; } = new Pen(Color.White, 2) { DashStyle = DashStyle.Solid };
        public Pen NetPen { get; set; } = new Pen(Color.LightSkyBlue, 3);
        public Color HitColor { get; set; } = Color.LimeGreen;
        public Color MissColor { get; set; } = Color.OrangeRed;
        public Color TrajectoryColor { get; set; } = Color.Yellow;

        public CourtRenderer(TennisCourt court) { Court = court; }

        public void DrawCourt(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(CourtBrush, Court.CourtBounds());
            foreach (var (a, b) in Court.GetLines())
            {
                if (a.X == b.X && Math.Abs(a.X - Court.CourtToScreen(0, 0).X) < 0.5f)
                    g.DrawLine(NetPen, a, b);
                else
                    g.DrawLine(LinePen, a, b);
            }
        }

        public void DrawTrajectory(Graphics g, Trajectory t)
        {
            if (t?.Points == null || t.Points.Count < 2) return;
            using var p = new Pen(TrajectoryColor, 2) { DashStyle = DashStyle.Dot };
            var pts = t.Points.Select(Court.CourtToScreen).ToArray();
            g.DrawLines(p, pts);
            // 起点圆点
            g.FillEllipse(Brushes.Yellow, pts[0].X - 3, pts[0].Y - 3, 6, 6);
            // 预测落点
            if (t.PredictedLanding != null)
            {
                var lp = Court.CourtToScreen(t.PredictedLanding);
                g.DrawEllipse(Pens.White, lp.X - 8, lp.Y - 8, 16, 16);
            }
        }

        public void DrawLandings(Graphics g, IEnumerable<Point3D> landings, Func<Point3D,bool> isHit)
        {
            foreach (var lp in landings)
            {
                var sp = Court.CourtToScreen(lp);
                var c = isHit(lp) ? HitColor : MissColor;
                using var b = new SolidBrush(Color.FromArgb(160, c));
                g.FillEllipse(b, sp.X - 6, sp.Y - 6, 12, 12);
            }
        }

        public void DrawHeatmap(Graphics g, IEnumerable<Point3D> landings)
        {
            var list = landings?.ToList();
            if (list == null || list.Count == 0) return;
            double minX = list.Min(p => p.X), maxX = list.Max(p => p.X);
            double minY = list.Min(p => p.Y), maxY = list.Max(p => p.Y);
            double rangeX = Math.Max(0.5, maxX - minX), rangeY = Math.Max(0.5, maxY - minY);
            foreach (var p in list)
            {
                var sp = Court.CourtToScreen(p);
                double intensity = 1.0; // 单点强度
                int alpha = (int)(40 + intensity * 120);
                using var b = new SolidBrush(Color.FromArgb(alpha, 255, 80, 0));
                g.FillEllipse(b, sp.X - 18, sp.Y - 18, 36, 36);
            }
        }

        public void DrawTargets(Graphics g, IEnumerable<Point3D> targets)
        {
            foreach (var t in targets ?? Enumerable.Empty<Point3D>())
            {
                var sp = Court.CourtToScreen(t);
                g.DrawEllipse(new Pen(Color.Cyan, 2) { DashStyle = DashStyle.Dash }, sp.X - 20, sp.Y - 20, 40, 40);
            }
        }

        public void DrawScore(Graphics g, TrainingStats st, int x = 12, int y = 12)
        {
            using var f = new Font("微软雅黑", 14, FontStyle.Bold);
            string txt = $"球数 {st.TotalBalls}  命中 {st.Hits}  命中率 {st.HitRate:0.0}%  均速 {st.AvgSpeed:0.0}m/s  一致性 {st.Consistency:0}";
            using var b = new SolidBrush(Color.White);
            using var bg = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
            var size = g.MeasureString(txt, f);
            g.FillRectangle(bg, x - 4, y - 2, size.Width + 8, size.Height + 4);
            g.DrawString(txt, f, b, x, y);
        }
    }
}
