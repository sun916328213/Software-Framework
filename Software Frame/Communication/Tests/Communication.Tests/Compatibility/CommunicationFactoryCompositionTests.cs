// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：CommunicationFactoryCompositionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Runtime.Core、Communication.Runtime.Composition、Communication.Core.Protocols.Modbus.ModbusTcp、Communication.Runtime.Protocols.Modbus.ModbusTcp、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Runtime.Core;
using Communication.Runtime.Composition;
using Communication.Core.Protocols.Modbus.ModbusTcp;
using Communication.Runtime.Protocols.Modbus.ModbusTcp;
using Xunit;

namespace Communication.Tests.Compatibility;

/// <summary>
/// CommunicationFactoryCompositionTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class CommunicationFactoryCompositionTests
{

    /// <summary>
    /// 验证 Manager_uses_explicit_builtin_protocol_registry 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateDefaultFactory、CreateDevice、IsType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Manager_uses_explicit_builtin_protocol_registry()
    {
        using var manager = new CommunicationManager(CommunicationDefaults.CreateDefaultFactory());

        var device = manager.CreateDevice(new ModbusTcpParam());

        Assert.IsType<ModbusTcpClientDevice>(device);
    }

    /// <summary>
    /// 验证 Default_registry_creates_builtin_protocol_device 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Create、CreateDefaultFactory、IsType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Default_registry_creates_builtin_protocol_device()
    {
        using var device = CommunicationDefaults.CreateDefaultFactory().Create(new ModbusTcpParam());

        Assert.IsType<ModbusTcpClientDevice>(device);
    }

    /// <summary>
    /// 验证 Core_factory_contract_can_be_injected_into_manager 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateDefaultFactory、CreateDevice、IsType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Core_factory_contract_can_be_injected_into_manager()
    {
        Communication.Core.ICommunicationDeviceFactory factory =
            CommunicationDefaults.CreateDefaultFactory();
        using var manager = new CommunicationManager(factory);
        using var device = manager.CreateDevice(new ModbusTcpParam());

        Assert.IsType<ModbusTcpClientDevice>(device);
    }

    /// <summary>
    /// 验证 Default_factory_exposes_core_factory_seam 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateDefaultFactory、Create、IsType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Default_factory_exposes_core_factory_seam()
    {
        Communication.Core.ICommunicationDeviceFactory factory =
            CommunicationDefaults.CreateDefaultFactory();
        using var device = factory.Create(new ModbusTcpParam());

        Assert.IsType<ModbusTcpClientDevice>(device);
    }

}
