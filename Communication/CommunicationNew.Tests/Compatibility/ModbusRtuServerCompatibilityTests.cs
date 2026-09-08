using CommunicationNew.Abstractions;
using CommunicationNew.Protocols.Modbus.ModbusRtu;
using Xunit;

namespace CommunicationNew.Tests.Compatibility;

public sealed class ModbusRtuServerCompatibilityTests
{
    [Fact]
    public async Task Server_adapter_preserves_read_write_and_owned_lifecycle()
    {
        var adapter = new ScriptedModbusRtuServerAdapter
        {
            ReadResult = new byte[] { 0x00, 0x2A },
        };
        await using var device = new ModbusRtuServerDevice(new ModbusRtuParam
        {
            PortName = "COM_TEST",
            SlaveId = 1,
        }, adapter);

        Assert.IsAssignableFrom<IConnection>(device);
        Assert.True(await device.ConnectAsync());
        Assert.Equal(ConnectionState.Connected, ((IConnection)device).State);
        Assert.Equal(new byte[] { 0x00, 0x2A }, await device.ReadAsync("40001", 1));

        Assert.True(await device.WriteAsync("40002", new byte[] { 0x00, 0x2B }));
        Assert.Equal(("40002", new byte[] { 0x00, 0x2B }), adapter.LastWrite);

        await device.DisconnectAsync();

        Assert.Equal(ConnectionState.Disconnected, ((IConnection)device).State);
        Assert.Equal(1, adapter.StopCount);
    }

    private sealed class ScriptedModbusRtuServerAdapter : IModbusRtuServerAdapter
    {
        public bool IsRunning { get; private set; }
        public int StopCount { get; private set; }
        public byte[] ReadResult { get; init; } = Array.Empty<byte>();
        public (string Address, byte[] Data)? LastWrite { get; private set; }

        public void Start() => IsRunning = true;

        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;
            StopCount++;
        }

        public byte[] Read(string address, int length) => ReadResult;

        public void Write(string address, byte[] data) => LastWrite = (address, data.ToArray());

        public void Dispose() => Stop();
    }
}
