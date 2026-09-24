using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using CommunicationNew.Runtime;
using Xunit;

namespace CommunicationNew.Tests.Runtime;

public sealed class BoundedEventDispatcherStressTests
{
    [Fact]
    public async Task Full_queue_keeps_latest_items_in_order_and_reports_every_drop()
    {
        const int capacity = 8;
        const int publishedWhileBlocked = 10_000;
        var protocolTag = $"stress-{Guid.NewGuid():N}";
        var context = new CommunicationDiagnosticContext(protocolTag, "memory");
        var received = new ConcurrentQueue<int>();
        var handlerEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseHandler = new ManualResetEventSlim();
        long dropped = 0;
        using var meterListener = CreateDropListener(
            protocolTag,
            value => Interlocked.Add(ref dropped, value));
        meterListener.Start();

        var dispatcher = new BoundedEventDispatcher<int>(
            capacity,
            item =>
            {
                received.Enqueue(item);
                if (item == 0)
                {
                    handlerEntered.TrySetResult();
                    releaseHandler.Wait();
                }
            },
            _ => { },
            "stress");
        Assert.True(dispatcher.TryPublish(0, context));
        await handlerEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));

        for (var item = 1; item <= publishedWhileBlocked; item++)
        {
            Assert.True(dispatcher.TryPublish(item, context));
        }

        releaseHandler.Set();
        await dispatcher.DisposeAsync();

        Assert.Equal(
            new[] { 0 }.Concat(Enumerable.Range(
                publishedWhileBlocked - capacity + 1,
                capacity)),
            received);
        Assert.Equal(publishedWhileBlocked - capacity, Volatile.Read(ref dropped));
        Assert.False(dispatcher.TryPublish(publishedWhileBlocked + 1, context));
    }

    private static MeterListener CreateDropListener(
        string protocolTag,
        Action<long> recordDrop)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, currentListener) =>
            {
                if (instrument.Meter.Name == CommunicationDiagnostics.MeterName &&
                    instrument.Name == "communication.events.dropped")
                {
                    currentListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "protocol" &&
                    string.Equals(tag.Value?.ToString(), protocolTag, StringComparison.Ordinal))
                {
                    recordDrop(value);
                    break;
                }
            }
        });
        return listener;
    }
}
