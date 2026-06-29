using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TennisTraining.Core;

namespace TennisTraining.Projection
{
    /// <summary>
    /// 投影显示与交互模块：维护实时轨迹/落点/统计，渲染到画布并对外提供位图。
    /// </summary>
    public sealed class ProjectionModule : TennisModuleBase
    {
        public BitmapProjectorView View { get; }
        public CourtRenderer Renderer { get; }
        private Trajectory _liveTraj;
        private readonly List<Point3D> _landings = new();
        private TrainingStats _stats = new();
        private readonly List<Point3D> _targets = new();

        public ProjectionModule(int width = 1280, int height = 720, EventBus bus = null, Action<string> logger = null)
            : base(bus, logger)
        {
            View = new BitmapProjectorView(width, height);
            Renderer = new CourtRenderer(new TennisCourt(width, height));
        }

        public override string Name => "投影显示";

        public void OnTrajectory(Trajectory t) { _liveTraj = t; Render(); }
        public void OnBallProcessed(ProcessedBallData b)
        {
            if (b?.ActualLanding != null) _landings.Add(b.ActualLanding);
            if (_landings.Count > 200) _landings.RemoveAt(0);
        }
        public void OnStats(TrainingStats s) { _stats = s; Render(); }
        public void SetTargets(IEnumerable<Point3D> ts)
        {
            _targets.Clear();
            if (ts != null) _targets.AddRange(ts);
        }

        public void Render()
        {
            View.BeginDraw();
            View.Clear(unchecked((int)0xFF0B1320));
            Renderer.DrawCourt(View.Graphics);
            Renderer.DrawHeatmap(View.Graphics, _landings);
            Renderer.DrawTargets(View.Graphics, _targets);
            Renderer.DrawLandings(View.Graphics, _landings, p => true);
            if (_liveTraj != null) Renderer.DrawTrajectory(View.Graphics, _liveTraj);
            Renderer.DrawScore(View.Graphics, _stats);
            View.EndDraw();
        }

        /// <summary>保存当前画面到 PNG（回放/导出用）。</summary>
        public string SaveSnapshot(string path)
        {
            Render();
            View.Bitmap.Save(path, ImageFormat.Png);
            return path;
        }

        public override Task StartAsync() { SetStatus(ModuleStatus.Running); Render(); return Task.CompletedTask; }
        public override Task StopAsync() { SetStatus(ModuleStatus.Stopped); return Task.CompletedTask; }

        public override async Task<SelfTestResult> SelfTestAsync()
        {
            var r = new SelfTestResult { Module = Name };
            var sw = Stopwatch.StartNew();
            await Task.Yield();
            string png = null;
            try
            {
                OnTrajectory(new Trajectory
                {
                    Points = new() { new Point3D(-11, 0, 1), new Point3D(-5, 1, 1.5), new Point3D(0, 0, 1), new Point3D(5, -1, 0.5) },
                    PredictedLanding = new Point3D(8, 0.5, 0),
                    Speed = 30
                });
                for (int i = 0; i < 12; i++)
                {
                    OnBallProcessed(new ProcessedBallData
                    {
                        Hit = i % 4 != 0,
                        ActualLanding = new Point3D(-9 + i * 1.5, (i % 3 - 1) * 1.2, 0)
                    });
                }
                OnStats(new TrainingStats { TotalBalls = 12, Hits = 9, AvgSpeed = 28, Consistency = 75 });
                SetTargets(new[] { new Point3D(7, 0, 0), new Point3D(-7, 0, 0) });
                png = Path.Combine(Path.GetTempPath(), "tt_proj_" + Guid.NewGuid().ToString("N") + ".png");
                SaveSnapshot(png);
                var fi = new FileInfo(png);
                r.Details.Add($"画布尺寸: {View.Width}x{View.Height}");
                r.Details.Add($"PNG 体积: {fi.Length} 字节");
                r.Success = fi.Exists && fi.Length > 5000;
                r.Message = r.Success ? "投影模块自检通过（渲染球场/轨迹/热力图/PNG导出）" : "投影模块自检失败";
            }
            catch (Exception ex) { r.Success = false; r.Message = ex.Message; }
            finally { try { if (png != null && File.Exists(png)) File.Delete(png); } catch { } }
            sw.Stop();
            r.ElapsedMs = sw.ElapsedMilliseconds;
            return r;
        }

        public override void Dispose() => View?.Dispose();
    }
}
