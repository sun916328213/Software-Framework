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
}
