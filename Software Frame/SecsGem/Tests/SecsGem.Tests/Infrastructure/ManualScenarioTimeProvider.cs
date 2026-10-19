namespace SecsGem.Tests.Infrastructure;

/// <summary>业务会话测试的单次手动时钟；不依赖真实等待或额外 NuGet 包。</summary>
internal sealed class ManualScenarioTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private readonly HashSet<ClockTimer> _timers = new();
    private long _ticks;
    public override DateTimeOffset GetUtcNow() { lock (_gate) return DateTimeOffset.UnixEpoch.AddTicks(_ticks); }
    public override long GetTimestamp() { lock (_gate) return _ticks; }
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ClockTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    internal void Advance(TimeSpan time)
    {
        ClockTimer[] ready;
        lock (_gate)
        {
            _ticks += time.Ticks;
            ready = _timers.Where(timer => timer.Due <= _ticks).ToArray();
            foreach (var timer in ready) timer.Due = long.MaxValue;
        }
        foreach (var timer in ready) timer.Callback(timer.State);
    }

    private sealed class ClockTimer(ManualScenarioTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        internal readonly TimerCallback Callback = callback;
        internal readonly object? State = state;
        internal long Due = long.MaxValue;
        private bool _disposed;
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (period != Timeout.InfiniteTimeSpan) throw new NotSupportedException("此测试时钟只支持单次计时。");
            lock (clock._gate)
            {
                if (_disposed) return false;
                Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._ticks + dueTime.Ticks;
                clock._timers.Add(this);
                return true;
            }
        }
        public void Dispose() { lock (clock._gate) { _disposed = true; clock._timers.Remove(this); } }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
