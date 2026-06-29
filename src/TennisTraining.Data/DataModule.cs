using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TennisTraining.Core;

namespace TennisTraining.Data
{
    /// <summary>
    /// 数据记录与分析模块：封装仓储与分析引擎，提供会话生命周期管理。
    /// </summary>
    public sealed class DataModule : TennisModuleBase
    {
        public TrainingRepository Repository { get; }
        private TrainingSession _current;
        private readonly List<ProcessedBallData> _buffer = new();
        private DateTime _startTime;
        private int _seq;

        public TrainingSession CurrentSession => _current;

        public DataModule(string dbPath = null, EventBus bus = null, Action<string> logger = null)
            : base(bus, logger)
        {
            var path = dbPath ?? Path.Combine(AppContext.BaseDirectory, "tennis_training.db");
            Repository = new TrainingRepository(path);
            Log($"数据库: {path}");
        }

        public override string Name => "数据记录与分析";

        public long BeginSession(TrainingSession proto)
        {
            _current = proto ?? new TrainingSession();
            _current.StartTime = DateTime.UtcNow;
            _startTime = DateTime.UtcNow;
            _buffer.Clear(); _seq = 0;
            long id = Repository.InsertSession(_current);
            _current.Id = id;
            Log($"开始会话 #{id} 用户={_current.UserName}");
            return id;
        }

        public void RecordBall(ProcessedBallData ball)
        {
            if (_current == null) return;
            Repository.InsertBall(_current.Id, _seq++, ball);
            _buffer.Add(ball);
            var stats = AnalysisEngine.Compute(_buffer, DateTime.UtcNow - _startTime);
            _current.Stats = stats;
            Bus?.Publish(EventNames.StatsUpdated, stats);
        }

        public TrainingStats EndSession()
        {
            if (_current == null) return new TrainingStats();
            var stats = AnalysisEngine.Compute(_buffer, DateTime.UtcNow - _startTime);
            _current.Stats = stats;
            _current.EndTime = DateTime.UtcNow;
            Repository.UpdateSessionStats(_current.Id, stats, _current.EndTime);
            Log($"结束会话 #{_current.Id} 命中率{stats.HitRate:0.0}% 均速{stats.AvgSpeed:0.0}");
            var done = _current;
            _current = null;
            _buffer.Clear();
            return stats;
        }

        public List<TrainingSession> History(int n = 20) => Repository.GetRecentSessions(n);

        public override async Task<SelfTestResult> SelfTestAsync()
        {
            var r = new SelfTestResult { Module = Name };
            var sw = Stopwatch.StartNew();
            await Task.Yield();
            string temp = null;
            TrainingRepository repo = null;
            try
            {
                temp = Path.Combine(Path.GetTempPath(), "tt_selftest_" + Guid.NewGuid().ToString("N") + ".db");
                repo = new TrainingRepository(temp);
                var s = new TrainingSession
                {
                    UserName = "自检学员",
                    Mode = TrainingMode.Sequence,
                    Position = DevicePosition.BaselineCenter,
                    Difficulty = 2,
                    StartTime = DateTime.UtcNow
                };
                long id = repo.InsertSession(s);
                r.Details.Add($"插入会话 #{id}");
                var balls = new List<ProcessedBallData>();
                var rnd = new Random(1);
                for (int i = 0; i < 10; i++)
                {
                    var b = new ProcessedBallData
                    {
                        Hit = rnd.NextDouble() > 0.3,
                        QualityScore = rnd.Next(50, 100),
                        Trajectory = new Trajectory { Speed = 20 + rnd.Next(0, 20) },
                        ActualLanding = new Point3D(rnd.NextDouble(), 8 + rnd.NextDouble(), 0),
                        Timestamp = DateTime.UtcNow
                    };
                    repo.InsertBall(id, i, b);
                    balls.Add(b);
                }
                var stats = AnalysisEngine.Compute(balls, TimeSpan.FromSeconds(60));
                repo.UpdateSessionStats(id, stats, DateTime.UtcNow);
                int cnt = repo.CountSessions();
                var recent = repo.GetRecentSessions(5);
                r.Details.Add($"会话数: {cnt}");
                r.Details.Add($"最近会话命中率: {recent[0].Stats.HitRate:0.0}%");
                r.Details.Add($"最近会话均速: {recent[0].Stats.AvgSpeed:0.0}");
                r.Success = id > 0 && cnt >= 1 && recent[0].Stats.TotalBalls == 10;
                r.Message = r.Success ? "数据模块自检通过（建表/写入/查询/统计）" : "数据模块自检失败";
            }
            catch (Exception ex)
            {
                r.Success = false;
                r.Message = ex.Message;
            }
            finally
            {
                repo?.Dispose();
                try { if (temp != null && File.Exists(temp)) File.Delete(temp); } catch { }
            }
            sw.Stop();
            r.ElapsedMs = sw.ElapsedMilliseconds;
            return r;
        }

        public override void Dispose() => Repository?.Dispose();
    }
}
