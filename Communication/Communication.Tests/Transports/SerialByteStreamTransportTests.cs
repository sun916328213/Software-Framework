using System.Buffers;
using System.IO.Pipelines;
using CommunicationNew.Transports;
using Xunit;

namespace CommunicationNew.Tests.Transports;

public sealed class SerialByteStreamTransportTests
{
    [Fact]
    public async Task Repeated_open_is_idempotent()
    {
        var connection = new LoopbackSerialConnection();
        await using var transport = new SerialByteStreamTransport(
            new SerialTransportOptions("COM_TEST", 9600),
            connection);

        await transport.OpenAsync();
        await transport.OpenAsync();

        Assert.True(transport.IsOpen);
        Assert.Equal(1, connection.OpenCount);
    }

    [Fact]
    public async Task Open_transport_exposes_duplex_stream_and_close_is_idempotent()
    {
        var connection = new LoopbackSerialConnection();
        await using var transport = new SerialByteStreamTransport(
            new SerialTransportOptions("COM_TEST", 9600),
            connection);

        await transport.OpenAsync();
        await transport.Output.WriteAsync(new byte[] { 0x31, 0x32 });
        var read = await transport.Input.ReadAsync();
        var payload = read.Buffer.ToArray();
        transport.Input.AdvanceTo(read.Buffer.End);

        Assert.Equal(new byte[] { 0x31, 0x32 }, payload);
        Assert.True(transport.IsOpen);
        Assert.Equal(1, connection.OpenCount);

        await transport.CloseAsync();
        await transport.CloseAsync();
        Assert.False(transport.IsOpen);
        Assert.Equal(1, connection.CloseCount);
    }

    [Fact]
    public async Task Concurrent_dispose_calls_release_the_serial_connection_once()
    {
        var connection = new LoopbackSerialConnection();
        var transport = new SerialByteStreamTransport(
            new SerialTransportOptions("COM_TEST", 9600),
            connection);
        await transport.OpenAsync();
        connection.PauseNextClose();

        var firstDispose = Task.Run(async () => await transport.DisposeAsync());
        await connection.WaitForCloseStartedAsync().WaitAsync(TimeSpan.FromSeconds(1));
        var secondCallReturned = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondDispose = Task.Run(async () =>
        {
            var operation = transport.DisposeAsync();
            secondCallReturned.TrySetResult();
            await operation;
        });

        await secondCallReturned.Task.WaitAsync(TimeSpan.FromSeconds(1));
        connection.ResumeClose();
        await Task.WhenAll(firstDispose, secondDispose);

        Assert.Equal(1, connection.CloseCount);
        Assert.Equal(1, connection.DisposeCount);
        Assert.False(transport.IsOpen);
    }

    [Fact]
    public async Task Open_started_during_dispose_is_rejected_without_reopening_the_port()
    {
        var connection = new LoopbackSerialConnection();
        var transport = new SerialByteStreamTransport(
            new SerialTransportOptions("COM_TEST", 9600),
            connection);
        await transport.OpenAsync();
        connection.PauseNextClose();

        var disposeTask = Task.Run(async () => await transport.DisposeAsync());
        await connection.WaitForCloseStartedAsync().WaitAsync(TimeSpan.FromSeconds(1));
        var reopenTask = transport.OpenAsync().AsTask();
        connection.ResumeClose();

        await disposeTask;
        await Assert.ThrowsAsync<ObjectDisposedException>(() => reopenTask);
        Assert.Equal(1, connection.OpenCount);
        Assert.False(transport.IsOpen);
    }

    [Fact]
    public async Task Pending_read_observes_caller_cancellation()
    {
        var connection = new LoopbackSerialConnection();
        await using var transport = new SerialByteStreamTransport(
            new SerialTransportOptions("COM_TEST", 9600),
            connection);
        await transport.OpenAsync();
        using var cancellation = new CancellationTokenSource();

        // 测试串口不写入数据，挂起读取应由同一个调用方 token 终止。
        var readTask = transport.Input.ReadAsync(cancellation.Token).AsTask();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => readTask);
    }

    private sealed class LoopbackSerialConnection : ISerialPortConnection
    {
        private readonly LoopbackStream _stream = new();
        private readonly TaskCompletionSource _closeStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim _resumeClose = new(initialState: false);
        private bool _pauseNextClose;

        public bool IsOpen { get; private set; }
        public Stream Stream => _stream;
        public int OpenCount { get; private set; }
        public int CloseCount { get; private set; }
        public int DisposeCount { get; private set; }

        public void Open()
        {
            OpenCount++;
            IsOpen = true;
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            if (_pauseNextClose)
            {
                _pauseNextClose = false;
                _closeStarted.TrySetResult();
                _resumeClose.Wait();
            }

            CloseCount++;
            IsOpen = false;
        }

        public void Dispose()
        {
            DisposeCount++;
            Close();
            _stream.Dispose();
            _resumeClose.Dispose();
        }

        public void PauseNextClose() => _pauseNextClose = true;

        public Task WaitForCloseStartedAsync() => _closeStarted.Task;

        public void ResumeClose() => _resumeClose.Set();
    }

    private sealed class LoopbackStream : Stream
    {
        private readonly Pipe _pipe = new();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            ReadCoreAsync(buffer, cancellationToken);

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            await _pipe.Writer.WriteAsync(buffer, cancellationToken);
        }

        private async ValueTask<int> ReadCoreAsync(
            Memory<byte> destination,
            CancellationToken cancellationToken)
        {
            var result = await _pipe.Reader.ReadAsync(cancellationToken);
            var length = (int)Math.Min(result.Buffer.Length, destination.Length);
            result.Buffer.Slice(0, length).CopyTo(destination.Span);
            _pipe.Reader.AdvanceTo(result.Buffer.GetPosition(length));
            return length;
        }

        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pipe.Reader.Complete();
                _pipe.Writer.Complete();
            }
            base.Dispose(disposing);
        }
    }
}
