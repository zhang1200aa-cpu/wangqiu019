using System;
using System.Collections.Generic;
using System.Linq;
using TennisTraining.Core;

namespace TennisTraining.Data
{
    /// <summary>
    /// 性能分析引擎：从逐球数据计算命中率、平均/最大速度、质量评分、一致性等。
    /// </summary>
    public static class AnalysisEngine
    {
        /// <summary>统计一组球数据。</summary>
        public static TrainingStats Compute(IEnumerable<ProcessedBallData> balls, TimeSpan? duration = null)
        {
            var list = balls?.ToList() ?? new List<ProcessedBallData>();
            var st = new TrainingStats
            {
                TotalBalls = list.Count,
                Hits = list.Count(b => b.Hit),
                Misses = list.Count(b => !b.Hit)
            };
            if (list.Count > 0)
            {
                var speeds = list.Where(b => b.Trajectory?.Speed > 0).Select(b => b.Trajectory.Speed).ToList();
                if (speeds.Count > 0)
                {
                    st.AvgSpeed = speeds.Average();
                    st.MaxSpeed = speeds.Max();
                }
                st.AvgQuality = list.Average(b => b.QualityScore);
                st.Consistency = ComputeConsistency(list);
            }
            st.Duration = duration ?? TimeSpan.Zero;
            return st;
        }

        /// <summary>一致性：速度相对离散度的补值（0..100）。</summary>
        public static double ComputeConsistency(IList<ProcessedBallData> balls)
        {
            var speeds = balls.Where(b => b.Trajectory?.Speed > 0).Select(b => b.Trajectory.Speed).ToList();
            if (speeds.Count < 2) return 100;
            double mean = speeds.Average();
            double std = Math.Sqrt(speeds.Average(v => (v - mean) * (v - mean)));
            double cv = mean > 0 ? std / mean : 0; // 变异系数
            return Math.Max(0, Math.Min(100, (1 - cv) * 100));
        }

        /// <summary>单球质量评分（0..100）：综合落点偏差与速度。</summary>
        public static double ScoreBall(ProcessedBallData b, Point3D target)
        {
            if (b?.ActualLanding == null || target == null) return b?.QualityScore ?? 0;
            double dx = b.ActualLanding.X - target.X;
            double dy = b.ActualLanding.Y - target.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            // 距离误差 0→100分，3米→0分
            double acc = Math.Max(0, 100 - dist / 3.0 * 100);
            double spd = Math.Min(100, (b.Trajectory?.Speed ?? 0) / 40.0 * 100);
            return 0.7 * acc + 0.3 * spd;
        }
    }
}
