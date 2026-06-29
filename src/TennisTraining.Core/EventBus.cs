using System;
using System.Collections.Generic;
using System.Threading;

namespace TennisTraining.Core
{
    /// <summary>
    /// 简单的线程安全事件总线。各模块通过它解耦通信（发布/订阅），
    /// 避免模块之间直接互相引用，便于“每个模块单独运作”。
    /// </summary>
    public sealed class EventBus : IDisposable
    {
        private readonly Dictionary<string, List<Delegate>> _handlers = new();
        private readonly object _lock = new();
        private readonly SynchronizationContext _uiContext;

        public EventBus(SynchronizationContext uiContext = null)
        {
            _uiContext = uiContext;
        }

        /// <summary>订阅事件。返回一个取消订阅的 token（调用即取消）。</summary>
        public Action Subscribe<T>(string eventName, Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            lock (_lock)
            {
                if (!_handlers.TryGetValue(eventName, out var list))
                {
                    list = new List<Delegate>();
                    _handlers[eventName] = list;
                }
                list.Add(handler);
            }
            return () => Unsubscribe(eventName, handler);
        }

        public void Unsubscribe<T>(string eventName, Action<T> handler)
        {
            lock (_lock)
            {
                if (_handlers.TryGetValue(eventName, out var list))
                    list.Remove(handler);
            }
        }

        /// <summary>发布事件。若注册了 UI 同步上下文，则切到 UI 线程触发。</summary>
        public void Publish<T>(string eventName, T payload)
        {
            List<Delegate> snapshot;
            lock (_lock)
            {
                if (!_handlers.TryGetValue(eventName, out var list)) return;
                snapshot = new List<Delegate>(list);
            }
            foreach (var d in snapshot)
            {
                if (d is Action<T> act)
                {
                    try
                    {
                        if (_uiContext != null)
                            _uiContext.Post(_ => act(payload), null);
                        else
                            act(payload);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EventBus] {eventName} 处理异常: {ex.Message}");
                    }
                }
            }
        }

        public void Publish(string eventName)
        {
            Publish<object>(eventName, null);
        }

        public int HandlerCount(string eventName)
        {
            lock (_lock)
                return _handlers.TryGetValue(eventName, out var l) ? l.Count : 0;
        }

        public void Dispose()
        {
            lock (_lock) _handlers.Clear();
        }
    }

    /// <summary>系统级事件名常量，统一管理避免拼写错误。</summary>
    public static class EventNames
    {
        public const string FrameReceived = "vision.frame";            // FrameData
        public const string BallDetected = "vision.ball.detected";     // BallDetectionResult
        public const string TrajectoryUpdated = "vision.trajectory";   // Trajectory
        public const string BallProcessed = "analysis.ball";           // ProcessedBallData
        public const string StatsUpdated = "analysis.stats";           // TrainingStats
        public const string LauncherStatus = "launcher.status";        // LauncherStatus
        public const string LauncherLaunched = "launcher.launched";    // BallParameter
        public const string StateChanged = "coord.state";              // TrainingState
        public const string Error = "system.error";                    // string
        public const string Log = "system.log";                        // string
    }
}
