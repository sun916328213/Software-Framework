using CommunicationNew.Abstractions;
using CommunicationNew.Protocols.OpcUa;
using Xunit;

namespace CommunicationNew.Tests.Compatibility;

public sealed class OpcUaClientCompatibilityTests
{
    [Fact]
    public async Task Client_adapter_performs_real_session_operations_through_legacy_surface()
    {
        var adapter = new ScriptedOpcUaClientAdapter
        {
            ReadResult = new byte[] { 0x2A },
        };
        await using var device = new OpcUaClientDevice(new OpcUaParam
        {
            EndpointUrl = "opc.tcp://127.0.0.1:4840",
        }, adapter);

        Assert.IsAssignableFrom<IConnection>(device);
        Assert.True(await device.ConnectAsync());
        Assert.Equal(new byte[] { 0x2A }, await device.ReadAsync("ns=2;s=Value", 1));
        Assert.True(await device.WriteAsync("ns=2;s=Value", new byte[] { 0x2B }));
        Assert.Equal(("ns=2;s=Value", new byte[] { 0x2B }), adapter.LastWrite);

        await device.DisconnectAsync();

        Assert.Equal(ConnectionState.Disconnected, ((IConnection)device).State);
        Assert.Equal(1, adapter.DisconnectCount);
    }

    private sealed class ScriptedOpcUaClientAdapter : IOpcUaClientAdapter
    {
        public bool IsConnected { get; private set; }
        public int DisconnectCount { get; private set; }
        public byte[] ReadResult { get; init; } = Array.Empty<byte>();
        public (string NodeId, byte[] Value)? LastWrite { get; private set; }

        public ValueTask ConnectAsync(CancellationToken cancellationToken)
        {
            IsConnected = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisconnectAsync(CancellationToken cancellationToken)
        {
            if (IsConnected)
            {
                IsConnected = false;
                DisconnectCount++;
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask<byte[]> ReadAsync(string nodeId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ReadResult);

        public ValueTask WriteAsync(
            string nodeId,
            byte[] value,
            CancellationToken cancellationToken)
        {
            LastWrite = (nodeId, value.ToArray());
            return ValueTask.CompletedTask;
        }

        public async ValueTask DisposeAsync() => await DisconnectAsync(CancellationToken.None);
    }
}
