using System.Text.Json;
using Device.Core.Cameras;
using Device.Core.Devices;
using Xunit;

namespace Device.Core.Tests;

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
        Assert.NotNull(restored);
        Assert.Equal(DriverId, restored.DriverId);
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

    private static CameraOptions ValidOptions(string name) => new()
    {
        Name = name,
        DriverId = DriverId,
        IpAddress = "192.168.1.10",
    };
}
