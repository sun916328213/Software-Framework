// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：CameraContractTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Text.Json、Device.Core.Cameras、Device.Core.Devices、Xunit。导入不代表所有类型都会在运行时加载。
using System.Text.Json;
using Device.Core.Cameras;
using Device.Core.Devices;
using Xunit;

namespace Device.Tests.Core;

/// <summary>
/// CameraContractTests 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class CameraContractTests
{
    private static readonly DeviceDriverId DriverId = new("camera.fake.gige");

    /// <summary>
    /// 测试相关入口 DriverId_JsonRoundTrip_UsesStableStringValue；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ValidOptions、Serialize、Deserialize、Contains、NotNull；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DriverId_JsonRoundTrip_UsesStableStringValue()
    {
        var options = ValidOptions("Camera01");

        var json = JsonSerializer.Serialize(options);
        var restored = JsonSerializer.Deserialize<CameraOptions>(json);

        Assert.Contains("\"DriverId\":\"camera.fake.gige\"", json);
        Assert.Contains("\"ScanType\":\"AreaScan\"", json);
        Assert.Contains("\"Mode\":\"On\"", json);
        Assert.NotNull(restored);
        Assert.Equal(DriverId, restored.DriverId);
        Assert.Equal(CameraScanType.AreaScan, restored.ScanType);
        Assert.Equal(CameraTriggerMode.On, restored.FrameTrigger.Mode);
        Assert.Equal(CameraTriggerSources.Software, restored.FrameTrigger.Source);
    }

    /// <summary>
    /// 测试相关入口 Create_InvalidConfiguration_ReturnsAllValidationErrors；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、Validate、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Create_InvalidConfiguration_ReturnsAllValidationErrors()
    {
        var options = new CameraOptions
        {
            Name = "",
            DriverId = default,
            IpAddress = "not-an-ip",
            ExposureMilliseconds = 0,
            Gain = -1,
            TimeoutMilliseconds = 0,
        };

        var exception = Assert.Throws<CameraConfigurationException>(() => CameraOptionsValidator.Validate(options));

        Assert.Equal(6, exception.Errors.Count);
    }

    /// <summary>
    /// 测试相关入口 Validate_LineScanWithFrameAndLineTriggers_IsAccepted；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ValidOptions、Validate；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Validate_LineScanWithFrameAndLineTriggers_IsAccepted()
    {
        var options = ValidOptions("LineCamera01") with
        {
            ScanType = CameraScanType.LineScan,
            FrameTrigger = new CameraTriggerSettings
            {
                Mode = CameraTriggerMode.On,
                Source = CameraTriggerSources.Line1,
            },
            LineTrigger = new CameraTriggerSettings
            {
                Mode = CameraTriggerMode.On,
                Source = CameraTriggerSources.RotaryEncoder1,
            },
        };

        CameraOptionsValidator.Validate(options);
    }

    /// <summary>
    /// 测试相关入口 Validate_InvalidScanTriggerAndParameterFile_ReturnsAllErrors；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Combine、GetTempPath、NewGuid、ValidOptions、Throws；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Validate_InvalidScanTriggerAndParameterFile_ReturnsAllErrors()
    {
        var missingFile = Path.Combine(Path.GetTempPath(), $"camera-{Guid.NewGuid():N}.cfg");
        var options = ValidOptions("Camera01") with
        {
            ScanType = (CameraScanType)999,
            ParameterFilePath = missingFile,
            FrameTrigger = new CameraTriggerSettings
            {
                Mode = (CameraTriggerMode)999,
                Source = "",
            },
        };

        var exception = Assert.Throws<CameraConfigurationException>(
            () => CameraOptionsValidator.Validate(options));

        Assert.Contains(exception.Errors, error => error.Contains("扫描类型", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("参数文件", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("触发模式", StringComparison.Ordinal));
    }

    /// <summary>
    /// 测试相关入口 ValidOptions；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    private static CameraOptions ValidOptions(string name) => new()
    {
        Name = name,
        DriverId = DriverId,
        IpAddress = "192.168.1.10",
    };
}
