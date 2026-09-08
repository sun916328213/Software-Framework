using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using CommunicationNew.Runtime;
using Xunit;

namespace CommunicationNew.Tests.Runtime;

public sealed class LatestValueDispatcherTests
{
    [Fact]
    public async Task Blocked_consumer_receives_only_the_latest_pending_value_for_each_key()
    {
        long coalesced = 0;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == CommunicationDiagnostics.MeterName &&
                    instrument.Name == "communication.events.coalesced")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "protocol" && Equals(tag.Value, "test"))
                {
                    Interlocked.Add(ref coalesced, value);
                    break;
                }
            }
        });
        listener.Start();

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delivered = new ConcurrentQueue<int>();
        await using var dispatcher = new LatestValueDispatcher<string, int>(
            capacity: 8,
            item =>
            {
                delivered.Enqueue(item);
                if (item == 1)
                {
                    entered.TrySetResult();
                    release.Task.GetAwaiter().GetResult();
                }
            },
            _ => { },
            "data_point_changed",
            new CommunicationDiagnosticContext("test", "memory"));

        Assert.True(dispatcher.TryPublish("temperature", 1));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        for (var value = 2; value <= 10_000; value++)
        {
            Assert.True(dispatcher.TryPublish("temperature", value));
        }

        release.SetResult();
        await dispatcher.DisposeAsync();

        Assert.Equal(new[] { 1, 10_000 }, delivered);
        Assert.Equal(9_998, Volatile.Read(ref coalesced));
    }
}
