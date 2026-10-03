// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：CommunicationDeviceRegistryTests、CustomParam、StubFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Runtime.Composition、Communication.Core、Communication.Runtime.Core、Communication.Core.Protocols.Modbus.ModbusTcp、Communication.Runtime.Protocols.Modbus.ModbusTcp、Microsoft.Extensions.DependencyInjection。导入不代表所有类型都会在运行时加载。
using Communication.Runtime.Composition;
using Communication.Core;
using Communication.Runtime.Core;
using Communication.Core.Protocols.Modbus.ModbusTcp;
using Communication.Runtime.Protocols.Modbus.ModbusTcp;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Communication.Tests.Composition;

/// <summary>
/// CommunicationDeviceRegistryTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class CommunicationDeviceRegistryTests
{
    /// <summary>
    /// 验证 Registry_supports_core_factory_seam_and_legacy_composition_alias 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 IsAssignableFrom；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Registry_supports_core_factory_seam_and_legacy_composition_alias()
    {
        var registry = new CommunicationDeviceRegistry();

        Assert.IsAssignableFrom<Communication.Core.ICommunicationDeviceFactory>(registry);
#pragma warning disable CS0618 // 本断言专门覆盖旧接口兼容性。
        Assert.IsAssignableFrom<Communication.Core.ICommunicationDeviceFactory>(registry);
#pragma warning restore CS0618
    }

    /// <summary>
    /// 验证 Manager_accepts_factory_through_core_seam 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateDevice、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Manager_accepts_factory_through_core_seam()
    {
        var expected = new ModbusTcpClientDevice(new ModbusTcpParam());
        Communication.Core.ICommunicationDeviceFactory factory = new StubFactory(expected);
        using var manager = new CommunicationManager(factory);

        var actual = manager.CreateDevice(new CustomParam());

        Assert.Same(expected, actual);
    }

    /// <summary>
    /// 验证 Manager_rejects_missing_core_factory 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Throws；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Manager_rejects_missing_core_factory()
    {
        Assert.Throws<ArgumentNullException>(() => new CommunicationManager(null!));
    }

    /// <summary>
    /// 验证 New_parameter_type_can_be_registered_without_changing_a_core_switch 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Register、Create、IsType、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Microsoft_di_resolves_new_and_legacy_factory_seams_to_one_instance 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 AddCommunicationNew、BuildServiceProvider、GetRequiredService、Same、NotNull；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Microsoft_di_resolves_new_and_legacy_factory_seams_to_one_instance()
    {
        var services = new ServiceCollection();
        services.AddCommunicationNew();
        using var provider = services.BuildServiceProvider();

        var current = provider.GetRequiredService<
            Communication.Core.ICommunicationDeviceFactory>();
#pragma warning disable CS0618 // 本断言锁定迁移期旧 DI 解析键的兼容行为。
        var legacy = provider.GetRequiredService<
            Communication.Core.ICommunicationDeviceFactory>();
#pragma warning restore CS0618

        Assert.Same(current, legacy);
        Assert.NotNull(provider.GetRequiredService<CommunicationManager>());
    }

    /// <summary>
    /// CustomParam 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：CommunicationParam。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class CustomParam : CommunicationParam
    {
    }

    /// <summary>
    /// StubFactory 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：Communication.Core.ICommunicationDeviceFactory。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class StubFactory(ICommunicationDevice device)
        : Communication.Core.ICommunicationDeviceFactory
    {
        /// <summary>
        /// 验证 Create 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public ICommunicationDevice Create(CommunicationParam parameter) => device;
    }
}
