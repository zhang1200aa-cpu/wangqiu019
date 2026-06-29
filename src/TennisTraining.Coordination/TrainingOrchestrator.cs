using System;
using System.Threading.Tasks;
using TennisTraining.Core;
using TennisTraining.Vision;
using TennisTraining.Launcher;
using TennisTraining.Projection;
using TennisTraining.Data;

namespace TennisTraining.Coordination
{
    /// <summary>
    /// 训练编排器：流程管理 + 实时同步 + 异常处理。
    /// 串联 摄像头→球体识别→数据分析→发球机控制→投影→记录 的完整闭环。
    /// </summary>
    public sealed partial class TrainingOrchestrator : TennisModuleBase
    {
        public VisionModule Vision { get; }
        public LauncherControlEngine Launcher { get; }
        public ProjectionModule Projection { get; }
        public DataModule Data { get; }
        public TrainingStateMachine Fsm { get; } = new();

        private Trajectory _lastTraj;
        private long _curStartMs = -1;
        private Point3D _currentTarget = new(7, 0, 0);
        private Action _subTraj, _subLaunched, _subStatus;

        public TrainingOrchestrator(VisionModule vision, LauncherControlEngine launcher,
            ProjectionModule projection, DataModule data, EventBus bus = null, Action<string> logger = null)
            : base(bus, logger)
        {
            Vision = vision; Launcher = launcher; Projection = projection; Data = data;
            if (Bus == null) Bus = new EventBus();
            Fsm.StateChanged += (p, c) => Bus.Publish(EventNames.StateChanged, c);
            _subTraj = Bus.Subscribe<Trajectory>(EventNames.TrajectoryUpdated, OnTrajectory);
            _subLaunched = Bus.Subscribe<BallParameter>(EventNames.LauncherLaunched, OnLaunched);
            _subStatus = Bus.Subscribe<LauncherStatus>(EventNames.LauncherStatus, OnLauncherStatus);
        }

        public override string Name => "系统协调";

        /// <summary>开始一次训练。</summary>
        public void StartTraining(LaunchParameter param)
        {
            Log($"开始训练：{param?.Name}");
            Data.BeginSession(new TrainingSession
            {
                UserName = "学员",
                Mode = TrainingMode.Sequence,
                Position = param.Position,
                Difficulty = param.Difficulty
            });
            Fsm.Transition(TrainingState.WaitingForServe);
            Vision.StartAsync().Wait();
            Projection.StartAsync().Wait();
            Fsm.Transition(TrainingState.Serving);
            bool ok = Launcher.Launch(param);
            if (!ok)
            {
                Fsm.Transition(TrainingState.Error);
                Bus.Publish(EventNames.Error, "发球机启动失败");
            }
        }

        public void StopTraining()
        {
            Log("停止训练");
            Launcher.Stop();
            Fsm.Transition(TrainingState.Stopped);
            Data.EndSession();
            Vision.StopAsync().Wait();
            Fsm.Transition(TrainingState.Idle);
        }

        private void OnLaunched(BallParameter _)
        {
            _curStartMs = -1;
            Fsm.Transition(TrainingState.Tracking);
        }

        // 轨迹更新：实时投影 + 检测轨迹段结束（丢球重置）→ 结算这一球
        private void OnTrajectory(Trajectory t)
        {
            var prev = _lastTraj;
            _lastTraj = t;
            Projection.OnTrajectory(t);
            if (_curStartMs < 0) { _curStartMs = t.StartMs; return; }
            bool segmentEnd = t.StartMs > _curStartMs + 50
                || (t.Points.Count < 3 && prev != null && prev.Points.Count > 3);
            if (segmentEnd) { FinalizeBall(prev); _curStartMs = t.StartMs; }
        }

        private void OnLauncherStatus(LauncherStatus s)
        {
            if (s.ErrorCode != LauncherErrorCode.None)
            {
                Log("发球机故障: " + s.ErrorCode.ToMessage());
                Fsm.Transition(TrainingState.Error);
                Bus.Publish(EventNames.Error, s.ErrorCode.ToMessage());
            }
        }

        /// <summary>结算这一球：落点→命中判定→评分→记录→统计→投影。</summary>
        private void FinalizeBall(Trajectory traj)
        {
            if (traj == null || traj.Points.Count == 0) return;
            Fsm.Transition(TrainingState.Analyzing);
            var landing = traj.PredictedLanding ?? traj.Points[traj.Points.Count - 1];
            bool hit = IsInCourt(landing);
            var pb = new ProcessedBallData
            {
                Trajectory = traj,
                ActualLanding = landing,
                HitPoint = traj.Points[traj.Points.Count - 1],
                Hit = hit,
                QualityScore = AnalysisEngine.ScoreBall(
                    new ProcessedBallData { ActualLanding = landing, Trajectory = traj }, _currentTarget),
                Timestamp = DateTime.UtcNow,
                Note = hit ? "界内" : "界外"
            };
            Fsm.Transition(TrainingState.Recording);
            Data.RecordBall(pb);
            Projection.OnBallProcessed(pb);
            Bus.Publish(EventNames.BallProcessed, pb);
            Fsm.Transition(TrainingState.WaitingForServe);
        }

        private static bool IsInCourt(Point3D p)
            => Math.Abs(p.X) <= TennisCourt.HalfLength
            && Math.Abs(p.Y) <= TennisCourt.SinglesHalfWidth;

        public override Task StartAsync() { SetStatus(ModuleStatus.Running); return Task.CompletedTask; }
        public override Task StopAsync() { StopTraining(); return Task.CompletedTask; }

        public override void Dispose()
        {
            _subTraj?.Invoke(); _subLaunched?.Invoke(); _subStatus?.Invoke();
            base.Dispose();
        }
    }
}
