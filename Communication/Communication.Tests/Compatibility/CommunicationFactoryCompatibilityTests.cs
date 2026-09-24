using CommunicationNew.Core;
using CommunicationNew.Composition;
using CommunicationNew.Protocols.Modbus.ModbusTcp;
using Xunit;

namespace CommunicationNew.Tests.Compatibility;

public sealed class CommunicationFactoryCompatibilityTests
{
#pragma warning disable CS0618 // 本测试专门验证已弃用入口在迁移期仍保持可用。

    [Fact]
    public void Parameterless_manager_still_uses_builtin_protocol_registry()
    {
        using var manager = new CommunicationManager();

        var device = manager.CreateDevice(new ModbusTcpParam());

        Assert.IsType<ModbusTcpClientDevice>(device);
    }

    [Fact]
    public void Static_factory_still_creates_builtin_protocol_device()
    {
        using var device = CommunicationFactory.Create(new ModbusTcpParam());

        Assert.IsType<ModbusTcpClientDevice>(device);
    }

    [Fact]
    public void Default_factory_remains_assignable_to_legacy_composition_interface()
    {
        CommunicationNew.Composition.ICommunicationDeviceFactory factory =
            CommunicationDefaults.CreateFactory();
        using var device = factory.Create(new ModbusTcpParam());

        Assert.IsType<ModbusTcpClientDevice>(device);
    }

    [Fact]
    public void Default_factory_exposes_core_factory_seam()
    {
        CommunicationNew.Core.ICommunicationDeviceFactory factory =
            CommunicationDefaults.CreateDefaultFactory();
        using var device = factory.Create(new ModbusTcpParam());

        Assert.IsType<ModbusTcpClientDevice>(device);
    }

#pragma warning restore CS0618
}
