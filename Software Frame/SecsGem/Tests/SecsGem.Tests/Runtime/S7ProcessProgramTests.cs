using Secs4Net;
using Secs4Net.Sml;
using SecsGem.Runtime.Manager;
using SecsGem.Runtime.Order;
using SecsGem.Runtime.Order.S7;
using SecsGem.Runtime.Protocol.Registry;
using SecsGem.Tests.Fakes;
using SecsGem.Tests.Infrastructure;
using Xunit;

namespace SecsGem.Tests.Runtime;

/// <summary>离线 SECS-II S7 契约测试，无套接字、SDK、文件或设备写入。
/// 结构期望参考 common.sml 的 S7 样例，不由被测编码器生成。</summary>
public sealed class S7ProcessProgramTests
{
    private readonly DeviceManager _device;
    private readonly SecsMessageRegistry _registry;

    public S7ProcessProgramTests()
    {
        var container = new TestPrismContainer();
        _device = new DeviceManager(container);
        container.RegisterInstance(typeof(DeviceManager), _device);
        _registry = new SecsMessageRegistry(container);
        _registry.AutoRegisterHandlers();
    }

    private SecsOrderBase Handler(int function) => _registry.GetHandler($"S7F{function}");
    private Item Reply(int function, Item? item = null) => Handler(function % 2 == 0 ? function - 1 : function).GetReplyItem(
        new SecsMessage(7, (byte)function, true) { SecsItem = item });

    private static void Ack(Item item, byte expected)
    {
        Assert.NotNull(item);
        Assert.Equal(SecsFormat.Binary, item.Format);
        Assert.Equal(new[] { expected }, item.GetMemory<byte>().ToArray());
    }

    private static Item Formatted(string id = "R1") => Item.L(
        Item.A(id), Item.A("MODEL"), Item.A("1.2"),
        Item.L(Item.L(Item.U2(12), Item.L(
            Item.F4(1.25f, -2.5f), Item.F8(3.125, 4.5), Item.U1(7, 8),
            Item.I4(-1, 2), Item.Boolean(true, false), Item.B(0, 255),
            Item.U2(), Item.A("PARAM")))));

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(17)]
    [InlineData(19)]
    [InlineData(23)]
    [InlineData(25)]
    public void Common_Primaries_Are_Registered(int function) => Assert.NotNull(Handler(function));

    [Fact]
    public void S7F20_Is_A_Flat_List_Of_Ppids_Not_Recipe_Bodies()
    {
        _device.UpdateProcessProgramDirectory(new()
        {
            new() { PPID = "R1", PPBODY = "must-not-leak" }, new() { PPID = "R2" }
        });
        var reply = Reply(19);
        Assert.Equal(SecsFormat.List, reply.Format);
        Assert.All(reply.Items, item => Assert.Equal(SecsFormat.ASCII, item.Format));
        Assert.Equal(new[] { "R1", "R2" }, reply.Items.Select(i => i.GetString()).OrderBy(x => x));
    }

    [Fact]
    public void S7F19_Is_Header_Only_And_An_Empty_Directory_Is_L0()
    {
        Assert.Empty(Reply(19).Items);
        Assert.Null(Reply(19, Item.L()));
    }

    [Fact]
    public void S7F25_Request_Is_A_Ppid_Not_L1()
    {
        var request = Handler(25).GetSendItem("R1");
        Assert.Equal(SecsFormat.ASCII, request.Format);
        Assert.Equal("R1", request.GetString());
        Assert.Null(Reply(25, Item.L(Item.A("R1"))));
    }

    [Fact]
    public void Formatted_Download_Then_Upload_Preserves_The_Four_Fields_And_All_Parameter_Values()
    {
        using var incoming = Formatted();
        Ack(Reply(23, incoming), 0);
        incoming.Items[3].Items[0].Items[1].Items[0].GetMemory<float>().Span[0] = 999;
        Assert.Equal("R1", Assert.Single(Reply(19).Items).GetString());
        var reply = Reply(25, Item.A("R1"));
        Assert.Equal(SecsFormat.List, reply.Format);
        Assert.Equal(4, reply.Count);
        Assert.Equal("R1", reply.Items[0].GetString());
        Assert.Equal("MODEL", reply.Items[1].GetString());
        Assert.Equal("1.2", reply.Items[2].GetString());
        var command = Assert.Single(reply.Items[3].Items);
        Assert.Equal((ushort)12, command.Items[0].GetMemory<ushort>().Span[0]);
        var parameters = command.Items[1].Items;
        Assert.Equal(new[] { 1.25f, -2.5f }, parameters[0].GetMemory<float>().ToArray());
        Assert.Equal(new[] { 3.125, 4.5 }, parameters[1].GetMemory<double>().ToArray());
        Assert.Equal(SecsFormat.U1, parameters[2].Format);
        Assert.Equal(new byte[] { 7, 8 }, parameters[2].GetMemory<byte>().ToArray());
        Assert.Equal(new[] { -1, 2 }, parameters[3].GetMemory<int>().ToArray());
        Assert.Equal(new[] { true, false }, parameters[4].GetMemory<bool>().ToArray());
        Assert.Equal(SecsFormat.Binary, parameters[5].Format);
        Assert.Equal(new byte[] { 0, 255 }, parameters[5].GetMemory<byte>().ToArray());
        Assert.Equal(SecsFormat.U2, parameters[6].Format);
        Assert.Equal(0, parameters[6].Count);
        Assert.Equal("PARAM", parameters[7].GetString());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 7)]
    [InlineData(17, 33)]
    public void Different_Command_And_Parameter_Counts_Roundtrip_Without_Truncation(int commandCount, int parameterCount)
    {
        Item Parameter(int index) => (index % 7) switch
        {
            0 => Item.A($"TEXT-{index}"),
            1 => Item.I4(-index, 0, index),
            2 => Item.F4(1.25f, -2.5f),
            3 => Item.Boolean(true, false, true),
            4 => Item.B(0, 128, 255),
            5 => Item.L(Item.A("NESTED"), Item.L(Item.U8(ulong.MaxValue), Item.I2(-3, 4))),
            _ => Item.U2() // 空向量必须保留，不能丢弃。
        };
        var commands = Enumerable.Range(0, commandCount).Select(index =>
            Item.L(Item.A($"STEP-{index}"), Item.L(Enumerable.Range(0, parameterCount + index).Select(Parameter))));
        var body = Item.L(Item.A("MULTI-PROFILE"), Item.A("PROFILE-MODEL"), Item.A("3.0"), Item.L(commands));
        Ack(Reply(23, body), 0);
        string expected;
        using (var source = new SecsMessage(7, 26) { SecsItem = body }) expected = source.ToSml();
        using var actual = new SecsMessage(7, 26) { SecsItem = Reply(25, Item.A("MULTI-PROFILE")) };
        Assert.Equal(expected, actual.ToSml());
        Assert.Equal(commandCount, actual.SecsItem!.Items[3].Count);
        for (var i = 0; i < commandCount; i++)
            Assert.Equal(parameterCount + i, actual.SecsItem.Items[3].Items[i].Items[1].Count);
        if (commandCount > 0)
        {
            actual.SecsItem.Items[3].Items[0].Items[1] = Item.L();
            using var reread = new SecsMessage(7, 26) { SecsItem = Reply(25, Item.A("MULTI-PROFILE")) };
            Assert.Equal(expected, reread.ToSml()); // 修改读取结果不能污染协议库。
        }
    }

    public static IEnumerable<object[]> OpaqueCommandCodes()
    {
        yield return new object[] { Item.A("1") };
        yield return new object[] { Item.A("01") };
        yield return new object[] { Item.U4(12) };
        yield return new object[] { Item.U2() };
        yield return new object[] { Item.U2(12, 13) };
        yield return new object[] { Item.B(0, 255) };
        yield return new object[] { Item.L(Item.A("CUSTOM"), Item.I4(-1)) };
    }

    [Theory]
    [MemberData(nameof(OpaqueCommandCodes))]
    public void Command_Code_Type_Count_And_Value_Are_Preserved_In_S7F23_S7F25_Roundtrip(Item code)
    {
        var body = Item.L(Item.A("DEMO-Test"), Item.A("WORKBENCH"), Item.A("1.0"),
            Item.L(Item.L(code, Item.L(Item.A("DEMO")))));
        Ack(Reply(23, body), 0);
        string expected;
        using (var source = new SecsMessage(7, 26) { SecsItem = body })
            expected = source.ToSml(); // 源报文释放后再读取，不保留传输借用数据。
        using var actual = new SecsMessage(7, 26) { SecsItem = Reply(25, Item.A("DEMO-Test")) };
        Assert.Equal(expected, actual.ToSml());
    }

    [Fact]
    public async Task Malformed_Formatted_Requests_Report_Why_In_Runtime_Error_Log()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var errors = new List<string>();
        runtime.OnError += errors.Add;
        using var download = new SecsMessage(7, 23, true)
        {
            SecsItem = Item.L(Item.A("DEMO-Test"), Item.A("WORKBENCH"), Item.A("1.0"),
                Item.L(Item.L(Item.A("1"), Item.A("not-a-parameter-list"))))
        };
        await transport.EmitReceivedAsync(download);
        Assert.Contains(errors, text => text.Contains("S7F23") && text.Contains("命令[1].PPARM") && text.Contains("ASCII[20]"));
        Assert.Empty(runtime.GetProcessPrograms());
        using var request = new SecsMessage(7, 25, true) { SecsItem = Item.L(Item.A("DEMO-Test")) };
        await transport.EmitReceivedAsync(request);
        Assert.Contains(errors, text => text.Contains("S7F25") && text.Contains("PPID") && text.Contains("List[1]"));
    }

    [Fact]
    public void Formatted_Send_Uses_The_Same_Standard_Four_Field_Shape()
    {
        var data = new ProcessProgramData
        {
            PPID = "R1", MDLN = "MODEL", SOFTREV = "1.2",
            PPBODY = new() { new List<object> { (ushort)12, new List<object> { 1.25f, "P" } } }
        };
        var request = Handler(23).GetSendItem(data);
        Assert.Equal(4, request.Count);
        Assert.Equal("MODEL", request.Items[1].GetString());
        Assert.Equal("1.2", request.Items[2].GetString());
        var command = Assert.Single(request.Items[3].Items);
        Assert.Equal(SecsFormat.U2, command.Items[0].Format);
        Assert.Equal(SecsFormat.List, command.Items[1].Format);
        Assert.Null(Handler(23).GetSendItem("R1", new List<object> { "old-private-format" }));
    }

    [Fact]
    public void Common_Request_Builders_Use_Standard_Item_Shapes()
    {
        var inquiry = Handler(1).GetSendItem("R1", 2u);
        Assert.Equal(2, inquiry.Count);
        Assert.Equal(SecsFormat.U4, inquiry.Items[1].Format);
        Assert.Equal(2u, inquiry.Items[1].GetMemory<uint>().Span[0]);
        var download = Handler(3).GetSendItem("R1", new byte[] { 0, 255 });
        Assert.Equal(2, download.Count);
        Assert.Equal(SecsFormat.Binary, download.Items[1].Format);
        Assert.Equal(new byte[] { 0, 255 }, download.Items[1].GetMemory<byte>().ToArray());
        Assert.Equal(SecsFormat.ASCII, Handler(5).GetSendItem("R1").Format);
        var delete = Handler(17).GetSendItem("R1", "R2");
        Assert.Equal(new[] { "R1", "R2" }, delete.Items.Select(i => i.GetString()));
        Assert.Empty(Handler(17).GetSendItem(Array.Empty<string>()).Items);
        Assert.Null(Handler(17).GetSendItem("R1", 2));
        Assert.Null(Handler(3).GetSendItem("R1", "not-binary"));
    }

    public static IEnumerable<object[]> InvalidFormatted()
    {
        yield return new object[] { Item.L(Item.A("R1"), Item.L()) };
        yield return new object[] { Item.L(Item.A("R1"), Item.A(new string('M', 21)), Item.A("V"), Item.L()) };
        yield return new object[] { Item.L(Item.A("R1"), Item.A("M"), Item.A("中文"), Item.L()) };
        yield return new object[] { Item.L(Item.A("R1"), Item.A("M"), Item.A("V"), Item.A("body")) };
        yield return new object[] { Item.L(Item.A("R1"), Item.A("M"), Item.A("V"),
            Item.L(Item.L(Item.U2(12), Item.A("not-parameters")))) };
    }

    [Theory]
    [MemberData(nameof(InvalidFormatted))]
    public void Invalid_Formatted_Requests_Return_Null_For_S9F7_Without_Changing_The_Store(Item malformed)
    {
        Ack(Reply(23, Formatted()), 0);
        Assert.Null(Reply(23, malformed));
        Assert.Equal("MODEL", Reply(25, Item.A("R1")).Items[1].GetString());
        Assert.Single(Reply(19).Items);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(25)]
    public void Unknown_Recipe_Returns_L0_Not_A_Success_Ack(int function)
    {
        var reply = Reply(function, Item.A("missing"));
        Assert.Equal(SecsFormat.List, reply.Format);
        Assert.Empty(reply.Items);
    }

    [Fact]
    public void S7F1_Grants_Load_Without_Creating_A_Recipe()
    {
        foreach (var length in new[] { Item.U1(2), Item.U2(2), Item.U4(2), Item.U8(2) })
            Ack(Reply(1, Item.L(Item.A("R1"), length)), 0);
        Ack(Reply(1, Item.L(Item.A("R1"), Item.U8(ulong.MaxValue))), 2);
        Assert.Null(Reply(1, Item.L(Item.A("R1"), Item.I4(-1))));
        Assert.Null(Reply(1, Item.L(Item.A("R1"), Item.U4(1, 2))));
        Assert.Empty(Reply(19).Items);
    }

    [Fact]
    public void Binary_Download_And_Upload_Are_Byte_Exact_And_Do_Not_Borrow_Incoming_Memory()
    {
        var bytes = new byte[] { 0, 255, 128, 10 };
        using var request = Item.L(Item.A("R1"), Item.B(bytes));
        Ack(Reply(3, request), 0);
        bytes[0] = 99;
        var reply = Reply(5, Item.A("R1"));
        Assert.Equal(2, reply.Count);
        Assert.Equal("R1", reply.Items[0].GetString());
        Assert.Equal(SecsFormat.Binary, reply.Items[1].Format);
        Assert.Equal(new byte[] { 0, 255, 128, 10 }, reply.Items[1].GetMemory<byte>().ToArray());
        reply.Items[1].GetMemory<byte>().Span[1] = 42;
        Assert.Equal((byte)255, Reply(5, Item.A("R1")).Items[1].GetMemory<byte>().Span[1]);
    }

    [Fact]
    public void Binary_Empty_Body_Is_Not_An_Unknown_Recipe_And_Replacement_Is_Visible()
    {
        Ack(Reply(3, Item.L(Item.A("R1"), Item.B())), 0);
        Assert.Equal(2, Reply(5, Item.A("R1")).Count);
        Ack(Reply(3, Item.L(Item.A("R1"), Item.B(7))), 0);
        Assert.Equal((byte)7, Reply(5, Item.A("R1")).Items[1].GetMemory<byte>().Span[0]);
        Assert.Null(Reply(3, Item.L(Item.A("R1"), Item.A("not-binary"))));
        Assert.Equal((byte)7, Reply(5, Item.A("R1")).Items[1].GetMemory<byte>().Span[0]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("中文")]
    public void Unsupported_Ppids_Do_Not_Mutate_The_Store(string id)
    {
        Assert.Null(Reply(3, Item.L(Item.A(id), Item.B(1))));
        Assert.Null(Reply(5, Item.A(id)));
        Assert.Null(Reply(25, Item.A(id)));
        Assert.Empty(Reply(19).Items);
    }

    [Fact]
    public void Digit_Only_Ppids_Are_Ascii_Identifiers_And_Keep_Leading_Zeroes()
    {
        Ack(Reply(3, Item.L(Item.A("001001"), Item.B(1))), 0);
        Ack(Reply(3, Item.L(Item.A("1001"), Item.B(2))), 0);
        Assert.Equal(2, Reply(19).Count); // 两个不同标识，不能先转 int 再格式化。
        var request = Handler(5).GetSendItem("001001");
        Assert.Equal(SecsFormat.ASCII, request.Format);
        Assert.Equal("001001", request.GetString());
        Assert.Equal("001001", Reply(5, request).Items[0].GetString());
        Assert.Equal((byte)1, Reply(5, request).Items[1].GetMemory<byte>().Span[0]);
        Assert.Equal((byte)2, Reply(5, Item.A("1001")).Items[1].GetMemory<byte>().Span[0]);
        Assert.Null(Reply(5, Item.U4(1001)));
        Assert.Null(Reply(25, Item.U4(1001)));
        Assert.Null(Reply(3, Item.L(Item.U4(1001), Item.B(9))));
        Assert.Equal(2, Reply(19).Count);
    }

    [Fact]
    public void Ppid_Length_Boundary_Is_120_Ascii_Characters()
    {
        Ack(Reply(3, Item.L(Item.A(new string('R', 120)), Item.B(1))), 0);
        Assert.Null(Reply(3, Item.L(Item.A(new string('R', 121)), Item.B(1))));
        Assert.Single(Reply(19).Items);
    }

    [Fact]
    public void Delete_Unknown_Or_Malformed_List_Does_Not_Partially_Delete()
    {
        Ack(Reply(3, Item.L(Item.A("R1"), Item.B(1))), 0);
        Ack(Reply(17, Item.L(Item.A("R1"), Item.A("missing"))), 4);
        Assert.Null(Reply(17, Item.L(Item.A("R1"), Item.U2(2))));
        Assert.Single(Reply(19).Items);
        Ack(Reply(17, Item.L(Item.A("R1"))), 0);
        Assert.Empty(Reply(5, Item.A("R1")).Items);
    }

    [Fact]
    public void Empty_Delete_List_Removes_Both_Formats_And_Directory()
    {
        Ack(Reply(3, Item.L(Item.A("RAW"), Item.B(1))), 0);
        Ack(Reply(23, Formatted("FMT")), 0);
        Ack(Reply(17, Item.L()), 0);
        Assert.Empty(Reply(19).Items);
        Assert.Empty(Reply(5, Item.A("RAW")).Items);
        Assert.Empty(Reply(25, Item.A("FMT")).Items);
        Ack(Reply(17, Item.L()), 0);
    }

    [Fact]
    public void S7F24_Decodes_Ack_Without_Mistaking_It_For_A_Download()
    {
        bool? success = null;
        string? description = null;
        _device.ProcessProgramSendResult += (ok, text) => { success = ok; description = text; };
        Ack(Reply(24, Item.B(1)), 1);
        Assert.Equal(false, success);
        Assert.Equal("无权限", description);
        Assert.Null(Reply(24, Item.B()));
        Assert.Null(Reply(24, Item.B(0, 1)));
        Assert.Empty(Reply(19).Items);
    }

    [Fact]
    public void Formatted_Reply_Parsing_Rejects_The_Old_Private_L2_Format()
    {
        var handler = Handler(25);
        Assert.Null(handler.GetReplyItem(new SecsMessage(7, 26)
            { SecsItem = Item.L(Item.A("R1"), Item.L()) }));
        Assert.Empty(Reply(19).Items);
        var received = Formatted();
        Assert.Same(received, handler.GetReplyItem(new SecsMessage(7, 26) { SecsItem = received }));
        Assert.Single(Reply(19).Items);
    }

    [Fact]
    public async Task Runtime_Dispatches_Common_S7_Requests_Using_Only_InMemory_Transport()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        await transport.EmitReceivedAsync(new SecsMessage(7, 3, true)
            { SecsItem = Item.L(Item.A("R1"), Item.B(10, 20)) });
        Ack(transport.LastReplyItem!, 0);
        await transport.EmitReceivedAsync(new SecsMessage(7, 5, true) { SecsItem = Item.A("R1") });
        Assert.Equal(new byte[] { 10, 20 }, transport.LastReplyItem!.Items[1].GetMemory<byte>().ToArray());
        await transport.EmitReceivedAsync(new SecsMessage(7, 23, true) { SecsItem = Formatted("FMT") });
        Ack(transport.LastReplyItem!, 0);
        await transport.EmitReceivedAsync(new SecsMessage(7, 25, true) { SecsItem = Item.A("FMT") });
        Assert.Equal(4, transport.LastReplyItem!.Count);
        await transport.EmitReceivedAsync(new SecsMessage(7, 17, true) { SecsItem = Item.L() });
        Ack(transport.LastReplyItem!, 0);
        await transport.EmitReceivedAsync(new SecsMessage(7, 19, true));
        Assert.Empty(transport.LastReplyItem!.Items);
    }
}
