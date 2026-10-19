using Secs4Net;
using SecsGem.Runtime.Order;
using SecsGem.Runtime.Order.S9;
using SecsGem.Runtime.Protocol.Registry;
using SecsGem.Tests.Fakes;
using SecsGem.Tests.Infrastructure;
using Xunit;

namespace SecsGem.Tests.Runtime;

public sealed class S9ProtocolTests
{
    private static readonly byte[] Header = { 0x12, 0x34, 0x81, 1, 0, 0, 0x89, 0xAB, 0xCD, 0xEF };
    private static SecsOrderBase Handler(int function)
    {
        var registry = new SecsMessageRegistry(new TestPrismContainer());
        registry.AutoRegisterHandlers();
        return registry.GetHandler($"S9F{function}");
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(5)] [InlineData(7)] [InlineData(9)] [InlineData(11)]
    public void Header_Notices_Require_Exact_B10_And_Copy_All_Bytes(int function)
    {
        var handler = Handler(function);
        var source = Header.ToArray();
        using var item = handler.GetSendItem(new S9HeaderData(source));
        source[0] = 0;
        Assert.Equal(SecsFormat.Binary, item.Format);
        Assert.Equal(Header, item.GetMemory<byte>().ToArray());
        Assert.Null(handler.GetSendItem());
        Assert.Null(handler.GetSendItem(new byte[9]));
        Assert.Null(handler.GetSendItem(new byte[11]));
        using var invalid = Item.U1(Header);
        Assert.Null(handler.GetSendItem(invalid));
    }

    [Fact]
    public void Legacy_S9F9_Builder_Preserves_Wbit_And_Unsigned_SystemByte_Bit_Pattern()
    {
        using var message = new SecsMessage(1, 1, true);
        using var item = Handler(9).GetSendItem(new SS9F9
        {
            SecsMessage = message, DeviceID = 0x1234, ID = unchecked((int)0x89ABCDEF)
        });
        Assert.Equal(Header, item.GetMemory<byte>().ToArray());
    }

    [Fact]
    public void S9F13_Uses_Mexp_And_Edid_Not_Transaction_Header_Or_Ack()
    {
        var handler = Handler(13);
        using var item = handler.GetSendItem(new SS9F13 { Order = "S7F3", Message = "00042" });
        Assert.Equal(2, item.Count);
        Assert.Equal("S7F3", item.Items[0].GetString());
        Assert.Equal("00042", item.Items[1].GetString());
        Assert.Null(handler.GetSendItem());
        Assert.Null(handler.GetSendItem(new SS9F13 { Order = "", Message = "X" }));
        Assert.Null(handler.GetSendItem(new SS9F13 { Order = "S7F3", Message = "中文" }));
        using var message = new SecsMessage(9, 13) { SecsItem = item };
        Assert.Null(handler.GetReplyItem(message));
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(5)] [InlineData(7)] [InlineData(9)] [InlineData(11)] [InlineData(13)] [InlineData(99)]
    public async Task Incoming_S9_Never_Requests_A_Reply_Even_With_Bad_Wbit_Or_Body(int function)
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var errors = new List<string>();
        runtime.OnError += errors.Add;
        using var valid = new SecsMessage(9, (byte)function, false)
        {
            SecsItem = function == 13 ? Item.L(Item.A("S7F3"), Item.A("00042")) : Item.B(Header.ToArray())
        };
        await transport.EmitReceivedAsync(valid);
        using var invalid = new SecsMessage(9, (byte)function, true) { SecsItem = Item.L() };
        await transport.EmitReceivedAsync(invalid);
        Assert.Equal(0, transport.ReplyAttempts);
        Assert.Empty(transport.Sent);
        Assert.Equal(2, errors.Count);
        Assert.All(errors, text => Assert.Contains($"S9F{function}", text));
    }

    [Theory]
    [InlineData(12, 1, "S9F3")]
    [InlineData(1, 251, "S9F5")]
    public async Task Unknown_Stream_And_Function_Are_Classified_Using_The_Original_Header(byte stream, byte function, string expected)
    {
        var transport = new InMemorySecsTransport { DeviceID = 0x1234, ID = unchecked((int)0x89ABCDEF) };
        using var runtime = RuntimeTestFixture.Create(transport);
        using var request = new SecsMessage(stream, function, true);
        await transport.EmitReceivedAsync(request);
        var sent = Assert.Single(transport.Sent);
        Assert.Equal(expected, sent.Order);
        var bytes = sent.Item.GetMemory<byte>().ToArray();
        Assert.Equal(new byte[] { 0x12, 0x34, (byte)(0x80 | stream), function, 0, 0, 0x89, 0xAB, 0xCD, 0xEF }, bytes);
        Assert.Equal(0, transport.ReplyAttempts);
    }

    [Fact]
    public async Task S9_Send_Disallows_Wait_And_Manual_Length_And_Conversation_Reports_Are_Available()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        await Assert.ThrowsAsync<ArgumentException>(() => runtime.SendItem("S9F11", true, new S9HeaderData(Header)));
        Assert.Empty(transport.Sent);
        await runtime.SendItem("S9F11", false, new S9HeaderData(Header));
        await runtime.SendItem("S9F13", false, new SS9F13 { Order = "S7F3", Message = "00042" });
        Assert.Equal(new[] { "S9F11", "S9F13" }, transport.Sent.Select(entry => entry.Order));
    }

    [Fact]
    public void Real_Transport_Captures_Per_Message_Header_Without_Connecting()
    {
        var container = new TestPrismContainer();
        container.RegisterSingleton(typeof(DataManager.Runtime.Log.LogManager), () => new DataManager.Runtime.Log.LogManager());
        using var transport = new SecsGem.Runtime.Transport.Secs4NetTransport(container) { DeviceID = 0x1234 };
        using var original = new SecsMessage(1, 1, true);
        using var other = new SecsMessage(2, 17, true);
        transport.MessageOut(original, unchecked((int)0x89ABCDEF));
        transport.MessageOut(other, 0x77777777);
        Assert.True(transport.TryGetSentHeader(original, out var header));
        Assert.Equal(Header, header);
        header[0] = 0;
        Assert.True(transport.TryGetSentHeader(original, out var reread));
        Assert.Equal(Header, reread);
    }

    [Fact]
    public async Task Remote_S9F9_Does_Not_Create_Another_S9F9_And_Report_Failure_Preserves_Original_T3()
    {
        var transport = new InMemorySecsTransport { DeviceID = 0x1234 };
        using var runtime = RuntimeTestFixture.Create(transport);
        using var remote = new SecsMessage(9, 9);
        transport.SendOverride = (_, _, _) => throw new SecsException(remote, "T3 Timeout");
        await Assert.ThrowsAsync<Exception>(() => runtime.SendItem("S1F1", true));
        Assert.Single(transport.Sent);
        transport.Sent.Clear();
        using var original = new SecsMessage(1, 1, true);
        transport.MessageOut(original, unchecked((int)0x89ABCDEF));
        transport.SendOverride = (order, _, _) => order == "S1F1"
            ? throw new SecsException(original, "T3 Timeout") : throw new InvalidOperationException("发送通知失败");
        var error = await Assert.ThrowsAsync<Exception>(() => runtime.SendItem("S1F1", true));
        Assert.IsType<SecsException>(error.InnerException);
        Assert.Contains("T3", error.Message);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task T3_Reports_Only_The_Timed_Out_Transaction_Not_The_Last_Global_Id(bool captureHeader)
    {
        var transport = new InMemorySecsTransport { DeviceID = 0x1234 };
        using var runtime = RuntimeTestFixture.Create(transport);
        using var original = new SecsMessage(1, 1, true);
        if (captureHeader) transport.MessageOut(original, unchecked((int)0x89ABCDEF));
        transport.ID = 0x77777777; // 模拟其他报文已覆盖全局 ID。
        transport.SendOverride = (order, _, _) => order == "S1F1"
            ? throw new SecsException(original, "T3 Timeout") : new SecsMessage(1, 2);
        var error = await Assert.ThrowsAsync<Exception>(() => runtime.SendItem("S1F1", true));
        Assert.IsType<SecsException>(error.InnerException);
        var notifications = transport.Sent.Where(entry => entry.Order == "S9F9").ToArray();
        if (captureHeader) Assert.Equal(Header, Assert.Single(notifications).Item.GetMemory<byte>().ToArray());
        else Assert.Empty(notifications); // 无捕获证据时不能伪造 SHEAD。
    }
}
