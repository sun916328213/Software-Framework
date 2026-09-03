using CommunicationNew.Composition;
using CommunicationNew.Core;
using CommunicationNew.Protocols.Modbus.ModbusTcp;
using Xunit;

namespace CommunicationNew.Tests.Composition;

public sealed class CommunicationDeviceRegistryTests
{
    [Fact]
    public void New_parameter_type_can_be_registered_without_changing_a_core_switch()
    {
        var registry = new CommunicationDeviceRegistry();
        registry.Register<CustomParam>(parameter => new ModbusTcpClientDevice(new ModbusTcpParam
        {
            DeviceName = parameter.DeviceName,
        }));

        var device = registry.Create(new CustomParam { DeviceName = "Custom device" });

        Assert.IsType<ModbusTcpClientDevice>(device);
        Assert.Equal("Custom device", device.DeviceName);
    }

    private sealed class CustomParam : CommunicationParam
    {
    }
}
