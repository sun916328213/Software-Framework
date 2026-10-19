using SecsGem.Runtime;
using SecsGem.Tests.Infrastructure;
using Xunit;

namespace SecsGem.Tests.Integration;

/// <summary>仅连接测试创建的 Loopback Host；所有期望报头/正文为独立手工字节。</summary>
public sealed class S9ProtocolIntegrationTests
{
    private static async Task Connect(SecsGemRuntime runtime, HsmsHostSimulator host, CancellationToken token)
    {
        await runtime.ConnectAsync(true, "127.0.0.1", host.Port, 0);
        await host.WaitForDeviceAsync(token);
        await host.CompleteSelectAsync(token);
        while (!runtime.IsConnected) await Task.Delay(10, token);
    }

    private static async Task HealthyProbe(HsmsHostSimulator host, CancellationToken token)
    {
        host.SendDataMessage(7, 19, true, 0, 0x70000001, Array.Empty<byte>(), token);
        var reply = await host.ReadMessageAsync(token);
        Assert.Equal((byte)7, reply.S);
        Assert.Equal((byte)20, reply.F);
        Assert.Equal(new byte[] { 0x01, 0 }, reply.Body);
    }

    [Theory(Timeout = 30000)]
    [InlineData(1, 1)]
    [InlineData(7, 19)]
    public async Task S9F1_Includes_The_Wrong_Device_Id_Even_For_Function_One(byte stream, byte function)
    {
        using var host = HsmsHostSimulator.Start();
        using var runtime = RuntimeTestFixture.CreateReal();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        await Connect(runtime, host, timeout.Token);
        host.SendDataMessage(stream, function, true, 1, 0x89ABCDEF, Array.Empty<byte>(), timeout.Token);
        var reply = await host.ReadMessageAsync(timeout.Token);
        Assert.Equal((byte)9, reply.S);
        Assert.Equal((byte)1, reply.F);
        Assert.False(reply.WBit);
        Assert.Equal(new byte[] { 0x21, 10, 0, 1, (byte)(0x80 | stream), function, 0, 0, 0x89, 0xAB, 0xCD, 0xEF }, reply.Body);
        await HealthyProbe(host, timeout.Token);
    }

    [Theory(Timeout = 30000)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task S9F7_Reports_Illegal_Data_With_And_Without_Request_Wbit(bool w)
    {
        using var host = HsmsHostSimulator.Start();
        using var runtime = RuntimeTestFixture.CreateReal();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        await Connect(runtime, host, timeout.Token);
        // S7F19 应只有报头，发送 L[0] 属于非法正文。
        host.SendDataMessage(7, 19, w, 0, 0x12345678, new byte[] { 0x01, 0 }, timeout.Token);
        var reply = await host.ReadMessageAsync(timeout.Token);
        Assert.Equal((byte)9, reply.S);
        Assert.Equal((byte)7, reply.F);
        Assert.False(reply.WBit);
        Assert.Equal(new byte[] { 0x21, 10, 0, 0, (byte)(7 | (w ? 0x80 : 0)), 19, 0, 0, 0x12, 0x34, 0x56, 0x78 }, reply.Body);
        await HealthyProbe(host, timeout.Token);
    }

    [Theory(Timeout = 30000)]
    [InlineData(1, false)] [InlineData(3, false)] [InlineData(5, false)] [InlineData(7, false)]
    [InlineData(9, false)] [InlineData(11, false)] [InlineData(13, false)]
    [InlineData(7, true)] [InlineData(13, true)] [InlineData(99, false)]
    public async Task S9_Notifications_Are_Logged_But_Never_Replied_To(byte function, bool malformed)
    {
        using var host = HsmsHostSimulator.Start();
        using var runtime = RuntimeTestFixture.CreateReal();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var seen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.OnError += text => { if (text.Contains($"S9F{function}")) seen.TrySetResult(); };
        await Connect(runtime, host, timeout.Token);
        byte[] body = malformed ? new byte[] { 0x01, 0 }
            : function == 13 ? new byte[] { 0x01, 2, 0x41, 4, 83, 55, 70, 51, 0x41, 2, 48, 49 }
            : new byte[] { 0x21, 10, 0, 0, 0x81, 1, 0, 0, 0x99, 0xAB, 0xCD, 0xEF };
        // 故意置非法 W=1，也不能回 S9F2/F4 等或引发 S9 错误循环。
        host.SendDataMessage(9, function, true, 0, 0x99000001, body, timeout.Token);
        await seen.Task.WaitAsync(timeout.Token);
        await HealthyProbe(host, timeout.Token); // 下一条线上报文必须是探测应答，不是 S9 的应答。
    }

    [Fact(Timeout = 30000)]
    public async Task S9F13_Cannot_Abort_An_Unrelated_Transaction_With_The_Same_SystemBytes()
    {
        using var host = HsmsHostSimulator.Start();
        using var runtime = RuntimeTestFixture.CreateReal();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var seen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.OnError += text => { if (text.Contains("收到 S9F13")) seen.TrySetResult(); };
        await Connect(runtime, host, timeout.Token);
        var pending = runtime.SendItem("S1F1", true);
        var primary = await host.ReadMessageAsync(timeout.Token);
        Assert.Equal((byte)1, primary.S);
        Assert.Equal((byte)1, primary.F);
        host.SendDataMessage(9, 13, false, 0, primary.SystemBytes,
            new byte[] { 0x01, 2, 0x41, 4, 83, 55, 70, 51, 0x41, 2, 48, 49 }, timeout.Token);
        await seen.Task.WaitAsync(timeout.Token);
        host.SendDataMessage(1, 2, false, 0, primary.SystemBytes, new byte[] { 0x01, 0 }, timeout.Token);
        await pending.WaitAsync(timeout.Token); // 必须仍能收到正常 S1F2，不能被 S9F13 提前结束。
    }
}
