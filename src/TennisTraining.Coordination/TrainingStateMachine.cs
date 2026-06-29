using System;
using System.Collections.Generic;
using TennisTraining.Core;

namespace TennisTraining.Coordination
{
    /// <summary>
    /// 训练状态机：管理 待发球→发球→追踪→分析→记录 的状态流转与合法性校验。
    /// </summary>
    public class TrainingStateMachine
    {
        public TrainingState State { get; private set; } = TrainingState.Idle;
        public event Action<TrainingState, TrainingState> StateChanged;

        // 合法迁移表
        private static readonly Dictionary<TrainingState, HashSet<TrainingState>> _transitions = new()
        {
            { TrainingState.Idle, new(){ TrainingState.WaitingForServe, TrainingState.Stopped } },
            { TrainingState.WaitingForServe, new(){ TrainingState.Serving, TrainingState.Stopped, TrainingState.Error } },
            { TrainingState.Serving, new(){ TrainingState.Tracking, TrainingState.Paused, TrainingState.Stopped, TrainingState.Error } },
            { TrainingState.Tracking, new(){ TrainingState.Analyzing, TrainingState.WaitingForServe, TrainingState.Paused, TrainingState.Stopped, TrainingState.Error } },
            { TrainingState.Analyzing, new(){ TrainingState.Recording, TrainingState.WaitingForServe, TrainingState.Error } },
            { TrainingState.Recording, new(){ TrainingState.WaitingForServe, TrainingState.Serving, TrainingState.Stopped } },
            { TrainingState.Paused, new(){ TrainingState.Serving, TrainingState.Stopped } },
            { TrainingState.Error, new(){ TrainingState.Idle, TrainingState.Stopped } },
            { TrainingState.Stopped, new(){ TrainingState.Idle } },
        };

        public bool CanTransition(TrainingState to)
        {
            return _transitions.TryGetValue(State, out var set) && set.Contains(to);
        }

        public bool Transition(TrainingState to)
        {
            if (!CanTransition(to))
            {
                System.Diagnostics.Debug.WriteLine($"[FSM] 非法迁移 {State} -> {to}");
                return false;
            }
            var prev = State;
            State = to;
            StateChanged?.Invoke(prev, to);
            return true;
        }

        public void Reset() { var p = State; State = TrainingState.Idle; StateChanged?.Invoke(p, State); }
    }
}
