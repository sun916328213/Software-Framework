using System.Text.Json;
using Device.Core.Cameras;
using Device.Core.Devices;
using Xunit;

namespace Device.Tests.Core;

public sealed class CameraContractTests
{
    private static readonly DeviceDriverId DriverId = new("camera.fake.gige");

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

    private static CameraOptions ValidOptions(string name) => new()
    {
        Name = name,
        DriverId = DriverId,
        IpAddress = "192.168.1.10",
    };
}
