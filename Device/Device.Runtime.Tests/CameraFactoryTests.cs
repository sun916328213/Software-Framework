using Device.Core.Cameras;
using Device.Core.Devices;
using Device.Runtime.Cameras;
using Xunit;

namespace Device.Runtime.Tests;

/// <summary>
/// 验证统一相机工厂的驱动目录和品牌路由。
/// </summary>
/// <remarks>
/// 这些测试只创建尚未连接的相机实例，不访问相机硬件或启动厂商 SDK 数据流。
/// 与运行时守卫测试同集合串行执行，避免进程级守卫状态互相干扰。
/// </remarks>
[Collection("RuntimePlatformGuard")]
public sealed class CameraFactoryTests
{
    private static readonly string[] ExpectedDriverIds =
    [
        "camera.basler.gige",
        "camera.dahua.gige",
        "camera.hik.gige",
        "camera.itek.gige",
    ];

    /// <summary>
    /// 工厂应向配置界面公开全部已安装的相机驱动，并按品牌稳定排序。
    /// </summary>
    [Fact]
    public void AvailableDrivers_ContainsAllSupportedBrandsInDisplayOrder()
    {
        var factory = new CameraFactory();

        Assert.Equal(ExpectedDriverIds, factory.AvailableDrivers.Select(x => x.Id.Value));
        Assert.All(factory.AvailableDrivers, descriptor =>
            Assert.Equal(DeviceCategory.Camera, descriptor.Category));
    }

    /// <summary>
    /// 每个稳定驱动标识都应由同一个工厂只创建命中的品牌相机。
    /// </summary>
    /// <param name="driverId">待验证的驱动标识。</param>
    /// <param name="expectedAdapterType">该驱动标识应命中的适配器类型。</param>
    [Theory]
    [InlineData("camera.hik.gige", "HikGigeCamera")]
    [InlineData("camera.basler.gige", "BaslerGigeCamera")]
    [InlineData("camera.dahua.gige", "DahuaGigeCamera")]
    [InlineData("camera.itek.gige", "ITekGigeCamera")]
    public async Task Create_KnownDriver_CreatesOnlyRequestedAdapter(
        string driverId,
        string expectedAdapterType)
    {
        var options = ValidOptions(driverId);
        var factory = new CameraFactory();

        await using var camera = factory.Create(options);

        Assert.Equal(options, camera.Options);
        Assert.Equal(options.Name, camera.Name);
        Assert.Equal(expectedAdapterType, camera.GetType().Name);
        Assert.Equal(DeviceState.Disconnected, camera.State);
    }

    /// <summary>
    /// 未安装的驱动应返回包含可选驱动目录的明确异常。
    /// </summary>
    [Fact]
    public void Create_UnknownDriver_ReportsAllInstalledDrivers()
    {
        var factory = new CameraFactory();
        var options = ValidOptions("camera.unknown.gige");

        var exception = Assert.Throws<CameraDriverNotFoundException>(() => factory.Create(options));

        foreach (var driverId in ExpectedDriverIds)
            Assert.Contains(driverId, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 面阵相机只下发当前帧触发器，并在软件触发模式下主动发送触发命令。
    /// </summary>
    [Fact]
    public void DriverConfiguration_AreaScanSoftwareTrigger_UsesCurrentSelector()
    {
        var options = ValidOptions(ExpectedDriverIds[0]);

        var bindings = CameraDriverConfiguration.GetTriggerBindings(options);

        var binding = Assert.Single(bindings);
        Assert.Null(binding.Selector);
        Assert.Equal(CameraTriggerSources.Software, binding.Settings.Source);
        Assert.True(CameraDriverConfiguration.ShouldExecuteSoftwareTrigger(options));
    }

    /// <summary>
    /// 线扫相机应依次设置行触发和帧触发；外部帧触发不能由 CaptureAsync 发送软件命令。
    /// </summary>
    [Fact]
    public void DriverConfiguration_LineScanExternalTrigger_ConfiguresBothSelectors()
    {
        var options = ValidOptions(ExpectedDriverIds[0]) with
        {
            ScanType = CameraScanType.LineScan,
            FrameTrigger = new CameraTriggerSettings
            {
                Selector = CameraTriggerSelectors.FrameBurstStart,
                Mode = CameraTriggerMode.On,
                Source = CameraTriggerSources.Line1,
            },
            LineTrigger = new CameraTriggerSettings
            {
                Mode = CameraTriggerMode.On,
                Source = CameraTriggerSources.RotaryEncoder1,
            },
        };

        var bindings = CameraDriverConfiguration.GetTriggerBindings(options);

        Assert.Collection(
            bindings,
            line =>
            {
                Assert.Equal(CameraDriverConfiguration.LineStartSelector, line.Selector);
                Assert.Equal(CameraTriggerSources.RotaryEncoder1, line.Settings.Source);
            },
            frame =>
            {
                Assert.Equal(CameraTriggerSelectors.FrameBurstStart, frame.Selector);
                Assert.Equal(CameraTriggerSources.Line1, frame.Settings.Source);
            });
        Assert.False(CameraDriverConfiguration.ShouldExecuteSoftwareTrigger(options));
    }

    /// <summary>
    /// 运行时下发与连接期下发必须共用同一套决策：线扫先行触发、再帧触发。
    /// </summary>
    [Fact]
    public void GetTriggerBindings_LineScanRuntimeSettings_UsesLineThenFrameOrder()
    {
        var frame = new CameraTriggerSettings
        {
            Mode = CameraTriggerMode.On,
            Source = CameraTriggerSources.Line2,
        };
        var line = new CameraTriggerSettings
        {
            Mode = CameraTriggerMode.On,
            Source = CameraTriggerSources.RotaryEncoder1,
        };

        var bindings = CameraDriverConfiguration.GetTriggerBindings(
            CameraScanType.LineScan,
            frame,
            line);

        Assert.Collection(
            bindings,
            first =>
            {
                Assert.Equal(CameraDriverConfiguration.LineStartSelector, first.Selector);
                Assert.Equal(CameraTriggerSources.RotaryEncoder1, first.Settings.Source);
            },
            second =>
            {
                Assert.Equal(CameraDriverConfiguration.FrameStartSelector, second.Selector);
                Assert.Equal(CameraTriggerSources.Line2, second.Settings.Source);
            });
    }

    /// <summary>
    /// 面阵相机不下发行触发，并保留设备当前的触发选择器。
    /// </summary>
    [Fact]
    public void GetTriggerBindings_AreaScanRuntimeSettings_IgnoresLineTrigger()
    {
        var frame = new CameraTriggerSettings
        {
            Mode = CameraTriggerMode.On,
            Source = CameraTriggerSources.Line0,
        };
        var line = new CameraTriggerSettings
        {
            Mode = CameraTriggerMode.On,
            Source = CameraTriggerSources.RotaryEncoder1,
        };

        var bindings = CameraDriverConfiguration.GetTriggerBindings(
            CameraScanType.AreaScan,
            frame,
            line);

        var binding = Assert.Single(bindings);
        Assert.Null(binding.Selector);
        Assert.Equal(CameraTriggerSources.Line0, binding.Settings.Source);
    }

    /// <summary>
    /// 运行时改过触发后，是否主动发软件触发命令必须看当前生效的帧触发，
    /// 而不是创建相机时的 <see cref="CameraOptions.FrameTrigger"/>。
    /// </summary>
    [Fact]
    public void ShouldExecuteSoftwareTrigger_RuntimeSettings_FollowsCurrentSource()
    {
        Assert.True(CameraDriverConfiguration.ShouldExecuteSoftwareTrigger(
            new CameraTriggerSettings
            {
                Mode = CameraTriggerMode.On,
                Source = CameraTriggerSources.Software,
            }));

        Assert.False(CameraDriverConfiguration.ShouldExecuteSoftwareTrigger(
            new CameraTriggerSettings
            {
                Mode = CameraTriggerMode.On,
                Source = CameraTriggerSources.Line1,
            }));

        // 关闭帧触发时相机自由运行，同样不能主动发软件触发命令
        Assert.False(CameraDriverConfiguration.ShouldExecuteSoftwareTrigger(
            new CameraTriggerSettings
            {
                Mode = CameraTriggerMode.Off,
                Source = CameraTriggerSources.Software,
            }));
    }

    /// <summary>
    /// 合法的运行时触发配置应通过校验。
    /// </summary>
    [Fact]
    public void ValidateTriggerSettings_ValidLineScanSettings_ReturnsNull()
    {
        var error = CameraDriverConfiguration.ValidateTriggerSettings(
            CameraScanType.LineScan,
            new CameraTriggerSettings
            {
                Mode = CameraTriggerMode.On,
                Source = CameraTriggerSources.Line1,
            },
            new CameraTriggerSettings
            {
                Mode = CameraTriggerMode.Off,
                Source = CameraTriggerSources.Line1,
            });

        Assert.Null(error);
    }

    /// <summary>
    /// 面阵相机不下发行触发，因此行触发里的无效值不能阻断帧触发的下发。
    /// </summary>
    [Fact]
    public void ValidateTriggerSettings_AreaScanWithInvalidLineTrigger_IsAccepted()
    {
        var error = CameraDriverConfiguration.ValidateTriggerSettings(
            CameraScanType.AreaScan,
            new CameraTriggerSettings
            {
                Mode = CameraTriggerMode.On,
                Source = CameraTriggerSources.Software,
            },
            new CameraTriggerSettings
            {
                Mode = CameraTriggerMode.On,
                Source = "   ",
            });

        Assert.Null(error);
    }

    /// <summary>
    /// 线扫相机的行触发无效时同样要拦下。
    /// </summary>
    [Fact]
    public void ValidateTriggerSettings_LineScanWithBlankLineTriggerSource_ReportsError()
    {
        var error = CameraDriverConfiguration.ValidateTriggerSettings(
            CameraScanType.LineScan,
            new CameraTriggerSettings
            {
                Mode = CameraTriggerMode.On,
                Source = CameraTriggerSources.Software,
            },
            new CameraTriggerSettings
            {
                Mode = CameraTriggerMode.On,
                Source = "   ",
            });

        Assert.NotNull(error);
        Assert.Contains("行", error, StringComparison.Ordinal);
    }

    /// <summary>
    /// 启用触发却没给触发源时必须拦下，否则厂商 SDK 会报参数错误。
    /// </summary>
    [Fact]
    public void ValidateTriggerSettings_EnabledTriggerWithoutSource_ReportsError()
    {
        var error = CameraDriverConfiguration.ValidateTriggerSettings(
            CameraScanType.AreaScan,
            new CameraTriggerSettings
            {
                Mode = CameraTriggerMode.On,
                Source = string.Empty,
            },
            new CameraTriggerSettings());

        Assert.NotNull(error);
        Assert.Contains("帧", error, StringComparison.Ordinal);
    }

    /// <summary>
    /// 触发模式为未定义枚举值时应被拦下（JSON 反序列化可以塞进任意整数）。
    /// </summary>
    [Fact]
    public void ValidateTriggerSettings_UndefinedTriggerMode_ReportsError()
    {
        var error = CameraDriverConfiguration.ValidateTriggerSettings(
            CameraScanType.AreaScan,
            new CameraTriggerSettings
            {
                Mode = (CameraTriggerMode)42,
                Source = CameraTriggerSources.Software,
            },
            new CameraTriggerSettings());

        Assert.NotNull(error);
        Assert.Contains("模式", error, StringComparison.Ordinal);
    }

    private static CameraOptions ValidOptions(string driverId) => new()
    {
        Name = "Camera01",
        DriverId = new DeviceDriverId(driverId),
        IpAddress = "192.168.1.10",
    };
}
