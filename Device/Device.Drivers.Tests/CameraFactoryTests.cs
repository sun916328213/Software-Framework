using Device.Core.Cameras;
using Device.Core.Devices;
using Device.Drivers.Cameras;
using Xunit;

namespace Device.Drivers.Tests;

/// <summary>
/// 验证统一相机工厂的驱动目录和品牌路由。
/// </summary>
/// <remarks>
/// 这些测试只创建尚未连接的相机实例，不访问相机硬件或启动厂商 SDK 数据流。
/// </remarks>
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

    private static CameraOptions ValidOptions(string driverId) => new()
    {
        Name = "Camera01",
        DriverId = new DeviceDriverId(driverId),
        IpAddress = "192.168.1.10",
    };
}
