using SecsGem.Core;
using SecsGem.Runtime;
using SecsGem.Tests.Infrastructure;
using Xunit;

namespace SecsGem.Tests.Integration;

public sealed class S10TerminalWireTests
{
    private sealed class BlockingDisplay(TaskCompletionSource entered, ManualResetEventSlim release) : ITerminalDisplay
    {
        public void Show(string title, string message) { entered.TrySetResult(); release.Wait(); }
    }
    private static async Task Connect(SecsGemRuntime runtime, HsmsHostSimulator host, CancellationToken token)
    {
        await runtime.ConnectAsync(true, "127.0.0.1", host.Port, 0);
        await host.WaitForDeviceAsync(token);
        await host.CompleteSelectAsync(token);
        while (!runtime.IsConnected) await Task.Delay(10, token);
    }

    [Fact(Timeout = 30000)]
    public async Task Host_Gets_S10_Ack_And_Can_Continue_While_Operator_Display_Is_Blocked()
    {
        using var host = HsmsHostSimulator.Start();
        using var runtime = RuntimeTestFixture.CreateReal();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.TerminalDisplay = new BlockingDisplay(entered, release);
        await Connect(runtime, host, timeout.Token);
        try
        {
            host.SendDataMessage(10, 3, true, 0, 0x12345678,
                new byte[] { 1, 2, 0x21, 1, 7, 0x41, 5, 72, 101, 108, 108, 111 }, timeout.Token);
            await entered.Task.WaitAsync(timeout.Token);
            var ack = await host.ReadMessageAsync(timeout.Token);
            Assert.Equal((byte)10, ack.S);
            Assert.Equal((byte)4, ack.F);
            Assert.False(ack.WBit);
            Assert.Equal(0x12345678U, ack.SystemBytes);
            Assert.Equal(new byte[] { 0x21, 1, 0 }, ack.Body);
            host.SendDataMessage(7, 19, true, 0, 0x70000001, Array.Empty<byte>(), timeout.Token);
            var directory = await host.ReadMessageAsync(timeout.Token);
            Assert.Equal((byte)7, directory.S);
            Assert.Equal((byte)20, directory.F); // 其他业务不被操作员窗口阻塞。
            Assert.False(Assert.Single(runtime.GetTerminalMessageHistory()).IsRead);
        }
        finally { release.Set(); }
        await runtime.DrainTerminalMessagesAsync().WaitAsync(timeout.Token);
    }

    [Fact(Timeout = 30000)]
    public async Task Multiline_Host_Message_Preserves_Empty_Lines_And_Out_Of_Range_Tid_Uses_S9F7()
    {
        using var host = HsmsHostSimulator.Start();
        using var runtime = RuntimeTestFixture.CreateReal();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        await Connect(runtime, host, timeout.Token);
        host.SendDataMessage(10, 5, true, 0, 0x11111111,
            new byte[] { 1, 2, 0x21, 1, 9, 1, 3, 0x41, 3, 79, 110, 101, 0x41, 0, 0x41, 3, 84, 119, 111 }, timeout.Token);
        var ack = await host.ReadMessageAsync(timeout.Token);
        Assert.Equal((byte)10, ack.S);
        Assert.Equal((byte)6, ack.F);
        Assert.Equal(new byte[] { 0x21, 1, 0 }, ack.Body);
        await runtime.DrainTerminalMessagesAsync().WaitAsync(timeout.Token);
        var entry = Assert.Single(runtime.GetTerminalMessageHistory());
        Assert.Equal((byte)9, entry.Message.TerminalId);
        Assert.Equal("One\r\n\r\nTwo", entry.Message.Text);
        // U2 本身允许，但 TID=256 无法装入当前 byte 终端标识，不能截断成 0。
        host.SendDataMessage(10, 3, true, 0, 0x12345678,
            new byte[] { 1, 2, 0xA9, 2, 1, 0, 0x41, 1, 88 }, timeout.Token);
        var error = await host.ReadMessageAsync(timeout.Token);
        Assert.Equal((byte)9, error.S);
        Assert.Equal((byte)7, error.F);
        Assert.Equal(new byte[] { 0x21, 10, 0, 0, 0x8A, 3, 0, 0, 0x12, 0x34, 0x56, 0x78 }, error.Body);
        Assert.Single(runtime.GetTerminalMessageHistory());
    }
}
