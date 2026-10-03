// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FluentModbusServerDataAccessorTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Runtime.Protocols.Modbus、FluentModbus、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Runtime.Protocols.Modbus;
using FluentModbus;
using Xunit;

namespace Communication.Tests.Protocols.Modbus;

/// <summary>
/// FluentModbusServerDataAccessorTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class FluentModbusServerDataAccessorTests
{
    /// <summary>
    /// 验证 Accessor_round_trips_registers_and_unaligned_coils 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Write、Equal、Read；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Accessor_waits_for_the_fluent_modbus_data_lock 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Run、Set、Wait、True、FromSeconds；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </summary>
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

    /// <summary>
    /// 验证 Accessor_rejects_writes_to_read_only_regions 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Throws、Write；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Accessor_rejects_writes_to_read_only_regions()
    {
        using var server = new ModbusTcpServer();

        Assert.Throws<NotSupportedException>(() =>
            FluentModbusServerDataAccessor.Write(
                server, unitId: 0, "IR1", new byte[] { 0x00, 0x01 }));
    }
}
