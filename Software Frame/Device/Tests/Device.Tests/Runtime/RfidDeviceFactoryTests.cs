// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：RfidDeviceFactoryTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices、Device.Core.Rfid、Device.Runtime.Rfid、Xunit。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;
using Device.Core.Rfid;
using Device.Runtime.Rfid;
using Xunit;

namespace Device.Tests.Runtime;

/// <summary>
/// 验证统一 RFID 工厂和共享分页约束。
/// </summary>
public sealed class RfidDeviceFactoryTests
{
    private static readonly string[] ExpectedDriverIds =
    [
        "rfid.ck.s650.modbus-rtu",
        "rfid.xinuo.xk15a-r01.serial",
    ];

    /// <summary>工厂应公开晨控和椹诺两种 RFID 驱动。</summary>
    [Fact]
    public void AvailableDrivers_ContainsAllSupportedBrands()
    {
        var factory = new RfidDeviceFactory();

        Assert.Equal(ExpectedDriverIds, factory.AvailableDrivers.Select(x => x.Id.Value));
        Assert.All(factory.AvailableDrivers, descriptor =>
            Assert.Equal(DeviceCategory.Rfid, descriptor.Category));
    }

    /// <summary>两个品牌都应由同一个工厂创建且不自动连接串口。</summary>
    /// <param name="driverId">待验证的稳定驱动标识。</param>
    [Theory]
    [InlineData("rfid.ck.s650.modbus-rtu")]
    [InlineData("rfid.xinuo.xk15a-r01.serial")]
    public async Task Create_KnownDriver_RoutesThroughUnifiedFactory(string driverId)
    {
        var options = ValidOptions(driverId);

        await using var device = new RfidDeviceFactory().Create(options);

        Assert.Equal(options, device.Options);
        Assert.Equal(DeviceState.Disconnected, device.State);
    }

    /// <summary>未连接时读取应返回稳定错误，不能访问真实串口。</summary>
    [Fact]
    public async Task ReadPages_WhenDisconnected_ReturnsNotConnected()
    {
        await using var device = new RfidDeviceFactory().Create(ValidOptions(ExpectedDriverIds[0]));

        var result = await device.ReadPagesAsync(1, 2);

        Assert.False(result.Success);
        Assert.Equal("rfid.ck.s650.not_connected", result.ErrorCode);
    }

    /// <summary>未连接时写入应返回稳定错误，不能访问真实串口。</summary>
    [Fact]
    public async Task WritePages_WhenDisconnected_ReturnsNotConnected()
    {
        await using var device = new RfidDeviceFactory().Create(ValidOptions(ExpectedDriverIds[1]));

        var result = await device.WritePagesAsync(1, ["ABC"]);

        Assert.False(result.Success);
        Assert.Equal("rfid.xinuo.xk15a-r01.not_connected", result.ErrorCode);
    }

    /// <summary>未知驱动异常应列出全部已安装驱动。</summary>
    [Fact]
    public void Create_UnknownDriver_ReportsAllInstalledDrivers()
    {
        var factory = new RfidDeviceFactory();

        var exception = Assert.Throws<RfidDriverNotFoundException>(() =>
            factory.Create(ValidOptions("rfid.unknown.serial")));

        foreach (var driverId in ExpectedDriverIds)
            Assert.Contains(driverId, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 ValidOptions；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    private static RfidOptions ValidOptions(string driverId) => new()
    {
        Name = "Rfid01",
        DriverId = new DeviceDriverId(driverId),
        PortName = "COM3",
    };
}
