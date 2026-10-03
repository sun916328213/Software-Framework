// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：RfidContractTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices、Device.Core.Rfid、Xunit。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;
using Device.Core.Rfid;
using Xunit;

namespace Device.Tests.Core;

/// <summary>
/// 验证 RFID 公共配置约束。
/// </summary>
public sealed class RfidContractTests
{
    /// <summary>无效串口和分页配置应一次返回全部问题。</summary>
    [Fact]
    public void Validate_InvalidConfiguration_ReturnsAllErrors()
    {
        var options = new RfidOptions
        {
            Name = "",
            DriverId = default,
            PortName = "USB1",
            BaudRate = 0,
            DataBits = 4,
            StopBits = 3,
            Parity = "invalid",
            SlaveId = 0,
            PageSizeBytes = 16,
            TimeoutMilliseconds = 0,
        };

        var exception = Assert.Throws<RfidConfigurationException>(() =>
            RfidOptionsValidator.Validate(options));

        Assert.Equal(10, exception.Errors.Count);
    }

    /// <summary>串口名为空时应直接提示名称缺失，而不是报告串口格式错误。</summary>
    [Fact]
    public void Validate_BlankPortName_ReportsMissingPortName()
    {
        var exception = Assert.Throws<RfidConfigurationException>(() =>
            RfidOptionsValidator.Validate(new RfidOptions
            {
                Name = "Rfid01",
                DriverId = new DeviceDriverId("rfid.fake.serial"),
                PortName = "   ",
            }));

        var error = Assert.Single(exception.Errors);
        Assert.Contains("串口名称不能为空", error, StringComparison.Ordinal);
    }

    /// <summary>串口名称和校验方式应忽略大小写。</summary>
    [Fact]
    public void Validate_CaseInsensitiveSerialValues_AreAccepted()
    {
        RfidOptionsValidator.Validate(new RfidOptions
        {
            Name = "Rfid01",
            DriverId = new DeviceDriverId("rfid.fake.serial"),
            PortName = "com3",
            Parity = "NONE",
        });
    }
}
