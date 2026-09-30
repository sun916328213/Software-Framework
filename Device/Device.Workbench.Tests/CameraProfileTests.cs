using Device.Controls.Models;
using Device.Core.Cameras;
using Device.Core.Devices;
using Xunit;

namespace Device.Workbench.Tests;

public sealed class CameraProfileTests
{
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
