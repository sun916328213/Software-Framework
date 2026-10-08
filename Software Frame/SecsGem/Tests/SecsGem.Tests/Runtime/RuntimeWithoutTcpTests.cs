// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：RuntimeWithoutTcpTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Tests、Secs4Net、SecsGem.Core、SecsGem.Tests.Fakes、SecsGem.Tests.Infrastructure、SecsGem.Runtime.Variable。导入不代表所有类型都会在运行时加载。
using SecsGem.Tests;
using Secs4Net;
using SecsGem.Core;
using SecsGem.Tests.Fakes;
using SecsGem.Tests.Infrastructure;
using SecsGem.Runtime.Variable;
using Extension.Automation.Device;
using Extension.Automation.Secs;
using Xunit;

namespace SecsGem.Tests.Runtime;

/// <summary>
/// RuntimeWithoutTcpTests 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class RuntimeWithoutTcpTests
{
    /// <summary>
    /// 测试相关入口 S1F13_Is_Acknowledged_Without_A_Tcp_Connection；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Create、ConnectAsync、EmitReceivedAsync、L、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task S1F13_Is_Acknowledged_Without_A_Tcp_Connection()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        ISecsGemRuntime service = runtime;
        await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());

        await transport.EmitReceivedAsync(new SecsMessage(1, 13, true) { SecsItem = Item.L() });

        Assert.Equal(SecsFormat.List, transport.LastReplyItem?.Format);
        Assert.Equal((byte)0, transport.LastReplyItem?.Items[0].GetMemory<byte>().Span[0]);
    }

    /// <summary>
    /// 测试相关入口 Remote_Command_Reaches_Registered_Equipment_Handler_Without_Tcp；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Create、SetEventHandler、ConnectAsync、EmitReceivedAsync、L；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Remote_Command_Reaches_Registered_Equipment_Handler_Without_Tcp()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var handler = new FakeSecsGemEventHandler(0);
        ((ISecsGemRuntime)runtime).SetEventHandler(handler);
        await ((ISecsGemRuntime)runtime).ConnectAsync(new SecsGem.Core.SecsGemOptions());

        await transport.EmitReceivedAsync(new SecsMessage(2, 41, true)
        {
            SecsItem = Item.L(
                Item.A("START"),
                Item.L(Item.L(Item.L(Item.A("P1"), Item.A("V1")))))
        });

        Assert.Equal("START", handler.LastCommand?.Name);
        Assert.Equal("V1", handler.LastCommand?.Parameters["P1"]);
        Assert.Equal((byte)0, transport.LastReplyItem?.Items[0].GetMemory<byte>().Span[0]);
    }

    /// <summary>
    /// 测试相关入口 Host_Variable_Query_Uses_Fresh_Equipment_Snapshot；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Create、Add、SetEquipmentDataProvider、EmitReceivedAsync、L；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Host_Variable_Query_Uses_Fresh_Equipment_Snapshot()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var variable = new VIDInfo
        {
            ID = "1", Name = "LotId", Value = "stale", DataType = SecsFormat.ASCII
        };
        runtime.VariableManager.VIDDic_Name[variable.Name] = variable;
        runtime.VariableManager.VIDList.Add(variable);
        ((ISecsGemRuntime)runtime).SetEquipmentDataProvider(
            new FakeEquipmentDataProvider(new Dictionary<string, object?> { ["LotId"] = "LOT-42" }));

        await transport.EmitReceivedAsync(new SecsMessage(1, 3, true)
        {
            SecsItem = Item.L(Item.U2(1))
        });

        Assert.Equal("LOT-42", transport.LastReplyItem?.Items[0].GetString());
    }

    /// <summary>
    /// 测试相关入口 Disconnect_Updates_Public_State_Without_Tcp；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Create、ConnectAsync、UpdateSEMIMode、UpdateDeviceStatus、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Disconnect_Updates_Public_State_Without_Tcp()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        ISecsGemRuntime service = runtime;
        await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());
        runtime.StateManager.UpdateSEMIMode("Mode", ESecsMode.Online_Remote);
        runtime.StateManager.UpdateDeviceStatus("Status", EDeviceStatus.Running);

        Assert.Equal(new SecsGemState(true, SecsGemMode.OnlineRemote, SecsGemEquipmentStatus.Running),
            service.GetState());

        await service.DisconnectAsync();

        Assert.Equal(new SecsGemState(false, SecsGemMode.Offline, SecsGemEquipmentStatus.Running),
            service.GetState());
    }
}
