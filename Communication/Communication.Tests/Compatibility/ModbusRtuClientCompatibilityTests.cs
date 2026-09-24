using System.Buffers;
using System.IO.Pipelines;
using CommunicationNew.Abstractions;
using CommunicationNew.Protocols.Modbus.ModbusRtu;
using CommunicationNew.Transports;
using Xunit;

namespace CommunicationNew.Tests.Compatibility;

public sealed class ModbusRtuClientCompatibilityTests
{
    [Fact]
    public async Task Legacy_read_uses_serial_transport_codec_and_connection_session()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var device = new ModbusRtuClientDevice(new ModbusRtuParam
        {
            PortName = "COM_TEST",
            SlaveId = 1,
            TimeoutMs = 1000,
            RetryCount = 1,
        }, transport);

        Assert.IsAssignableFrom<IConnection>(device);
        Assert.True(await device.ConnectAsync());
        var readTask = device.ReadAsync("40001", 2);

        Assert.Equal(
            new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x02, 0xC4, 0x0B },
            await transport.ReadRequestAsync(8));
        await transport.WriteResponseAsync(
            new byte[] { 0x01, 0x03, 0x04, 0x00, 0x2A, 0x00, 0x2B, 0x9B, 0xE4 });

        Assert.Equal(new byte[] { 0x00, 0x2A, 0x00, 0x2B }, await readTask);
        Assert.Equal(ConnectionState.Connected, ((IConnection)device).State);
    }

    private sealed class ScriptedByteStreamTransport : IByteStreamTransport
    {
        private readonly Pipe _requests = new();
        private readonly Pipe _responses = new();

        public PipeReader Input => _responses.Reader;
        public PipeWriter Output => _requests.Writer;
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
            await _requests.Writer.CompleteAsync();
            await _responses.Writer.CompleteAsync();
        }

        public async ValueTask<byte[]> ReadRequestAsync(int length)
        {
            var read = await _requests.Reader.ReadAsync();
            try
            {
                return read.Buffer.Slice(0, length).ToArray();
            }
            finally
            {
                _requests.Reader.AdvanceTo(read.Buffer.GetPosition(length));
            }
        }

        public ValueTask<FlushResult> WriteResponseAsync(ReadOnlyMemory<byte> response) =>
            _responses.Writer.WriteAsync(response);

        public async ValueTask DisposeAsync()
        {
            await CloseAsync();
            await _requests.Reader.CompleteAsync();
            await _responses.Reader.CompleteAsync();
        }
    }
}
