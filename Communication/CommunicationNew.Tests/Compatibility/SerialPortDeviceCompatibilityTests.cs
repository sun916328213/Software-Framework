using System.IO.Pipelines;
using CommunicationNew.Abstractions;
using CommunicationNew.Protocols.Serial;
using CommunicationNew.Transports;
using Xunit;

namespace CommunicationNew.Tests.Compatibility;

public sealed class SerialPortDeviceCompatibilityTests
{
    [Fact]
    public async Task Legacy_serial_device_uses_byte_stream_transport_and_connection_seam()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var device = new SerialPortDevice(new SerialParam
        {
            PortName = "COM_TEST",
            Delimiter = null,
        }, transport);
        var received = new TaskCompletionSource<byte[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        device.DataReceived += (_, args) => received.TrySetResult(args.Data);

        Assert.IsAssignableFrom<IConnection>(device);
        Assert.True(await device.ConnectAsync());
        await transport.WriteIncomingAsync(new byte[] { 0x41, 0x42 });

        Assert.Equal(
            new byte[] { 0x41, 0x42 },
            await received.Task.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.Equal(ConnectionState.Connected, ((IConnection)device).State);

        await device.DisconnectAsync();
        Assert.Equal(ConnectionState.Disconnected, ((IConnection)device).State);
    }

    private sealed class ScriptedByteStreamTransport : IByteStreamTransport
    {
        private readonly Pipe _incoming = new();
        private readonly Pipe _outgoing = new();

        public PipeReader Input => _incoming.Reader;
        public PipeWriter Output => _outgoing.Writer;
        public bool IsOpen { get; private set; }

        public ValueTask OpenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
            await _incoming.Writer.CompleteAsync();
            await _outgoing.Writer.CompleteAsync();
        }

        public ValueTask<FlushResult> WriteIncomingAsync(ReadOnlyMemory<byte> data) =>
            _incoming.Writer.WriteAsync(data);

        public async ValueTask DisposeAsync()
        {
            await CloseAsync();
            await _incoming.Reader.CompleteAsync();
            await _outgoing.Reader.CompleteAsync();
        }
    }
}
