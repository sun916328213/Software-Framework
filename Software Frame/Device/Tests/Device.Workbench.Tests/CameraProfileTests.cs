// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：CameraProfileTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Workbench、Device.Controls.Models、Device.Core.Cameras、Device.Core.Devices、Xunit。导入不代表所有类型都会在运行时加载。
using Device.Workbench;
using Device.Controls.Models;
using Device.Core.Cameras;
using Device.Core.Devices;
using Xunit;

namespace Device.Workbench.Tests;

/// <summary>
/// CameraProfileTests 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class CameraProfileTests
{
    /// <summary>
    /// 测试相关入口 ToOptions_MapsAllConnectionFields；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ToOptions、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ToOptions_MapsAllConnectionFields()
    {
        var profile = new CameraProfile
        {
            Name = "相机A",
            DriverId = "camera.hik.gige",
            ScanType = CameraScanType.LineScan,
            IpAddress = "192.168.1.50",
            ExposureMilliseconds = 123,
            Gain = 2.5,
            TimeoutMilliseconds = 2500,
            ParameterFilePath = @"C:\cfg\cam.ini",
            FrameTriggerMode = CameraTriggerMode.On,
            FrameTriggerSource = CameraTriggerSources.Line1,
            LineTriggerMode = CameraTriggerMode.On,
            LineTriggerSource = CameraTriggerSources.RotaryEncoder1,
        };

        var options = profile.ToOptions();

        Assert.Equal("相机A", options.Name);
        Assert.Equal(new DeviceDriverId("camera.hik.gige"), options.DriverId);
        Assert.Equal(CameraScanType.LineScan, options.ScanType);
        Assert.Equal("192.168.1.50", options.IpAddress);
        Assert.Equal(123, options.ExposureMilliseconds);
        Assert.Equal(2.5, options.Gain);
        Assert.Equal(2500, options.TimeoutMilliseconds);
        Assert.Equal(@"C:\cfg\cam.ini", options.ParameterFilePath);
        Assert.Equal(CameraTriggerMode.On, options.FrameTrigger.Mode);
        Assert.Equal(CameraTriggerSources.Line1, options.FrameTrigger.Source);
        Assert.Equal(CameraTriggerMode.On, options.LineTrigger.Mode);
        Assert.Equal(CameraTriggerSources.RotaryEncoder1, options.LineTrigger.Source);
        Assert.Equal(CameraTriggerSelectors.LineStart, options.LineTrigger.Selector);
    }

    /// <summary>
    /// 测试相关入口 ToOptions_BlankParameterFilePath_MapsToNull；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Null、ToOptions；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ToOptions_BlankParameterFilePath_MapsToNull()
    {
        var profile = new CameraProfile
        {
            Name = "相机B",
            DriverId = "camera.basler.gige",
            IpAddress = "10.0.0.2",
            ParameterFilePath = "   ",
        };

        Assert.Null(profile.ToOptions().ParameterFilePath);
    }

    /// <summary>
    /// 测试相关入口 BuildFrameTrigger_UsesProfileModeAndSource；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 BuildFrameTrigger、Equal、Null；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void BuildFrameTrigger_UsesProfileModeAndSource()
    {
        var profile = new CameraProfile
        {
            FrameTriggerMode = CameraTriggerMode.Off,
            FrameTriggerSource = CameraTriggerSources.Software,
        };

        var trigger = profile.BuildFrameTrigger();

        Assert.Equal(CameraTriggerMode.Off, trigger.Mode);
        Assert.Equal(CameraTriggerSources.Software, trigger.Source);
        Assert.Null(trigger.Selector);
    }

    /// <summary>
    /// 测试相关入口 BuildLineTrigger_AlwaysUsesLineStartSelector；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 BuildLineTrigger、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void BuildLineTrigger_AlwaysUsesLineStartSelector()
    {
        var profile = new CameraProfile
        {
            LineTriggerMode = CameraTriggerMode.On,
            LineTriggerSource = CameraTriggerSources.Line2,
        };

        var trigger = profile.BuildLineTrigger();

        Assert.Equal(CameraTriggerSelectors.LineStart, trigger.Selector);
        Assert.Equal(CameraTriggerMode.On, trigger.Mode);
        Assert.Equal(CameraTriggerSources.Line2, trigger.Source);
    }
}
