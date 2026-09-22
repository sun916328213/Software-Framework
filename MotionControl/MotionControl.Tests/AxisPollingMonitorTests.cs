using MotionControl.Internal;
using Xunit;

namespace MotionControl.Tests;

public sealed class AxisPollingMonitorTests
{
    [Fact]
    public async Task AlarmRaised_OnlyOnRisingEdge()
    {
        var reads = 0;
        var alarms = 0;
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var monitor = new AxisPollingMonitor(
            1,
            _ => new AxisMonitorSample(123, Interlocked.Increment(ref reads) >= 2),
            (_, _) => Interlocked.Increment(ref alarms),
            (_, _) =>
            {
                if (Volatile.Read(ref reads) >= 5)
                    observed.TrySetResult();
            });

        monitor.Start(1);
        await observed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        monitor.Stop();

        Assert.Equal(1, Volatile.Read(ref alarms));
    }

    [Fact]
    public async Task PollingException_IsolatedAndNextCycleContinues()
    {
        var reads = 0;
        var errors = 0;
        var alarm = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var monitor = new AxisPollingMonitor(
            1,
            _ =>
            {
                if (Interlocked.Increment(ref reads) == 1)
                    throw new InvalidOperationException("transient");
                return new AxisMonitorSample(456, true);
            },
            (_, _) => alarm.TrySetResult(),
            errorObserved: _ => Interlocked.Increment(ref errors));

        monitor.Start(1);
        await alarm.Task.WaitAsync(TimeSpan.FromSeconds(2));
        monitor.Stop();

        Assert.True(Volatile.Read(ref reads) >= 2);
        Assert.Equal(1, Volatile.Read(ref errors));
    }

    [Fact]
    public async Task Stop_SuppressesCallbacksFromInFlightRead()
    {
        using var readStarted = new ManualResetEventSlim();
        using var releaseRead = new ManualResetEventSlim();
        var readReturned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var alarms = 0;
        var observations = 0;
        using var monitor = new AxisPollingMonitor(
            1,
            _ =>
            {
                readStarted.Set();
                releaseRead.Wait(TimeSpan.FromSeconds(2));
                readReturned.TrySetResult();
                return new AxisMonitorSample(789, true);
            },
            (_, _) => Interlocked.Increment(ref alarms),
            (_, _) => Interlocked.Increment(ref observations));

        monitor.Start(1);
        Assert.True(readStarted.Wait(TimeSpan.FromSeconds(2)));

        monitor.Stop();
        releaseRead.Set();
        await readReturned.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(25);

        Assert.Equal(0, Volatile.Read(ref alarms));
        Assert.Equal(0, Volatile.Read(ref observations));
    }
}
