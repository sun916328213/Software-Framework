using CommunicationNew.Abstractions;
using CommunicationNew.Protocols.OpcUa;
using Xunit;

namespace CommunicationNew.Tests.Compatibility;

public sealed class OpcUaServerCompatibilityTests
{
    [Fact]
    public async Task Server_adapter_exposes_registered_nodes_and_owned_lifecycle()
    {
        var adapter = new InMemoryOpcUaServerAdapter();
        await using var device = new OpcUaServerDevice(new OpcUaParam
        {
            EndpointUrl = "opc.tcp://127.0.0.1:4840/CommunicationNew",
        }, adapter);
        device.RegisterNode("ns=2;s=Value", new byte[] { 0x2A });

        Assert.IsAssignableFrom<IConnection>(device);
        Assert.True(await device.ConnectAsync());
        Assert.Equal(new byte[] { 0x2A }, await device.ReadAsync("ns=2;s=Value", 1));

        Assert.True(await device.WriteAsync("ns=2;s=Value", new byte[] { 0x2B }));
        Assert.Equal(new byte[] { 0x2B }, await device.ReadAsync("ns=2;s=Value", 1));

        await device.DisconnectAsync();

        Assert.Equal(ConnectionState.Disconnected, ((IConnection)device).State);
        Assert.Equal(1, adapter.StopCount);
    }

    private sealed class InMemoryOpcUaServerAdapter : IOpcUaServerAdapter
    {
        private readonly Dictionary<string, byte[]> _values = new();

        public bool IsRunning { get; private set; }
        public int StopCount { get; private set; }
        public IReadOnlyCollection<string> Nodes => _values.Keys.ToArray();

        public void RegisterNode(string nodeId, byte[] initialValue) =>
            _values[nodeId] = initialValue.ToArray();

        public ValueTask StartAsync(CancellationToken cancellationToken)
        {
            IsRunning = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken)
        {
            if (IsRunning)
            {
                IsRunning = false;
                StopCount++;
            }
            return ValueTask.CompletedTask;
        }

        public byte[] Read(string nodeId) => _values[nodeId].ToArray();

        public void Write(string nodeId, byte[] value) => _values[nodeId] = value.ToArray();

        public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None);
    }
}
