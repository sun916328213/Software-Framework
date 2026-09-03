using System.Buffers;
using System.IO.Pipelines;
using CommunicationNew.Abstractions;
using CommunicationNew.Runtime;
using CommunicationNew.Transports;
using Xunit;

namespace CommunicationNew.Tests.Runtime;

public sealed class ConnectionSessionTests
{
    [Fact]
    public async Task Connected_session_executes_a_request_and_stops_accepting_requests_after_disconnect()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1));

        await session.ConnectAsync();
        Assert.Equal(ConnectionState.Connected, session.State);

        var responseTask = session.ExecuteAsync(0x2A).AsTask();
        Assert.Equal(0x2A, await transport.ReadRequestAsync());

        await transport.WriteResponseAsync(0x7B);
        Assert.Equal(0x7B, await responseTask);

        await session.DisconnectAsync();
        Assert.Equal(ConnectionState.Disconnected, session.State);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.ExecuteAsync(0x01).AsTask());
    }

    [Fact]
    public async Task Operation_timeout_is_reported_separately_from_caller_cancellation()
    {
        var transport = new ScriptedByteStreamTransport();
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromMilliseconds(20));
        await session.ConnectAsync();

        await Assert.ThrowsAsync<TimeoutException>(() => session.ExecuteAsync(0x2A).AsTask());

        using var callerCts = new CancellationTokenSource();
        callerCts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.ExecuteAsync(0x2A, callerCts.Token).AsTask());
    }

    [Fact]
    public async Task Concurrent_connect_and_repeated_disconnect_are_serialized_and_idempotent()
    {
        var transport = new ScriptedByteStreamTransport();
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1));

        await Task.WhenAll(
            session.ConnectAsync().AsTask(),
            session.ConnectAsync().AsTask());
        Assert.Equal(ConnectionState.Connected, session.State);

        await session.DisconnectAsync();
        await session.DisconnectAsync();
        Assert.Equal(ConnectionState.Disconnected, session.State);
    }

    [Fact]
    public async Task Failed_transport_close_still_leaves_session_disconnected()
    {
        var transport = new ScriptedByteStreamTransport { FailNextClose = true };
        await using var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1));
        await session.ConnectAsync();

        await Assert.ThrowsAsync<IOException>(() => session.DisconnectAsync().AsTask());

        Assert.Equal(ConnectionState.Disconnected, session.State);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.ExecuteAsync(0x01).AsTask());
    }

    [Fact]
    public async Task Dispose_releases_transport_even_when_disconnect_fails()
    {
        var transport = new ScriptedByteStreamTransport { FailNextClose = true };
        var session = new ConnectionSession<byte, byte>(
            transport,
            new SingleByteCodec(),
            TimeSpan.FromSeconds(1));
        await session.ConnectAsync();

        await Assert.ThrowsAsync<IOException>(() => session.DisposeAsync().AsTask());

        Assert.True(transport.IsDisposed);
        Assert.Equal(ConnectionState.Disposed, session.State);
    }

    private sealed class SingleByteCodec : IRequestCodec<byte, byte>
    {
        public ReadOnlyMemory<byte> Encode(byte request, ushort correlationId)
        {
            return new byte[] { (byte)(correlationId >> 8), (byte)correlationId, request };
        }

        public bool TryDecode(
            ReadOnlySequence<byte> buffer,
            ushort correlationId,
            out byte response,
            out SequencePosition consumed)
        {
            response = default;
            consumed = buffer.Start;
            if (buffer.Length < 3)
            {
                return false;
            }

            Span<byte> frame = stackalloc byte[3];
            buffer.Slice(0, 3).CopyTo(frame);
            var actualCorrelationId = (ushort)((frame[0] << 8) | frame[1]);
            if (actualCorrelationId != correlationId)
            {
                throw new InvalidDataException("Unexpected correlation id.");
            }

            response = frame[2];
            consumed = buffer.GetPosition(3);
            return true;
        }
    }

    private sealed class ScriptedByteStreamTransport : IByteStreamTransport
    {
        private readonly Pipe _requests = new();
        private readonly Pipe _responses = new();

        public PipeReader Input => _responses.Reader;
        public PipeWriter Output => _requests.Writer;
        public bool IsOpen { get; private set; }
        public bool FailNextClose { get; set; }
        public bool IsDisposed { get; private set; }

        public ValueTask OpenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsOpen)
            {
                throw new InvalidOperationException("Transport was opened twice.");
            }

            IsOpen = true;
            return ValueTask.CompletedTask;
        }

        public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            if (FailNextClose)
            {
                FailNextClose = false;
                throw new IOException("Scripted close failure.");
            }
            await _requests.Writer.CompleteAsync();
            await _responses.Writer.CompleteAsync();
        }

        public async ValueTask<byte> ReadRequestAsync()
        {
            var read = await _requests.Reader.ReadAsync();
            try
            {
                var frame = read.Buffer.Slice(0, 3).ToArray();
                return frame[2];
            }
            finally
            {
                _requests.Reader.AdvanceTo(read.Buffer.GetPosition(3));
            }
        }

        public async ValueTask WriteResponseAsync(byte response)
        {
            await _responses.Writer.WriteAsync(new byte[] { 0, 1, response });
        }

        public async ValueTask DisposeAsync()
        {
            await CloseAsync();
            await _requests.Reader.CompleteAsync();
            await _responses.Reader.CompleteAsync();
            IsDisposed = true;
        }
    }
}
