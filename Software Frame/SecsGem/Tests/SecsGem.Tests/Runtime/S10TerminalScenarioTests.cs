using Secs4Net;
using SecsGem.Core;
using SecsGem.Runtime;
using SecsGem.Tests.Fakes;
using SecsGem.Tests.Infrastructure;
using Xunit;

namespace SecsGem.Tests.Runtime;

public sealed class S10TerminalScenarioTests
{
    private sealed class Display(Action<SecsTerminalMessage> action) : ITerminalDisplay
    {
        public void Show(string title, string message) => throw new InvalidOperationException("应使用带 TID 的入口。");
        public void Show(SecsTerminalMessage message) => action(message);
    }
    private static async Task Receive(InMemorySecsTransport transport, byte function, Item? body)
    {
        using var request = new SecsMessage(10, function, true) { SecsItem = body };
        await transport.EmitReceivedAsync(request);
    }

    [Theory]
    [InlineData(3)] [InlineData(5)]
    public async Task Parsed_Text_Is_Copied_Ordered_And_Only_Local_Confirmation_Marks_Read(byte function)
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var received = new List<SecsTerminalMessage>();
        runtime.TerminalDisplay = new Display(received.Add);
        await Receive(transport, function, Item.L(Item.B(7), function == 3 ? Item.A("Hello") : Item.L(Item.A("First"), Item.A(""), Item.A("Last"))));
        Assert.Equal((byte)0, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
        await runtime.DrainTerminalMessagesAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var message = Assert.Single(received);
        Assert.Equal((byte)7, message.TerminalId);
        Assert.Equal(function == 3 ? "Hello" : "First\r\n\r\nLast", message.Text);
        var entry = Assert.Single(runtime.GetTerminalMessageHistory());
        Assert.Equal(TerminalDeliveryState.Dispatched, entry.State);
        Assert.False(entry.IsRead);
        Assert.True(runtime.AcknowledgeTerminalMessage(message.Id));
        Assert.True(Assert.Single(runtime.GetTerminalMessageHistory()).IsRead);
        Assert.False(entry.IsRead); // 之前取出的状态快照不被回写。
        Assert.Empty(transport.Sent); // 本地已读不发送第二个协议 ACK。
    }

    [Fact]
    public async Task Malformed_Root_Tid_And_Text_Are_Not_Accepted_Or_Displayed()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var shown = new List<SecsTerminalMessage>();
        runtime.TerminalDisplay = new Display(shown.Add);
        var cases = new (byte Function, Item? Body)[]
        {
            (3, null), (3, Item.L()), (3, Item.L(Item.B(0))),
            (3, Item.L(Item.U4(256), Item.A("X"))),
            (3, Item.L(Item.I4(-1), Item.A("X"))),
            (3, Item.L(Item.F8(1.5), Item.A("X"))),
            (3, Item.L(Item.A("bad-id"), Item.A("X"))),
            (3, Item.L(Item.F8(1e-50), Item.A("X"))),
            (3, Item.L(Item.B(), Item.A("X"))),
            (3, Item.L(Item.B(0, 1), Item.A("X"))),
            (5, Item.L(Item.L(Item.U4(1)), Item.A("X")))
        };
        foreach (var test in cases)
        {
            await Receive(transport, test.Function, test.Body);
            Assert.Null(transport.LastReplyItem); // Runtime 请求底层 S9F7，不回成功 ACKC10。
        }
        Assert.Empty(runtime.GetTerminalMessageHistory());
        Assert.Empty(shown);
    }

    [Theory]
    [InlineData(3)] [InlineData(5)]
    public async Task Convertible_Types_And_Text_Containers_Are_Not_Restricted_To_The_Example_Format(byte function)
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var messages = new List<SecsTerminalMessage>();
        runtime.TerminalDisplay = new Display(messages.Add);
        await Receive(transport, function, Item.L(Item.A("007"), Item.J("设备通知")));
        await Receive(transport, function, Item.L(Item.U4(8), Item.L(Item.A("第一行"), Item.U2(1, 2),
            Item.L(Item.Boolean(true), Item.A(""), Item.F8(1.5)))));
        await Receive(transport, function, Item.L(Item.F8(9), Item.B(1, 255)));
        await Receive(transport, function, Item.L(Item.U1(10), Item.A("Plain")));
        await runtime.DrainTerminalMessagesAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new byte[] { 7, 8, 9, 10 }, messages.Select(message => message.TerminalId));
        Assert.Equal("设备通知", messages[0].Text);
        Assert.Equal("第一行\r\n1 2\r\nTrue\r\n\r\n1.5", messages[1].Text);
        Assert.Equal("1 255", messages[2].Text); // Binary 作为数值展示，不猜测字符编码。
        Assert.Equal("Plain", messages[3].Text); // F5 也允许直接文本，F3 也允许列表。
        Assert.All(runtime.GetTerminalMessageHistory(), entry => Assert.Equal(TerminalDeliveryState.Dispatched, entry.State));
    }

    [Fact]
    public async Task Blocked_Display_Does_Not_Block_Ack_Queue_Is_Bounded_And_Dispose_Cancels_Pending()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        runtime.TerminalDisplay = new Display(_ => { Interlocked.Increment(ref calls); entered.TrySetResult(); release.Wait(); });
        try
        {
            await Receive(transport, 3, Item.L(Item.B(0), Item.A("First"))).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal((byte)0, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            for (var index = 0; index < 64; index++)
            {
                await Receive(transport, 3, Item.L(Item.B(0), Item.A(index.ToString())));
                Assert.Equal((byte)0, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
            }
            await Receive(transport, 3, Item.L(Item.B(0), Item.A("Overflow")));
            Assert.Equal((byte)1, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
            Assert.Equal(65, runtime.GetTerminalMessageHistory().Count); // 一条正在分发，64 条排队。
            runtime.Dispose();
            Assert.All(runtime.GetTerminalMessageHistory(), entry => Assert.Equal(TerminalDeliveryState.Cancelled, entry.State));
        }
        finally { release.Set(); }
        await runtime.DrainTerminalMessagesAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await runtime.TerminalWorkerCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, calls);
        Assert.Equal((byte)1, runtime.EnqueueTerminalMessage(0, "S10F3", "Late"));
    }

    [Fact]
    public async Task Display_Failure_Does_Not_Send_Another_Reply_Or_Stop_The_Next_Message()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        var calls = 0;
        runtime.TerminalDisplay = new Display(_ => { if (++calls == 1) throw new InvalidOperationException("Display offline"); });
        await Receive(transport, 3, Item.L(Item.B(0), Item.A("First")));
        await Receive(transport, 5, Item.L(Item.B(1), Item.L(Item.A("Second"))));
        await runtime.DrainTerminalMessagesAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var history = runtime.GetTerminalMessageHistory();
        Assert.Equal(TerminalDeliveryState.Failed, history[0].State);
        Assert.Contains("Display offline", history[0].Error);
        Assert.Equal(TerminalDeliveryState.Dispatched, history[1].State);
        Assert.False(runtime.AcknowledgeTerminalMessage(history[0].Message.Id));
        Assert.Equal(2, transport.ReplyAttempts);
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task Disabled_Service_Rejects_Valid_Message_Without_Creating_A_History_Row()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        runtime.TerminalDisplay = null;
        await Receive(transport, 3, Item.L(Item.B(0), Item.A("Hello")));
        Assert.Equal((byte)1, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
        Assert.Empty(runtime.GetTerminalMessageHistory());
    }

    [Fact]
    public async Task Disconnect_Preserves_Accepted_Notifications_Without_Replaying_Them_On_Reconnect()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        await transport.ConnectAsync(true, "127.0.0.1", 1, 0);
        var received = new List<SecsTerminalMessage>();
        runtime.TerminalDisplay = new Display(received.Add);
        await Receive(transport, 3, Item.L(Item.B(1), Item.A("Before")));
        await runtime.DrainTerminalMessagesAsync().WaitAsync(TimeSpan.FromSeconds(5));
        transport.Close();
        Assert.Single(runtime.GetTerminalMessageHistory());
        await transport.ConnectAsync(true, "127.0.0.1", 1, 0);
        await Receive(transport, 3, Item.L(Item.B(1), Item.A("After")));
        await runtime.DrainTerminalMessagesAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "Before", "After" }, received.Select(message => message.Text));
        Assert.Equal(new long[] { 1, 2 }, received.Select(message => message.Id));
        Assert.Equal(2, transport.ReplyAttempts);
    }

    [Fact]
    public async Task Headless_History_Rolls_Over_At_200_And_Empty_Text_Remains_Valid()
    {
        var transport = new InMemorySecsTransport();
        using var runtime = RuntimeTestFixture.Create(transport);
        for (var index = 0; index < 205; index++)
        {
            await Receive(transport, 3, Item.L(Item.B(255), Item.A("")));
            await runtime.DrainTerminalMessagesAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        var history = runtime.GetTerminalMessageHistory();
        Assert.Equal(200, history.Count);
        Assert.Equal(6L, history[0].Message.Id);
        Assert.All(history, entry => { Assert.Empty(entry.Message.Text); Assert.False(entry.IsRead); });
        Assert.False(runtime.AcknowledgeTerminalMessage(1)); // 内存历史已轮转，不伪造确认。
    }
}
