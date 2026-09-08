using CommunicationNew.Composition;
using CommunicationNew.Core;
using CommunicationNew.Protocols.Modbus.ModbusTcp;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CommunicationNew.Tests.Composition;

public sealed class CommunicationDeviceRegistryTests
{
    [Fact]
    public void Registry_supports_core_factory_seam_and_legacy_composition_alias()
    {
        var registry = new CommunicationDeviceRegistry();

        Assert.IsAssignableFrom<CommunicationNew.Core.ICommunicationDeviceFactory>(registry);
#pragma warning disable CS0618 // 本断言专门覆盖旧接口兼容性。
        Assert.IsAssignableFrom<CommunicationNew.Composition.ICommunicationDeviceFactory>(registry);
#pragma warning restore CS0618
    }

    [Fact]
    public void Manager_accepts_factory_through_core_seam()
    {
        var expected = new ModbusTcpClientDevice(new ModbusTcpParam());
        CommunicationNew.Core.ICommunicationDeviceFactory factory = new StubFactory(expected);
        using var manager = new CommunicationManager(factory);

        var actual = manager.CreateDevice(new CustomParam());

        Assert.Same(expected, actual);
    }

    [Fact]
    public void Manager_rejects_missing_core_factory()
    {
        Assert.Throws<ArgumentNullException>(() => new CommunicationManager(null!));
    }

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

    [Fact]
    public void Microsoft_di_resolves_new_and_legacy_factory_seams_to_one_instance()
    {
        var services = new ServiceCollection();
        services.AddCommunicationNew();
        using var provider = services.BuildServiceProvider();

        var current = provider.GetRequiredService<
            CommunicationNew.Core.ICommunicationDeviceFactory>();
#pragma warning disable CS0618 // 本断言锁定迁移期旧 DI 解析键的兼容行为。
        var legacy = provider.GetRequiredService<
            CommunicationNew.Composition.ICommunicationDeviceFactory>();
#pragma warning restore CS0618

        Assert.Same(current, legacy);
        Assert.NotNull(provider.GetRequiredService<CommunicationManager>());
    }

    private sealed class CustomParam : CommunicationParam
    {
    }

    private sealed class StubFactory(ICommunicationDevice device)
        : CommunicationNew.Core.ICommunicationDeviceFactory
    {
        public ICommunicationDevice Create(CommunicationParam parameter) => device;
    }
}
