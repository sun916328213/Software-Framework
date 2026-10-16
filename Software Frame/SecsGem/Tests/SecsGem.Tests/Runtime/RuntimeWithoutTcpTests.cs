// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：RuntimeWithoutTcpTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Tests、Secs4Net、SecsGem.Core、SecsGem.Tests.Fakes、SecsGem.Tests.Infrastructure、SecsGem.Runtime.Variable。导入不代表所有类型都会在运行时加载。
using SecsGem.Tests;
using System;
using System.IO;
using System.Linq;
using Secs4Net;
using SecsGem.Core;
using SecsGem.Tests.Fakes;
using SecsGem.Tests.Infrastructure;
using SecsGem.Runtime.Variable;
using SecsGem.Runtime.Manager;
using Extension.Automation.Device;
using Extension.Automation.Secs;
using System.Globalization;
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
            ID = "1", Name = "LotId", Value = "stale", DataType = SecsFormat.ASCII, Category = "SVID"
        };
        runtime.VariableManager.VIDDic_Name[variable.Name] = variable;
        runtime.VariableManager.VIDDic_ID[variable.ID] = variable;
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

    /// <summary>
    /// 操作员侧上/下线：只改本地 SEMI 模式并上报 CEID，绝不发 S1F17/S1F15
    /// （E5 里这两条是 Host→Equipment；通知 Host 靠 S6F11 状态变更事件，Control State Model §10.2）。
    /// 原实现 <c>Online_EtoH</c> 先发 S1F17，而 <c>S1F17.GetSendItem</c> 恒返回 null，
    /// 引擎直接抛"组包失败"，上线按钮整个不可用。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Operator_Online_Or_Offline_Never_Sends_Host_Directed_S1F17_Or_S1F15(bool online)
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        ISecsGemRuntime service = runtime;
        await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());

        var ok = online ? await service.GoOnlineRemoteAsync() : await service.GoOfflineAsync();

        Assert.True(ok, online ? "上线失败" : "下线失败");
        Assert.Equal(online ? SecsGemMode.OnlineRemote : SecsGemMode.Offline, service.GetState().Mode);
        // CEID 表是空的，"Device Mode" 查不到，所以设备端不应发出任何报文。
        Assert.Null(transport.LastSentOrder);
    }

    /// <summary>
    /// 控制状态变更发两条 S6F11：先通用的 "Device Mode"（report 挂了 SVID 102，事件自带新模式值），
    /// 再报目标态专用事件（Device Online / Device Local / Device Offline），Host 可只订阅关心的那个。
    /// 见 <c>ConnectManager.SendControlStateChangedAsync</c>。
    /// </summary>
    [Theory]
    [InlineData(SecsGemMode.OnlineLocal, 102u, 105u)]      // Device Mode + Device Local
    [InlineData(SecsGemMode.OnlineRemote, 102u, 103u)]     // Device Mode + Device Online
    [InlineData(SecsGemMode.Offline, 102u, 104u)]          // Device Mode + Device Offline
    public async Task Every_Control_State_Change_Reports_Device_Mode_Then_The_Target_State_Ceid(
        SecsGemMode target, uint modeCeid, uint stateCeid)
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var catalog = runtime.VariableManager;
        // S6F11 组包会先 UpdateVID("Clock")，没有 Clock 会在组包阶段抛，测不到 CEID 选择
        void AddVid(string id, string name, SecsFormat format, object? value)
        {
            var vid = new VIDInfo { ID = id, Name = name, Value = value, DataType = format, DataTypeName = format.ToString(), Category = "SVID" };
            catalog.SVID.Datas.Add(vid);
            catalog.VIDList.Add(vid);
            catalog.VIDDic_Name[name] = vid;
            catalog.VIDDic_ID[vid.ID] = vid;
        }
        AddVid("101", "Clock", SecsFormat.ASCII, null);          // 组包时会被 UpdateVID 填时间戳
        AddVid("102", "DeviceMode", SecsFormat.U2, (ushort)2);    // 数值型必须给值：空串会让 Convert.ToUInt16 抛
        catalog.ReportID.Datas.Add(new ReportInfo { ID = "11", Name = "DeviceMode", VID_Collection = "102" });
        catalog.ReportID.Datas.Add(new ReportInfo { ID = "13", Name = "DeviceInfo", VID_Collection = "102" });
        catalog.CEID.Datas.Add(new CEIDInfo { ID = "102", Name = "Device Mode", ReportID_Collection = "11" });
        catalog.CEID.Datas.Add(new CEIDInfo { ID = "103", Name = "Device Online", ReportID_Collection = "13" });
        catalog.CEID.Datas.Add(new CEIDInfo { ID = "104", Name = "Device Offline", ReportID_Collection = "13" });
        catalog.CEID.Datas.Add(new CEIDInfo { ID = "105", Name = "Device Local", ReportID_Collection = "11" });

        ISecsGemRuntime service = runtime;
        await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());

        if (target == SecsGemMode.OnlineLocal) Assert.True(await service.GoOnlineLocalAsync());
        else if (target == SecsGemMode.OnlineRemote) Assert.True(await service.GoOnlineRemoteAsync());
        else Assert.True(await service.GoOfflineAsync());

        Assert.Equal(target, service.GetState().Mode);
        Assert.All(transport.Sent, s => Assert.Equal("S6F11", s.Order));   // 只发事件，绝不发 S1F17/S1F15
        Assert.Equal(new uint[] { modeCeid, stateCeid },
            transport.Sent.Select(s => (uint)s.Item.Items[1].GetMemory<ushort>().Span[0]));
    }

    /// <summary>
    /// 端到端：<c>Config()</c> 配好名字后，模式变化必须真的把值写进 DeviceMode 这个 SVID，
    /// 否则 S6F11 组包时 `vid.Value != null` 会把它整条跳过，Host 收到的事件里没有模式值。
    /// 走默认项（无 CSV）顺带覆盖 <see cref="VariableDefaultsTests"/> 与 StatusManager.Config 的接线。
    /// </summary>
    [Theory]
    [InlineData(SecsGemMode.OnlineLocal, 1)]
    [InlineData(SecsGemMode.OnlineRemote, 2)]
    [InlineData(SecsGemMode.Offline, 0)]
    public async Task Config_Wires_The_Mode_Vid_So_The_Reported_Event_Carries_Its_Value(SecsGemMode target, ushort expected)
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var catalog = runtime.VariableManager;
        var empty = Path.Combine(Path.GetTempPath(), "SecsGemTests", Guid.NewGuid().ToString("N"));
        catalog.SVID.FilePath = empty;
        catalog.DVID.FilePath = empty;
        catalog.ECID.FilePath = empty;
        catalog.CEID.FilePath = empty;
        catalog.ReportID.FilePath = empty;
        try
        {
            runtime.Config();                                   // 载入默认项并配好 StatusManager 的四个名字

            // 默认项的值是 null（S6F11 组包会跳过），先给个基线值，好观察后面是否被覆盖
            catalog.UpdateVID("DeviceMode", (ushort)9);
            catalog.UpdateVID("DeviceStatus", (ushort)9);

            ISecsGemRuntime service = runtime;
            await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());
            Assert.Equal("DeviceMode", runtime.StateManager.Mode_VIDName);
            Assert.Equal("DeviceStatus", runtime.StateManager.Status_VIDName);

            // 离线只在"确实发生了变化"时才写值（SetMode 比的是 _reportedMode），
            // 所以先制造一个非离线起点，否则 Offline→Offline 什么都不做。
            if (target == SecsGemMode.Offline) Assert.True(await service.GoOnlineRemoteAsync());

            bool ok = target == SecsGemMode.OnlineLocal ? await service.GoOnlineLocalAsync()
                : target == SecsGemMode.OnlineRemote ? await service.GoOnlineRemoteAsync()
                : await service.GoOfflineAsync();
            Assert.True(ok, "上下线失败");

            // SecsGemMode(契约层) 与 ESecsMode(运行时) 序号一致，变量里存的是后者
            Assert.Equal((ESecsMode)(int)target, catalog.GetVIDByName("DeviceMode")!.Value);
            // DeviceStatus 不该被模式变化顺手改掉
            Assert.Equal((ushort)9, catalog.GetVIDByName("DeviceStatus")!.Value);
            // 真正的验收点：最后一条 Device Mode(CEID 102) 事件的报文里带着 U2 的新模式值。
            // S6F11 结构见 S6F11.GetSendItem：L(DataID, CEID, L(report: L(reportId, L(VID 值...))))。
            // 注意 report 里只有值没有 VID ID，ID 由 ReportID 配置隐含，所以只能按位置取。
            var modeEvent = transport.Sent.Last(s => s.Item.Items[1].GetMemory<ushort>().Span[0] == 102);
            var reports = modeEvent.Item.Items[2].Items;
            var lastReport = reports[reports.Length - 1];
            Assert.Equal((ushort)11, lastReport.Items[0].GetMemory<ushort>().Span[0]);   // report 11 = DeviceMode
            Assert.Equal(expected, lastReport.Items[1].Items[0].GetMemory<ushort>().Span[0]);
        }
        finally
        {
            if (Directory.Exists(empty)) Directory.Delete(empty, recursive: true);
        }
    }

    /// <summary>
    /// 两个在线入口各自直达目标模式，任意起点都成立：
    /// Offline→Local、Offline→Remote（迁移表 #1）、Remote→Local（#9 操作员抢回控制权）。
    /// </summary>
    [Theory]
    [InlineData(SecsGemMode.Offline, true, SecsGemMode.OnlineLocal)]
    [InlineData(SecsGemMode.Offline, false, SecsGemMode.OnlineRemote)]
    [InlineData(SecsGemMode.OnlineRemote, true, SecsGemMode.OnlineLocal)]
    [InlineData(SecsGemMode.OnlineLocal, false, SecsGemMode.OnlineRemote)]
    public async Task Operator_Online_Entries_Reach_Their_Target_From_Any_State(
        SecsGemMode from, bool toLocal, SecsGemMode expected)
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        ISecsGemRuntime service = runtime;
        await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());
        switch (from)
        {
            case SecsGemMode.OnlineLocal:
                runtime.StateManager.UpdateSEMIMode("Mode", Extension.Automation.Secs.ESecsMode.Online_Local);
                break;
            case SecsGemMode.OnlineRemote:
                runtime.StateManager.UpdateSEMIMode("Mode", Extension.Automation.Secs.ESecsMode.Online_Remote);
                break;
        }
        Assert.Equal(from, service.GetState().Mode);

        Assert.True(toLocal ? await service.GoOnlineLocalAsync() : await service.GoOnlineRemoteAsync());

        Assert.Equal(expected, service.GetState().Mode);
        // CEID 表是空的，查不到对应事件，不应发出任何报文
        Assert.Null(transport.LastSentOrder);
    }

    /// <summary>
    /// 关闭时必须先下线再断连：Host 只能靠 S6F11 状态变更事件知道设备干净地离开了
    /// （EQ 不发 S1F15）。断在前的话 Host 只看到 socket 掉线，分不清关机还是崩溃。
    /// </summary>
    [Fact]
    public async Task Shutdown_Tears_Down_The_Transport()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        ISecsGemRuntime service = runtime;
        await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());

        await service.ShutdownAsync(TimeSpan.FromSeconds(5));

        Assert.False(service.GetState().IsConnected);
    }

    /// <summary>Host 不回 S6F12 时也不能把关闭拖死——通知尽力而为，超时照常断连。</summary>
    [Fact]
    public async Task Shutdown_Completes_Even_When_Offline_Notification_Hangs()
    {
        var transport = new InMemorySecsTransport { HangOnOrder = "S6F11" };
        using var runtime = RuntimeTestFixture.Create(transport);
        ISecsGemRuntime service = runtime;
        await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());

        await service.ShutdownAsync(TimeSpan.FromMilliseconds(50));

        Assert.False(service.GetState().IsConnected);
    }

    /// <summary>
    /// Host 主动上线的收发分离：
    /// ① S1F17 的 GetReplyItem 只回 ONLACK，不切状态；
    /// ② 引擎回完 S1F18 后触发 "S1F17" 事件，由 ConnectManager.Online_HtoE 切状态并报 S6F11。
    /// 这样 S1F18 一定先于 S6F11（§10.1 顺序要求），且状态切换点只有一处。
    /// </summary>
    [Fact]
    public async Task Host_Sent_S1F17_Acks_Then_The_Listener_Switches_Control_State()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        ISecsGemRuntime service = runtime;
        await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());
        await LetControlListenerSettle();

        await SendUntil(transport, 17, () => service.GetState().Mode == SecsGemMode.OnlineRemote);

        Assert.Equal((byte)0, transport.LastReplyItem?.GetMemory<byte>().Span[0]);   // ONLACK = Accepted
        Assert.Equal(SecsGemMode.OnlineRemote, service.GetState().Mode);

        // 再来一次：已在线，应回 1（AlreadyOnline），不是 0（再接受一次）
        await transport.EmitReceivedAsync(new SecsMessage(1, 17, true) { SecsItem = Item.L() });
        Assert.Equal((byte)1, transport.LastReplyItem?.GetMemory<byte>().Span[0]);
    }

    /// <summary>设备正在运行时拒绝上线，ONLACK = 2 (NOT_READY)，且监听器不应切状态。</summary>
    [Fact]
    public async Task Host_Sent_S1F17_Is_Rejected_While_Device_Is_Running()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        ISecsGemRuntime service = runtime;
        await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());
        runtime.StateManager.UpdateDeviceStatus("Status", EDeviceStatus.Running);
        await LetControlListenerSettle();

        await transport.EmitReceivedAsync(new SecsMessage(1, 17, true) { SecsItem = Item.L() });

        Assert.Equal((byte)2, transport.LastReplyItem?.GetMemory<byte>().Span[0]);   // ONLACK = NOT_READY
        await Task.Delay(50);
        Assert.Equal(SecsGemMode.Offline, service.GetState().Mode);
    }

    /// <summary>Host 主动下线：回 OFLACK=0，监听器切 OFFLINE。</summary>
    [Fact]
    public async Task Host_Sent_S1F15_Acks_Then_The_Listener_Transitions_Offline()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        ISecsGemRuntime service = runtime;
        await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());
        await service.GoOnlineRemoteAsync();
        await LetControlListenerSettle();
        Assert.Equal(SecsGemMode.OnlineRemote, service.GetState().Mode);

        await SendUntil(transport, 15, () => service.GetState().Mode == SecsGemMode.Offline);

        Assert.Equal((byte)0, transport.LastReplyItem?.GetMemory<byte>().Span[0]);   // OFLACK = OK
        Assert.Equal(SecsGemMode.Offline, service.GetState().Mode);
    }

    /// <summary>
    /// Host 不回 S6F12 时，监听器必须活着。
    /// <para>
    /// 回归：S6F11 是 wait=true，Host 不回就挂到 T3 超时（secs4net 抛 SecsException）。
    /// 这三个方法正是 <c>WaitOnlineAndOfflineAsync</c> 的等待者，异常一旦冲出那个 while 循环，
    /// 监听就彻底结束 —— 之后 Host 再发 S1F15 全部石沉大海，表现为"下线收不到"且无任何报错。
    /// 真机上尤其容易触发：CEID 没被 Host 订阅时 Host 就不回 S6F12。
    /// </para>
    /// </summary>
    [Fact]
    public async Task Control_Listener_Survives_A_Host_That_Never_Acks_The_Event_Report()
    {
        var transport = new InMemorySecsTransport { HangOnOrder = "S6F11" };   // 永远不回 S6F12
        using var runtime = RuntimeTestFixture.Create(transport);
        ISecsGemRuntime service = runtime;
        await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());
        await LetControlListenerSettle();

        // 第一条：S1F17 上线。S6F11 会一直挂着。
        await SendUntil(transport, 17, () => service.GetState().Mode == SecsGemMode.OnlineRemote);
        Assert.Equal(SecsGemMode.OnlineRemote, service.GetState().Mode);

        // 第二条：S1F15 下线仍必须被听见并回 S1F16
        await SendUntil(transport, 15, () => service.GetState().Mode == SecsGemMode.Offline);
        Assert.Equal((byte)0, transport.LastReplyItem?.GetMemory<byte>().Span[0]);   // OFLACK = OK
        Assert.Equal(SecsGemMode.Offline, service.GetState().Mode);
    }

    /// <summary>
    /// 反复发同一条主消息直到 <paramref name="done"/> 成立。
    /// <para>
    /// 监听器每处理完一条就注销再注册（<c>WaitOnlineAndOfflineAsync</c> 的循环结构），
    /// 那个空窗期里触发的事件会被丢弃 —— 并行跑整套测试时机器忙，空窗会明显拉长，
    /// 单发一次会偶发丢失。重发是现实里 Host 的做法（没收到 S1F18 本来就该重发），
    /// 而监听器一旦真死了，重发多少次也没用，所以这个断言照样能咬住回归。
    /// </para>
    /// </summary>
    private static async Task SendUntil(InMemorySecsTransport transport, byte f, Func<bool> done, bool w = true)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && !done())
        {
            await transport.EmitReceivedAsync(new SecsMessage(1, f, w) { SecsItem = Item.L() });
            if (done()) return;
            await Task.Delay(100);
        }
    }

    /// <summary>
    /// Host 的主消息不带 W 位时，回不了包，但**必须照常触发事件**。
    /// <para>
    /// 回归现场：Host 明确发了 S1F17/S1F15，<c>ConnectManager</c> 的监听一次都没跑。
    /// 原因是 <c>SecsProtocolEngine.ReceiveAsync</c> 里 <c>await _reply(...)</c> 抛了就把
    /// 后面的 <c>TriggerEvent</c> 一起带走 —— 而 secs4net 的
    /// <c>TryReplyAsync</c> 在 <c>!PrimaryMessage.ReplyExpected</c> 时是<b>抛异常</b>而不是返回 false
    /// （PrimaryMessageWrapper.cs:27），所以只要 Host 没置 W 位就必挂。
    /// </para>
    /// </summary>
    [Fact]
    public async Task Host_Control_Messages_Without_The_W_Bit_Are_Still_Processed()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        ISecsGemRuntime service = runtime;
        await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());
        await LetControlListenerSettle();

        // w: false —— Host 不要求回包
        await SendUntil(transport, 17, () => service.GetState().Mode == SecsGemMode.OnlineRemote, w: false);
        Assert.Equal(SecsGemMode.OnlineRemote, service.GetState().Mode);
        Assert.Null(transport.LastReplyItem);      // 确实没回包

        await SendUntil(transport, 15, () => service.GetState().Mode == SecsGemMode.Offline, w: false);
        Assert.Equal(SecsGemMode.Offline, service.GetState().Mode);
    }

    /// <summary>
    /// 会话未建立时要求等应答的发送必须 loud 失败，不能当成功。
    /// <para>
    /// 回归现场："发送正常"，实际 Host 什么都没收到。原因是
    /// <c>Secs4NetTransport.Send</c> 在没 Selected/连接断开时 early-return null 而不是抛，
    /// 引擎原来不检查，直接按成功返回。标准 Host 在 Selected 之前本来就不会发任何数据，
    /// 于是收发两边一起"正常"地没动静，谁也看不出会话根本没建起来。
    /// wait=false 的即发即忘保持原语义。
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Send_With_No_Session_Fails_Loudly_When_Reply_Is_Expected(bool wait)
    {
        var transport = new InMemorySecsTransport { NullOnOrder = "S1F13" };
        using var runtime = RuntimeTestFixture.Create(transport);

        if (wait)
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => runtime.SendItem("S1F13", true));
            Assert.Contains("Selected", ex.Message);
        }
        else
        {
            await runtime.SendItem("S1F13", false);   // 不抛
        }
    }

    /// <summary>
    /// S1F3/S1F11 回之前必须先刷系统变量。
    /// <para>
    /// 回归：<c>SecsDataBinding.Refresh</c> 没配 provider 就直接 return，
    /// Clock/DeviceMode/DeviceStatus 永远是 CSV 里的陈旧值（默认项里甚至是 null），
    /// S1F3 回出去全是死的。S1F11 虽然只回名录不回值，但走同一个 Refresh，
    /// 刷完后 SVID 表里的值也得是新的。
    /// </para>
    /// </summary>
    [Fact]
    public async Task S1F3_And_S1F11_Refresh_System_Vids_Before_Replying()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var catalog = runtime.VariableManager;
        var empty = Path.Combine(Path.GetTempPath(), "SecsGemTests", Guid.NewGuid().ToString("N"));
        catalog.SVID.FilePath = empty;
        catalog.DVID.FilePath = empty;
        catalog.ECID.FilePath = empty;
        catalog.CEID.FilePath = empty;
        catalog.ReportID.FilePath = empty;
        try
        {
            runtime.Config();   // 默认项 + StatusManager 四个名字
            ISecsGemRuntime service = runtime;
            await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());
            Assert.True(await service.GoOnlineRemoteAsync());
            runtime.StateManager.UpdateDeviceStatus("DeviceStatus", EDeviceStatus.Running);

            // S1F3 点名要 101/102/103
            await transport.EmitReceivedAsync(new SecsMessage(1, 3, true)
            {
                SecsItem = Item.L(Item.U2(101), Item.U2(102), Item.U2(103))
            });

            var reply = transport.LastReplyItem!;
            Assert.Equal(3, reply.Items.Length);
            Assert.Equal(17, reply.Items[0].GetString().Length);                       // Clock=当前时间，不是 null
            Assert.Equal((ushort)ESecsMode.Online_Remote, reply.Items[1].GetMemory<ushort>().Span[0]);
            Assert.Equal((ushort)EDeviceStatus.Running, reply.Items[2].GetMemory<ushort>().Span[0]);

            // S1F11：把 Clock 值清掉再查，查完必须被刷回来
            catalog.UpdateVID("Clock", null);
            await transport.EmitReceivedAsync(new SecsMessage(1, 11, true) { SecsItem = Item.L() });
            Assert.NotNull(transport.LastReplyItem);   // 名录正常回出
            Assert.False(string.IsNullOrEmpty(catalog.GetVIDByName("Clock")!.Value as string));
        }
        finally
        {
            if (Directory.Exists(empty)) Directory.Delete(empty, recursive: true);
        }
    }

    /// <summary>
    /// S1F23 事件名录：空请求回全部，点名回指定的，不认识的跳过（不断言 S9F7）。
    /// <para>
    /// 回包结构（E5 S1F24）：L[n] &lt;L[3] &lt;U4 CEID&gt; &lt;A CENAME&gt; &lt;L[a] &lt;U4 VID&gt;&gt;&gt;。
    /// CENAME 暂用 Name；每个事件的 VID 取自它挂的报表（去重保序）。
    /// 设备端不发 S1F23（Host→Equipment），发就报组包失败。
    /// </para>
    /// </summary>
    [Fact]
    public async Task S1F23_Replies_The_Collection_Event_Namelist()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var empty = Path.Combine(Path.GetTempPath(), "SecsGemTests", Guid.NewGuid().ToString("N"));
        var catalog = runtime.VariableManager;
        catalog.SVID.FilePath = empty;
        catalog.DVID.FilePath = empty;
        catalog.ECID.FilePath = empty;
        catalog.CEID.FilePath = empty;
        catalog.ReportID.FilePath = empty;
        try
        {
            runtime.Config();   // 默认项：CEID 101-105，report 1/11/12/13
            ISecsGemRuntime service = runtime;
            await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());

            // 空请求 = 全要：5 条事件
            await transport.EmitReceivedAsync(new SecsMessage(1, 23, true) { SecsItem = Item.L() });
            var all = transport.LastReplyItem!;
            Assert.Equal(5, all.Items.Length);

            // CEID 102 Device Mode：report "1,13,11" → VID 101,104,105,102
            var mode = all.Items.Single(e => e.Items[0].GetMemory<uint>().Span[0] == 102);
            Assert.Equal("Device Mode", mode.Items[1].GetString());
            Assert.Equal(new uint[] { 101, 104, 105, 102 },
                mode.Items[2].Items.Select(v => v.GetMemory<uint>().Span[0]));

            // 点名要 103：只回一条
            await transport.EmitReceivedAsync(new SecsMessage(1, 23, true)
            {
                SecsItem = Item.L(Item.U4(103))
            });
            var one = transport.LastReplyItem!;
            Assert.Single(one.Items);
            Assert.Equal(103u, one.Items[0].Items[0].GetMemory<uint>().Span[0]);
            Assert.Equal("Device Online", one.Items[0].Items[1].GetString());

            // 不认识的 CEID：跳过，回空列表而不是 S9F7
            await transport.EmitReceivedAsync(new SecsMessage(1, 23, true)
            {
                SecsItem = Item.L(Item.U4(999))
            });
            Assert.Equal(0, transport.LastReplyItem!.Items.Length);

            // 设备端不发 S1F23
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.SendItem("S1F23", false));
            Assert.Contains("组包失败", ex.Message);
        }
        finally
        {
            if (Directory.Exists(empty)) Directory.Delete(empty, recursive: true);
        }
    }

    /// <summary>
    /// 本设备 S1F3 按全表解析 ID：Host 点名 DVID/ECID 也能查到，按请求顺序回值。
    /// 前提是 ID 跨表全局唯一（<c>VIDManager.Load</c> 强制校验），裸 ID 无歧义。
    /// </summary>
    [Fact]
    public async Task S1F3_Resolves_Dvid_And_Ecid_By_Id_In_Request_Order()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var catalog = runtime.VariableManager;
        var empty = Path.Combine(Path.GetTempPath(), "SecsGemTests", Guid.NewGuid().ToString("N"));
        catalog.SVID.FilePath = empty;
        catalog.DVID.FilePath = empty;
        catalog.ECID.FilePath = empty;
        catalog.CEID.FilePath = empty;
        catalog.ReportID.FilePath = empty;
        try
        {
            runtime.Config();   // 默认项：SVID 101-103 + ECID 104/105
            var dvid = new VIDInfo { ID = "200", Name = "SubstrateID", Value = "S1", DataType = SecsFormat.ASCII, Category = "DVID" };
            catalog.DVID.Datas.Add(dvid);
            catalog.VIDList.Add(dvid);
            catalog.VIDDic_Name[dvid.Name] = dvid;
            catalog.VIDDic_ID[dvid.ID] = dvid;
            catalog.UpdateVID("DeviceName", "Packing_001");   // 对应产线 ECID.csv 里同名变量的值

            ISecsGemRuntime service = runtime;
            await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());
            Assert.True(await service.GoOnlineRemoteAsync());   // live 模式=2，S1F3 回的必须是它而不是手填值

            // 故意打乱类别顺序点名：DVID、ECID、SVID 各一个
            await transport.EmitReceivedAsync(new SecsMessage(1, 3, true)
            {
                SecsItem = Item.L(Item.U4(200), Item.U4(104), Item.U4(102))
            });

            var reply = transport.LastReplyItem!;
            Assert.Equal(3, reply.Items.Length);
            Assert.Equal("S1", reply.Items[0].GetString());
            Assert.Equal("Packing_001", reply.Items[1].GetString());   // ECID 默认值（MachineApp 配置同名）
            Assert.Equal((ushort)2, reply.Items[2].GetMemory<ushort>().Span[0]);
        }
        finally
        {
            if (Directory.Exists(empty)) Directory.Delete(empty, recursive: true);
        }
    }

    /// <summary>
    /// S2F33 改报表挂的变量，S2F35 改事件挂的报表（不是报表挂的变量）。
    /// <para>
    /// 回归：S2F35 原实现误调 <c>ReportID.Modify(eventid, …)</c>，事件 ID 在报表里
    /// 永远找不到，Host 的链接请求静默无事发生，还回 Success。
    /// </para>
    /// </summary>
    [Fact]
    public async Task S2F33_Changes_Report_Vids_And_S2F35_Links_Event_To_Report()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var catalog = runtime.VariableManager;
        var empty = Path.Combine(Path.GetTempPath(), "SecsGemTests", Guid.NewGuid().ToString("N"));
        catalog.SVID.FilePath = empty;
        catalog.DVID.FilePath = empty;
        catalog.ECID.FilePath = empty;
        catalog.CEID.FilePath = empty;
        catalog.ReportID.FilePath = empty;
        try
        {
            runtime.Config();   // 默认项：report 11→"102"，CEID 102→"1,13,11"
            ISecsGemRuntime service = runtime;
            await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());

            // S2F33：DATAID=1，RPTID=11，VID 改成 103
            await transport.EmitReceivedAsync(new SecsMessage(2, 33, true)
            {
                SecsItem = Item.L(Item.U2(1), Item.U2(11), Item.U2(103))
            });
            Assert.Equal((byte)0, transport.LastReplyItem?.GetMemory<byte>().Span[0]);
            Assert.Equal("103", catalog.ReportID.GetDatas().Single(x => x.ID == "11").VID_Collection);

            // S2F35：DATAID=1，CEID=102，挂 report 12
            await transport.EmitReceivedAsync(new SecsMessage(2, 35, true)
            {
                SecsItem = Item.L(Item.U2(1), Item.U2(102), Item.U2(12))
            });
            Assert.Equal((byte)0, transport.LastReplyItem?.GetMemory<byte>().Span[0]);
            Assert.Equal("12", catalog.CEID.GetDatas().Single(x => x.ID == "102").ReportID_Collection);
        }
        finally
        {
            if (Directory.Exists(empty)) Directory.Delete(empty, recursive: true);
        }
    }

    /// <summary>
    /// 连发两条 S2F33 / S2F35 时的关联图：
    /// 重复 VID 不进表；第一条改完不污染第二条（不出现"全部挂到第一个下面"）。
    /// </summary>
    [Fact]
    public async Task S2F33_And_S2F35_Stay_Clean_Across_Repeated_Modifies()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var catalog = runtime.VariableManager;
        var empty = Path.Combine(Path.GetTempPath(), "SecsGemTests", Guid.NewGuid().ToString("N"));
        catalog.SVID.FilePath = empty;
        catalog.DVID.FilePath = empty;
        catalog.ECID.FilePath = empty;
        catalog.CEID.FilePath = empty;
        catalog.ReportID.FilePath = empty;
        try
        {
            runtime.Config();
            ISecsGemRuntime service = runtime;
            await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());

            string Rpt(int id) => string.Join("/", catalog.ReportID.GetDatas().Single(x => x.ID == id.ToString()).VIDInfo.Select(v => v.ID));
            string Evt(int id) => string.Join("/", catalog.CEID.GetDatas().Single(x => x.ID == id.ToString()).ReportInfo.Select(r => r.ID));

            // 重复 VID：同一个 ID 点两次，只应留一份
            await transport.EmitReceivedAsync(new SecsMessage(2, 33, true)
            {
                SecsItem = Item.L(Item.U2(1), Item.U2(11), Item.L(Item.U2(103), Item.U2(103), Item.U2(104)))
            });
            Assert.Equal("103/104", Rpt(11));

            // 第二条 S2F33：改别的报表，11 不能被带偏
            await transport.EmitReceivedAsync(new SecsMessage(2, 33, true)
            {
                SecsItem = Item.L(Item.U2(1), Item.U2(12), Item.L(Item.U2(105)))
            });
            Assert.Equal("103/104", Rpt(11));
            Assert.Equal("105", Rpt(12));

            // 第一条 S2F35
            await transport.EmitReceivedAsync(new SecsMessage(2, 35, true)
            {
                SecsItem = Item.L(Item.U2(1), Item.U2(102), Item.L(Item.U2(12)))
            });
            Assert.Equal("12", Evt(102));

            // 第二条 S2F35：改另一个事件，102 不能被带偏
            await transport.EmitReceivedAsync(new SecsMessage(2, 35, true)
            {
                SecsItem = Item.L(Item.U2(1), Item.U2(103), Item.L(Item.U2(13)))
            });
            Assert.Equal("12", Evt(102));
            Assert.Equal("13", Evt(103));

            // 落盘也要对：第二遍不能把第一遍的关联盖掉（重启后仍是这样）
            string rptCsv = File.ReadAllText(Path.Combine(empty, "ReportID.csv"));
            string ceidCsv = File.ReadAllText(Path.Combine(empty, "CEID.csv"));
            Assert.Contains("11,DeviceMode,\"103,104\"", rptCsv.Replace("\r\n", "|"));
            Assert.Contains("12,DeviceStatus,105", rptCsv.Replace("\r\n", "|"));
            Assert.Contains("102,Device Mode,12,", ceidCsv.Replace("\r\n", "|"));
            Assert.Contains("103,Device Online,13,", ceidCsv.Replace("\r\n", "|"));

            // 静默失败是第二条链接"看起来没生效"的根源：报表/事件不存在时必须转 S9F7，
            // 不能回 Success —— Host 会以为挂上了，数据其实还挂在第一个报表下面。
            await transport.EmitReceivedAsync(new SecsMessage(2, 33, true)
            {
                SecsItem = Item.L(Item.U2(1), Item.U2(77), Item.L(Item.U2(104)))
            });
            Assert.Null(transport.LastReplyItem);
            await transport.EmitReceivedAsync(new SecsMessage(2, 35, true)
            {
                SecsItem = Item.L(Item.U2(1), Item.U2(999), Item.L(Item.U2(11)))
            });
            Assert.Null(transport.LastReplyItem);

            // 报错不能污染已有链接：11 仍然是 103/104，102 仍然挂 12
            Assert.Equal("103/104", Rpt(11));
            Assert.Equal("12", Evt(102));
        }
        finally
        {
            if (Directory.Exists(empty)) Directory.Delete(empty, recursive: true);
        }
    }

    /// <summary>
    /// 一条 S2F33 / S2F35 里带多组定义（产线 Host 实测报文形状）。
    /// <para>
    /// 回归：原来用 SecsItemToList 平铺取 data[0]/data[1]/Skip(2)，键和值的对应关系全丢 ——
    /// 第二组的 RPTID 被当成 VID 塞进第一组报表（S2F33），或第二个 CEID 被当成报表挂上去（S2F35），
    /// 结果报 S9F7，现场看起来就是"第二条链接不生效、变量全挂在第一个报表下面"。
    /// </para>
    /// </summary>
    [Fact]
    public async Task S2F33_And_S2F35_Accept_Multiple_Groups_In_One_Message()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var catalog = runtime.VariableManager;
        var empty = Path.Combine(Path.GetTempPath(), "SecsGemTests", Guid.NewGuid().ToString("N"));
        catalog.SVID.FilePath = empty;
        catalog.DVID.FilePath = empty;
        catalog.ECID.FilePath = empty;
        catalog.CEID.FilePath = empty;
        catalog.ReportID.FilePath = empty;
        try
        {
            runtime.Config();
            ISecsGemRuntime service = runtime;
            await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());

            string Rpt(int id) => string.Join("/", catalog.ReportID.GetDatas().Single(x => x.ID == id.ToString()).VIDInfo.Select(v => v.ID));
            string Evt(int id) => string.Join("/", catalog.CEID.GetDatas().Single(x => x.ID == id.ToString()).ReportInfo.Select(r => r.ID));

            // 三种形状都要认（成员平铺 / 标准 L[3] / Host 的两层多组），这里逐个发一遍：
            //   a) L[3] <1> <13> <104> <104>            成员平铺，无内层 L
            //   b) L[2] <1> <La>                        外层 DATAID 与 La 平级
            //        La  L2b<13, Lb<104,104>>
            //            L2b<12, Lb<103,104>>          每组只有 RPTID + Lb 两项
            await transport.EmitReceivedAsync(new SecsMessage(2, 33, true)
            {
                SecsItem = Item.L(Item.U2(1), Item.U2(13), Item.U2(104), Item.U2(104))
            });
            Assert.NotNull(transport.LastReplyItem);
            Assert.Equal("104", Rpt(13));   // 平铺写法里的重复 ID 也要去重

            await transport.EmitReceivedAsync(new SecsMessage(2, 33, true)
            {
                SecsItem = Item.L(
                    Item.U2(1),
                    Item.L(
                        Item.L(Item.U2(13), Item.L(Item.U2(104), Item.U2(104))),
                        Item.L(Item.U2(12), Item.L(Item.U2(103), Item.U2(104)))))
            });
            Assert.NotNull(transport.LastReplyItem);   // 两组都认，不会 S9F7
            Assert.Equal((byte)0, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
            Assert.Equal("104", Rpt(13));        // 组内重复 VID 去重，且 13 丢掉了原有的 105
            Assert.Equal("103/104", Rpt(12));    // 第二组的数据不许跑到第一组去

            // S2F36: L[2] <L[3] <U2 1> <U2 105> <L[2] <U2 1> <U2 13>>>
            //            <L[3] <U2 1> <U2 104> <L[2] <U2 1> <U2 13>>>>
            await transport.EmitReceivedAsync(new SecsMessage(2, 35, true)
            {
                SecsItem = Item.L(
                    Item.U2(1),
                    Item.L(
                        Item.L(Item.U2(105), Item.L(Item.U2(1), Item.U2(13))),
                        Item.L(Item.U2(104), Item.L(Item.U2(12)))))
            });
            Assert.NotNull(transport.LastReplyItem);
            Assert.Equal("1/13", Evt(105));   // 105 不再挂着默认的 11
            Assert.Equal("12", Evt(104));    // 第二组的数据不许跑到第一组去

            // 标准 L[3] 写法（成员带内层 L）也要认
            await transport.EmitReceivedAsync(new SecsMessage(2, 35, true)
            {
                SecsItem = Item.L(Item.U2(1), Item.U2(105), Item.L(Item.U2(13), Item.U2(11)))
            });
            Assert.Equal("13/11", Evt(105));
        }
        finally
        {
            if (Directory.Exists(empty)) Directory.Delete(empty, recursive: true);
        }
    }

    /// <summary>
    /// 引用不成立时整条不改、也不落盘（S2F33/S2F35 同一条规则）。
    /// <para>
    /// 回归：原顺序是"先 Modify 写盘、再由 Update 抛"，含无效 VID 的半截配置进了 CSV，
    /// 之后每次组包（S6F11/S1F23）都在同一处失败 —— 现场看着像配置整体坏了。
    /// 多组时也必须"全有或全无"，不能前面几组生效、后面一组失败。
    /// </para>
    /// </summary>
    [Fact]
    public async Task S2F33_And_S2F35_Apply_Nothing_When_A_Reference_Is_Bad()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var catalog = runtime.VariableManager;
        var empty = Path.Combine(Path.GetTempPath(), "SecsGemTests", Guid.NewGuid().ToString("N"));
        catalog.SVID.FilePath = empty;
        catalog.DVID.FilePath = empty;
        catalog.ECID.FilePath = empty;
        catalog.CEID.FilePath = empty;
        catalog.ReportID.FilePath = empty;
        try
        {
            runtime.Config();
            ISecsGemRuntime service = runtime;
            await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());

            // 第一组合法、第二组含不存在的 VID：两组都不能生效
            await transport.EmitReceivedAsync(new SecsMessage(2, 33, true)
            {
                SecsItem = Item.L(
                    Item.U2(1),
                    Item.L(
                        Item.L(Item.U2(11), Item.L(Item.U2(103))),
                        Item.L(Item.U2(12), Item.L(Item.U4(999999)))))
            });
            Assert.Null(transport.LastReplyItem);                                  // S9F7
            Assert.Equal("102", Rpt(catalog, 11));                                 // 11 没被改
            Assert.Equal("103", Rpt(catalog, 12));                                 // 12 也没轮到
            // 配置表只有落盘才会生成 CSV；失败路径不应落盘（旧行为是先写盘再抛）。
            Assert.False(File.Exists(Path.Combine(empty, "ReportID.csv")), "引用不成立时不应落盘 ReportID.csv");

            // 同理，报表不存在也不改任何一组
            await transport.EmitReceivedAsync(new SecsMessage(2, 33, true)
            {
                SecsItem = Item.L(
                    Item.U2(1),
                    Item.L(
                        Item.L(Item.U2(11), Item.L(Item.U2(104))),
                        Item.L(Item.U2(7777), Item.L(Item.U2(104)))))
            });
            Assert.Null(transport.LastReplyItem);
            Assert.Equal("102", Rpt(catalog, 11));

            // S2F35 一样：第二个 CEID 引用不存在的 Report，第一个也不生效
            await transport.EmitReceivedAsync(new SecsMessage(2, 35, true)
            {
                SecsItem = Item.L(
                    Item.U2(1),
                    Item.L(
                        Item.L(Item.U2(102), Item.L(Item.U2(12))),
                        Item.L(Item.U2(103), Item.L(Item.U4(88888)))))
            });
            Assert.Null(transport.LastReplyItem);
            Assert.Equal("1/13/11", Evt(catalog, 102));
            Assert.Equal("1/13/11", Evt(catalog, 103));
            Assert.False(File.Exists(Path.Combine(empty, "CEID.csv")), "引用不成立时不应落盘 CEID.csv");
        }
        finally
        {
            if (Directory.Exists(empty)) Directory.Delete(empty, recursive: true);
        }
    }

    /// <summary>
    /// S2F13 取设备常量：空请求 = 全要、点名按请求顺序回、不存在的 ECID 回 L:0 占位。
    /// 占位而不是丢包，是因为 S2F14 的值与 S2F13 的 ID 必须同序，
    /// 少回一项会让 Host 把后一项的值当成前一项的（错位比缺值更危险）。
    /// </summary>
    [Fact]
    public async Task S2F13_Returns_Ecv_In_Request_Order_And_Placeholder_For_Unknown_Ecid()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var catalog = runtime.VariableManager;
        var empty = Path.Combine(Path.GetTempPath(), "SecsGemTests", Guid.NewGuid().ToString("N"));
        catalog.SVID.FilePath = empty;
        catalog.DVID.FilePath = empty;
        catalog.ECID.FilePath = empty;
        catalog.CEID.FilePath = empty;
        catalog.ReportID.FilePath = empty;
        try
        {
            runtime.Config();   // 默认项：ECID 104 DeviceName / 105 DeviceSoftWare
            await ((ISecsGemRuntime)runtime).ConnectAsync(new SecsGem.Core.SecsGemOptions());
            catalog.UpdateVID("DeviceName", "Packing_001");

            // 空请求 → 全部 ECID
            await transport.EmitReceivedAsync(new SecsMessage(2, 13, true) { SecsItem = Item.L() });
            var all = transport.LastReplyItem!;
            Assert.Equal(2, all.Items.Length);
            Assert.Equal("Packing_001", all.Items[0].GetString());

            // 点名 104 + 一个不存在的 999
            await transport.EmitReceivedAsync(new SecsMessage(2, 13, true)
            {
                SecsItem = Item.L(Item.U4(104), Item.U4(999))
            });
            var reply = transport.LastReplyItem!;
            Assert.Equal(2, reply.Items.Length);
            Assert.Equal("Packing_001", reply.Items[0].GetString());
            Assert.Equal(SecsFormat.List, reply.Items[1].Format);   // 占位 L:0
            Assert.Empty(reply.Items[1].Items);
        }
        finally
        {
            if (Directory.Exists(empty)) Directory.Delete(empty, recursive: true);
        }
    }

    /// <summary>
    /// S2F15 写设备常量：全部认得才写、任一不认识就整条不落并回 EAC=1。
    /// 回归：原先是边校验边写，第一个认得第二个不认得时第一个已经被改掉，
    /// 现场表现是"常量改了一半，Host 却收到拒绝码"。
    /// </summary>
    [Fact]
    public async Task S2F15_Writes_Constants_Only_When_Every_Ecid_Is_Known()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var catalog = runtime.VariableManager;
        var empty = Path.Combine(Path.GetTempPath(), "SecsGemTests", Guid.NewGuid().ToString("N"));
        catalog.SVID.FilePath = empty;
        catalog.DVID.FilePath = empty;
        catalog.ECID.FilePath = empty;
        catalog.CEID.FilePath = empty;
        catalog.ReportID.FilePath = empty;
        try
        {
            runtime.Config();
            await ((ISecsGemRuntime)runtime).ConnectAsync(new SecsGem.Core.SecsGemOptions());

            // 合法一对：104 = "LOT-A"
            await transport.EmitReceivedAsync(new SecsMessage(2, 15, true)
            {
                SecsItem = Item.L(Item.L(Item.U4(104), Item.A("LOT-A")))
            });
            Assert.Equal((byte)0, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
            Assert.Equal("LOT-A", catalog.VIDDic_ID["104"].Value);

            // 第二对里 ECID 999 不认识 → EAC=1，且 104 不再被改
            await transport.EmitReceivedAsync(new SecsMessage(2, 15, true)
            {
                SecsItem = Item.L(
                    Item.L(Item.U4(104), Item.A("LOT-B")),
                    Item.L(Item.U4(999), Item.A("X")))
            });
            Assert.Equal((byte)1, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
            Assert.Equal("LOT-A", catalog.VIDDic_ID["104"].Value);
        }
        finally
        {
            if (Directory.Exists(empty)) Directory.Delete(empty, recursive: true);
        }
    }

    /// <summary>
    /// S2F29 回设备常量名录：L[n] L[6]{ECID, ECNAME, ECMIN, ECMAX, ECDEF, UNITS}。
    /// 本仓库 ECID 表没有 MIN/MAX/DEF/UNITS 四列，按标准"未知给空项"回。
    /// </summary>
    [Fact]
    public async Task S2F29_Returns_Namelist_With_Empty_Min_Max_Default_Units()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var catalog = runtime.VariableManager;
        var empty = Path.Combine(Path.GetTempPath(), "SecsGemTests", Guid.NewGuid().ToString("N"));
        catalog.SVID.FilePath = empty;
        catalog.DVID.FilePath = empty;
        catalog.ECID.FilePath = empty;
        catalog.CEID.FilePath = empty;
        catalog.ReportID.FilePath = empty;
        try
        {
            runtime.Config();
            await ((ISecsGemRuntime)runtime).ConnectAsync(new SecsGem.Core.SecsGemOptions());

            await transport.EmitReceivedAsync(new SecsMessage(2, 29, true) { SecsItem = Item.L() });
            var reply = transport.LastReplyItem!;
            Assert.Equal(2, reply.Items.Length);

            var first = reply.Items[0];
            Assert.Equal(6, first.Items.Length);
            Assert.Equal(104u, first.Items[0].GetMemory<uint>().Span[0]);
            Assert.Equal("DeviceName", first.Items[1].GetString());
            Assert.Equal(SecsFormat.ASCII, first.Items[2].Format);   // ECMIN：空 ASCII
            Assert.Empty(first.Items[5].GetString());                // UNITS：空
        }
        finally
        {
            if (Directory.Exists(empty)) Directory.Delete(empty, recursive: true);
        }
    }

    /// <summary>
    /// S2F17 回时间、S2F31 把 Host 下发的时间设到本机时钟（原生 <c>SetLocalTime</c>）。
    /// 两条都用 24 小时制 16 位 <c>yyyyMMddHHmmssff</c>（本设备没定义 TimeFormat ECV，统一取值）。
    /// </summary>
    [Fact]
    public async Task S2F17_And_S2F31_Exchange_Time_In_One_Format()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);

        // 本用例只读默认项，不落盘（无需临时目录）。
        runtime.Config();
        await ((ISecsGemRuntime)runtime).ConnectAsync(new SecsGem.Core.SecsGemOptions());

        await transport.EmitReceivedAsync(new SecsMessage(2, 17, true) { SecsItem = Item.L() });
        var time = transport.LastReplyItem!.GetString();
        Assert.Equal(16, time.Length);
        Assert.True(DateTime.TryParseExact(
            time, "yyyyMMddHHmmssff", CultureInfo.InvariantCulture, DateTimeStyles.None, out _));

        // S2F31 的请求体就是裸 TIME（不是列表）。被设值就用 S2F17 刚回显的当前时间：
        // 持有 SeSystemtimePrivilege 时 SetLocalTime 只是把时钟拨回原位、无副作用；
        // 普通权限进程会失败、按标准回 TIACK=1 —— 两种结果都算通过，不把 0 写死。
        await transport.EmitReceivedAsync(new SecsMessage(2, 31, true) { SecsItem = Item.A(time) });
        byte tiack = transport.LastReplyItem!.GetMemory<byte>().Span[0];
        Assert.True(tiack is 0 or 1, $"S2F31 合法时间应回 TIACK 0(已设置) / 1(缺 SeSystemtimePrivilege)，实际 {tiack}");

        // 格式不认识必须回 1：这条不依赖权限，断言保持确定性。
        await transport.EmitReceivedAsync(new SecsMessage(2, 31, true) { SecsItem = Item.A("not-a-time") });
        Assert.Equal((byte)1, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
    }

    /// <summary>
    /// S2F23 起一条 Trace：TIAACK=0，并按 REPGSZ 分组把 S6F1 周期推给 Host。
    /// <para>
    /// 回归：原实现把 <c>GetData</c> 的方法体整行注释掉、<c>Register</c> 没人调用，
    /// Host 发 S2F23 只拿到一个空应答，一条 S6F1 都收不到。
    /// </para>
    /// <para>
    /// 这里 DSPER 用毫秒写法 "50"（兼容路径）让用例跑得快；标准 hhmmss 的解析在本方法末尾直接断言。
    /// </para>
    /// </summary>
    [Fact]
    public async Task S2F23_Starts_Trace_And_Pushes_S6F1()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var catalog = runtime.VariableManager;
        var empty = Path.Combine(Path.GetTempPath(), "SecsGemTests", Guid.NewGuid().ToString("N"));
        catalog.SVID.FilePath = empty;
        catalog.DVID.FilePath = empty;
        catalog.ECID.FilePath = empty;
        catalog.CEID.FilePath = empty;
        catalog.ReportID.FilePath = empty;
        try
        {
            runtime.Config();
            await ((ISecsGemRuntime)runtime).ConnectAsync(new SecsGem.Core.SecsGemOptions());

            // TRID=7、周期 50ms、总量 2、分组 2 → 一条 S6F1，SV 两项（102/103）
            await transport.EmitReceivedAsync(new SecsMessage(2, 23, true)
            {
                SecsItem = Item.L(
                    Item.U4(7), Item.A("50"), Item.U4(2), Item.U4(2),
                    Item.L(Item.U2(102), Item.U2(103)))
            });
            Assert.Equal((byte)0, transport.LastReplyItem!.GetMemory<byte>().Span[0]);

            await WaitUntil(() => transport.Sent.Any(x => x.Order == "S6F1"));

            var s6f1 = transport.Sent.Single(x => x.Order == "S6F1").Item;
            Assert.Equal(7u, s6f1.Items[0].GetMemory<uint>().Span[0]);   // TRID 与 S2F23 同格式
            Assert.Equal(2u, s6f1.Items[1].GetMemory<uint>().Span[0]);   // SMPLN
            Assert.Equal(16, s6f1.Items[2].GetString().Length);          // STIME
            Assert.Equal(4, s6f1.Items[3].Items.Length);                 // SV：REPGSZ=2 个采样 × 2 个 SVID

            // 标准 DSPER 的解析：hhmmss / hhmmsscc
            Assert.True(TraceManager.TryParseDsper("000010", out var tenSeconds));
            Assert.Equal(10_000, tenSeconds);
            Assert.True(TraceManager.TryParseDsper("00001050", out var tenSecondsHalf));
            Assert.Equal(10_500, tenSecondsHalf);
            Assert.False(TraceManager.TryParseDsper("109999", out _));   // 分钟 99 非法
        }
        finally
        {
            if (Directory.Exists(empty)) Directory.Delete(empty, recursive: true);
        }
    }

    /// <summary>
    /// S2F23 的三个拒绝码 + 终止：周期非法回 3、SVID 未知回 4、REPGSZ 非法回 5；
    /// TOTSMP=0 终止该 TRID 后不再产生 S6F1。
    /// </summary>
    [Fact]
    public async Task S2F23_Rejects_Bad_Period_Unknown_Svid_And_Bad_Repgsz_And_Terminates()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var catalog = runtime.VariableManager;
        var empty = Path.Combine(Path.GetTempPath(), "SecsGemTests", Guid.NewGuid().ToString("N"));
        catalog.SVID.FilePath = empty;
        catalog.DVID.FilePath = empty;
        catalog.ECID.FilePath = empty;
        catalog.CEID.FilePath = empty;
        catalog.ReportID.FilePath = empty;
        try
        {
            runtime.Config();
            await ((ISecsGemRuntime)runtime).ConnectAsync(new SecsGem.Core.SecsGemOptions());

            // 周期非法（10:99:99）
            await transport.EmitReceivedAsync(new SecsMessage(2, 23, true)
            {
                SecsItem = Item.L(Item.U4(1), Item.A("109999"), Item.U4(1), Item.U4(1), Item.L(Item.U2(102)))
            });
            Assert.Equal((byte)3, transport.LastReplyItem!.GetMemory<byte>().Span[0]);

            // SVID 未知
            await transport.EmitReceivedAsync(new SecsMessage(2, 23, true)
            {
                SecsItem = Item.L(Item.U4(2), Item.A("50"), Item.U4(1), Item.U4(1), Item.L(Item.U4(99999)))
            });
            Assert.Equal((byte)4, transport.LastReplyItem!.GetMemory<byte>().Span[0]);

            // REPGSZ 非法
            await transport.EmitReceivedAsync(new SecsMessage(2, 23, true)
            {
                SecsItem = Item.L(Item.U4(3), Item.A("50"), Item.U4(5), Item.A("abc"), Item.L(Item.U2(102)))
            });
            Assert.Equal((byte)5, transport.LastReplyItem!.GetMemory<byte>().Span[0]);

            // 拒绝的三条一条 S6F1 都不该发出去
            Assert.DoesNotContain(transport.Sent, x => x.Order == "S6F1");

            // 起一条长 Trace：周期 50ms、总量 100、每采样一条
            await transport.EmitReceivedAsync(new SecsMessage(2, 23, true)
            {
                SecsItem = Item.L(Item.U4(9), Item.A("50"), Item.U4(100), Item.U4(1), Item.L(Item.U2(102)))
            });
            Assert.Equal((byte)0, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
            await WaitUntil(() => transport.Sent.Any(x => x.Order == "S6F1"));

            // TOTSMP=0 终止同一个 TRID
            await transport.EmitReceivedAsync(new SecsMessage(2, 23, true)
            {
                SecsItem = Item.L(Item.U4(9), Item.A("50"), Item.U4(0), Item.U4(1), Item.L(Item.U2(102)))
            });
            Assert.Equal((byte)0, transport.LastReplyItem!.GetMemory<byte>().Span[0]);

            // 先给在途的一条落地，再看两个采样窗口里条数是否不再增长
            await Task.Delay(250);
            int settled = transport.Sent.Count(x => x.Order == "S6F1");
            await Task.Delay(250);
            Assert.Equal(settled, transport.Sent.Count(x => x.Order == "S6F1"));
        }
        finally
        {
            if (Directory.Exists(empty)) Directory.Delete(empty, recursive: true);
        }
    }

    private static string Rpt(VIDManager catalog, int id)
    {
        // ReportInfo.VIDInfo 是按需重建的派生视图（S6F11/S1F23 才会先刷新），这里读之前先补一次。
        catalog.UpdateCEID();
        return string.Join("/", catalog.ReportID.GetDatas().Single(x => x.ID == id.ToString()).VIDInfo.Select(v => v.ID));
    }

    private static string Evt(VIDManager catalog, int id)
    {
        catalog.UpdateCEID();
        return string.Join("/", catalog.CEID.GetDatas().Single(x => x.ID == id.ToString()).ReportInfo.Select(r => r.ID));
    }

    /// <summary>监听在后台跑，等它真正切完状态，最多 2 秒。</summary>
    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(10);
        }
    }

    /// <summary>
    /// 控制消息监听是 Task.Run 起的后台循环，靠 EventHelper 动态注册 handler；
    /// 事件在注册之前触发就会被丢弃。生产上 S1F17 远晚于连接，不成问题；
    /// 测试里连接一返回就注入消息，必须先给监听一个就位窗口。
    /// shortcut: 固定 150ms，若监听启动变慢请改为可观测的就位信号。
    /// </summary>
    private static Task LetControlListenerSettle() => Task.Delay(150);

    /// <summary>
    /// S5F3：单条、标准多条列表、空列表（全量开关）都要改 Enable 并回 B(0)。
    /// 原实现只读 data[1]：多条时只生效第一条，空列表直接越界进 S9F7。
    /// </summary>
    [Fact]
    public async Task S5F3_DisableAndEnable_Single_Multi_And_All_Alarms()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        ISecsGemRuntime service = runtime;
        await service.ConnectAsync(new SecsGem.Core.SecsGemOptions());
        var alarm = runtime.VariableManager.Alarm;
        var root = Path.Combine(Path.GetTempPath(), "secs_s5f3_" + Guid.NewGuid().ToString("N"));
        var originalPath = alarm.FilePath;
        alarm.FilePath = Path.Combine(root, "Alarm");
        try
        {
            alarm.Datas.Add(new AlarmInfo { ID = "11", Description = "A", Enable = true });
            alarm.Datas.Add(new AlarmInfo { ID = "12", Description = "B", Enable = true });

            // 单条禁用
            await transport.EmitReceivedAsync(new SecsMessage(5, 3, true)
            {
                SecsItem = Item.L(Item.B(0), Item.L(Item.A("11")))
            });
            Assert.Equal((byte)0, transport.LastReplyItem.GetMemory<byte>().Span[0]);
            Assert.False(alarm.Datas.First(x => x.ID == "11").Enable);
            Assert.True(alarm.Datas.First(x => x.ID == "12").Enable);

            // 标准多条列表一次全禁
            await transport.EmitReceivedAsync(new SecsMessage(5, 3, true)
            {
                SecsItem = Item.L(Item.B(0), Item.L(Item.A("11"), Item.A("12")))
            });
            Assert.Equal((byte)0, transport.LastReplyItem.GetMemory<byte>().Span[0]);
            Assert.True(alarm.Datas.All(x => !x.Enable));

            // 空列表 = 全量启用
            await transport.EmitReceivedAsync(new SecsMessage(5, 3, true)
            {
                SecsItem = Item.L(Item.B(1), Item.L())
            });
            Assert.Equal((byte)0, transport.LastReplyItem.GetMemory<byte>().Span[0]);
            Assert.True(alarm.Datas.All(x => x.Enable));
            Assert.True(File.Exists(Path.Combine(root, "Alarm", "Alarm.csv")));
        }
        finally
        {
            alarm.Datas.Clear();
            alarm.FilePath = originalPath;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
