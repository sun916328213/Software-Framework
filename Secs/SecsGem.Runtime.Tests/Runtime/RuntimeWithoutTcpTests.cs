using Secs4Net;
using SecsGem.Core;
using SecsGem.Runtime.Tests.Fakes;
using SecsGem.Runtime.Tests.Infrastructure;
using Secs.Variable;
using Extension.Automation.Device;
using Extension.Automation.Secs;
using Xunit;

namespace SecsGem.Runtime.Tests.Runtime;

public sealed class RuntimeWithoutTcpTests
{
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

    [Fact]
    public async Task Host_Variable_Query_Uses_Fresh_Equipment_Snapshot()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var variable = new VIDInfo
        {
            ID = "1", Name = "LotId", Value = "stale", DataType = SecsFormat.ASCII
        };
        runtime._VIDManager.VIDDic_Name[variable.Name] = variable;
        runtime._VIDManager.VIDList.Add(variable);
        ((ISecsGemRuntime)runtime).SetEquipmentDataProvider(
            new FakeEquipmentDataProvider(new Dictionary<string, object?> { ["LotId"] = "LOT-42" }));

        await transport.EmitReceivedAsync(new SecsMessage(1, 3, true)
        {
            SecsItem = Item.L(Item.U2(1))
        });

        Assert.Equal("LOT-42", transport.LastReplyItem?.Items[0].GetString());
    }

    [Fact]
    public async Task Disconnect_Updates_Public_State_Without_Tcp()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        ISecsGemRuntime service = runtime;
        await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());
        runtime._StatusManager.UpdateSEMIMode("Mode", ESecsMode.Online_Remote);
        runtime._StatusManager.UpdateDeviceStatus("Status", EDeviceStatus.Running);

        Assert.Equal(new SecsGemState(true, SecsGemMode.OnlineRemote, SecsGemEquipmentStatus.Running),
            service.GetState());

        await service.DisconnectAsync();

        Assert.Equal(new SecsGemState(false, SecsGemMode.Offline, SecsGemEquipmentStatus.Running),
            service.GetState());
    }
}
