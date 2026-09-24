using System.Diagnostics.Metrics;
using System.Threading.Channels;
using CommunicationNew.Abstractions;
using CommunicationNew.Runtime;
using Xunit;

namespace CommunicationNew.Tests.Runtime;

public sealed class BoundedReceiveInboxTests
{
    [Fact]
    public async Task Packet_received_before_consumer_starts_is_preserved()
    {
        using var inbox = new BoundedReceiveInbox(2, ReceiveOverflowPolicy.Reject);
        var context = new CommunicationDiagnosticContext("receive-test", "memory");
        inbox.BeginGeneration();

        inbox.Publish(new ReceivedPacket([0x01], "peer"), context);

        var packet = await inbox.ReceiveAsync();
        Assert.Equal(new byte[] { 0x01 }, packet.Data.ToArray());
        Assert.Equal("peer", packet.RemoteAddress);
    }

    [Fact]
    public async Task Multiple_consumers_do_not_receive_the_same_packet()
    {
        using var inbox = new BoundedReceiveInbox(2, ReceiveOverflowPolicy.Reject);
        var context = new CommunicationDiagnosticContext("receive-test", "memory");
        inbox.BeginGeneration();
        var firstConsumer = inbox.ReceiveAsync().AsTask();
        var secondConsumer = inbox.ReceiveAsync().AsTask();

        inbox.Publish(new ReceivedPacket([0x01]), context);
        inbox.Publish(new ReceivedPacket([0x02]), context);

        var values = new[]
        {
            (await firstConsumer).Data.Span[0],
            (await secondConsumer).Data.Span[0],
        };
        Assert.Equal(new byte[] { 0x01, 0x02 }, values.OrderBy(value => value));
    }

    [Fact]
    public async Task Ending_generation_releases_pending_consumer()
    {
        using var inbox = new BoundedReceiveInbox(1, ReceiveOverflowPolicy.Reject);
        var generation = inbox.BeginGeneration();
        var pending = inbox.ReceiveAsync().AsTask();

        inbox.EndGeneration(generation);

        await Assert.ThrowsAsync<ChannelClosedException>(() => pending);
    }

    [Fact]
    public async Task Caller_cancellation_releases_pending_consumer()
    {
        using var inbox = new BoundedReceiveInbox(1, ReceiveOverflowPolicy.Reject);
        inbox.BeginGeneration();
        using var cancellation = new CancellationTokenSource();
        var pending = inbox.ReceiveAsync(cancellation.Token).AsTask();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task Dispose_releases_pending_consumer()
    {
        var inbox = new BoundedReceiveInbox(1, ReceiveOverflowPolicy.Reject);
        inbox.BeginGeneration();
        var pending = inbox.ReceiveAsync().AsTask();

        inbox.Dispose();

        await Assert.ThrowsAsync<ChannelClosedException>(() => pending);
    }

    [Fact]
    public async Task Late_old_generation_end_cannot_close_reconnected_inbox()
    {
        using var inbox = new BoundedReceiveInbox(1, ReceiveOverflowPolicy.Reject);
        var context = new CommunicationDiagnosticContext("receive-test", "memory");
        var oldGeneration = inbox.BeginGeneration();
        var currentGeneration = inbox.BeginGeneration();

        inbox.EndGeneration(oldGeneration);
        inbox.Publish(new ReceivedPacket([0x2A]), context);

        Assert.Equal(0x2A, (await inbox.ReceiveAsync()).Data.Span[0]);
        inbox.EndGeneration(currentGeneration);
    }

    [Fact]
    public void Reliable_stream_rejects_full_inbox_instead_of_dropping_bytes()
    {
        using var inbox = new BoundedReceiveInbox(1, ReceiveOverflowPolicy.Reject);
        var context = new CommunicationDiagnosticContext("receive-test", "memory");
        inbox.BeginGeneration();
        inbox.Publish(new ReceivedPacket([0x01]), context);

        var exception = Assert.Throws<ReceiveQueueFullException>(
            () => inbox.Publish(new ReceivedPacket([0x02]), context));

        Assert.Equal(1, exception.Capacity);
    }

    [Fact]
    public async Task Datagram_inbox_keeps_latest_packets_and_reports_drop()
    {
        var protocolTag = $"receive-{Guid.NewGuid():N}";
        var context = new CommunicationDiagnosticContext(protocolTag, "udp");
        long dropped = 0;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, currentListener) =>
            {
                if (instrument.Meter.Name == CommunicationDiagnostics.MeterName &&
                    instrument.Name == "communication.receive.dropped")
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
                    Interlocked.Add(ref dropped, value);
                    break;
                }
            }
        });
        listener.Start();

        using var inbox = new BoundedReceiveInbox(2, ReceiveOverflowPolicy.DropOldest);
        inbox.BeginGeneration();
        inbox.Publish(new ReceivedPacket([0x01]), context);
        inbox.Publish(new ReceivedPacket([0x02]), context);
        inbox.Publish(new ReceivedPacket([0x03]), context);

        Assert.Equal(0x02, (await inbox.ReceiveAsync()).Data.Span[0]);
        Assert.Equal(0x03, (await inbox.ReceiveAsync()).Data.Span[0]);
        Assert.Equal(1, Volatile.Read(ref dropped));
    }
}
