using CommunicationNew.Protocols.Modbus;
using FluentModbus;
using Xunit;

namespace CommunicationNew.Tests.Protocols.Modbus;

public sealed class FluentModbusServerDataAccessorTests
{
    [Fact]
    public void Accessor_round_trips_registers_and_unaligned_coils()
    {
        using var server = new ModbusTcpServer();

        FluentModbusServerDataAccessor.Write(
            server, unitId: 0, "HR2", new byte[] { 0x12, 0x34, 0xFE, 0xDC });
        FluentModbusServerDataAccessor.Write(
            server, unitId: 0, "C4", new byte[] { 0xA5 });

        Assert.Equal(
            new byte[] { 0x12, 0x34, 0xFE, 0xDC },
            FluentModbusServerDataAccessor.Read(server, unitId: 0, "HR2", length: 2));
        Assert.Equal(
            new byte[] { 0xA5 },
            FluentModbusServerDataAccessor.Read(server, unitId: 0, "C4", length: 8));
    }

    [Fact]
    public async Task Accessor_waits_for_the_fluent_modbus_data_lock()
    {
        using var server = new ModbusTcpServer();
        using var lockHeld = new ManualResetEventSlim();
        using var releaseLock = new ManualResetEventSlim();
        var lockOwner = Task.Run(() =>
        {
            lock (server.Lock)
            {
                lockHeld.Set();
                releaseLock.Wait();
            }
        });
        Assert.True(lockHeld.Wait(TimeSpan.FromSeconds(2)));

        var readTask = Task.Run(() =>
            FluentModbusServerDataAccessor.Read(server, unitId: 0, "HR1", length: 1));
        try
        {
            await Task.Delay(50);
            Assert.False(readTask.IsCompleted);
        }
        finally
        {
            releaseLock.Set();
        }

        await Task.WhenAll(lockOwner, readTask);
    }

    [Fact]
    public void Accessor_rejects_writes_to_read_only_regions()
    {
        using var server = new ModbusTcpServer();

        Assert.Throws<NotSupportedException>(() =>
            FluentModbusServerDataAccessor.Write(
                server, unitId: 0, "IR1", new byte[] { 0x00, 0x01 }));
    }
}
