// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SecsTransportShutdownTests。对应计划任务 T05 —— 验证传输关闭"返回即当前代际结束"。
// 直接导入的协作命名空间包括：System、System.Threading、System.Threading.Tasks、DataManager.Runtime.Log、Secs4Net、SecsGem.Runtime.Transport、SecsGem.Tests.Infrastructure。
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataManager.Runtime.Log;
using Secs4Net;
using SecsGem.Runtime.Transport;
using SecsGem.Tests.Infrastructure;
using Xunit;

namespace SecsGem.Tests.Runtime;

/// <summary>
/// T05 验收：Secs4NetTransport 的关闭语义。
/// 用真实 TCP 回环 + 最小 HSMS Host 模拟器，直接驱动真实 transport（不使用 InMemory fake，
/// 以免掩盖 DisposeAsync / 接收任务未被 await 的真实释放行为）。
/// 完成标准对应：返回后无当前代际接收/回复任务；不早释放 CTS；重复关闭等同最终结果；重连可重建。
/// </summary>
public sealed class SecsTransportShutdownTests
{
    private const ushort DeviceId = 0;

    private static Secs4NetTransport CreateTransport()
    {
        var container = new TestPrismContainer();
        container.RegisterSingleton(typeof(LogManager), () => new LogManager());
        return new Secs4NetTransport(container);
    }

    /// <summary>建立真实回环连接并完成 Select 握手，返回处于 Selected 的 transport。</summary>
    private static async Task<Secs4NetTransport> ConnectSelectedAsync(HsmsHostSimulator host, CancellationToken ct)
    {
        var transport = CreateTransport();
        await transport.ConnectAsync(true, "127.0.0.1", host.Port, DeviceId).ConfigureAwait(false);
        await host.WaitForDeviceAsync(ct).ConfigureAwait(false);
        await host.CompleteSelectAsync(ct).ConfigureAwait(false);
        await WaitUntilAsync(() => transport.IsConnected, TimeSpan.FromSeconds(5), "设备进入 Selected").ConfigureAwait(false);
        return transport;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string description)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(20).ConfigureAwait(false);
        }
        throw new TimeoutException($"等待超时: {description}");
    }

    /// <summary>
    /// CloseAsync 返回后连接已真正释放，且重复关闭返回同一最终结果（幂等）。
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task CloseAsync_Awaits_Shutdown_And_Is_Idempotent()
    {
        using var host = HsmsHostSimulator.Start();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        using var transport = await ConnectSelectedAsync(host, cts.Token).ConfigureAwait(false);

        Assert.True(transport.IsConnected);

        await transport.CloseAsync().ConfigureAwait(false);
        Assert.False(transport.IsConnected);

        // 重复关闭：不应抛异常，且状态保持已断开
        await transport.CloseAsync().ConfigureAwait(false);
        await transport.CloseAsync().ConfigureAwait(false);
        Assert.False(transport.IsConnected);

        // 连接已真正释放：发送能力不再存在
        var sent = await transport.Send("S1F1", null, false).ConfigureAwait(false);
        Assert.Null(sent);
    }

    /// <summary>
    /// 关闭返回后不得再有当前代际的接收分发（晚到的 Host 消息不进入处理器）。
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task CloseAsync_Leaves_No_Receive_After_Return()
    {
        using var host = HsmsHostSimulator.Start();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        using var transport = await ConnectSelectedAsync(host, cts.Token).ConfigureAwait(false);

        int received = 0;
        transport.OnReceive += (_, _) =>
        {
            Interlocked.Increment(ref received);
            return Task.CompletedTask;
        };

        await transport.CloseAsync().ConfigureAwait(false);

        // 关闭返回后由 Host 侧再发一条主消息
        try
        {
            host.SendDataMessage(1, 1, false, DeviceId, 0x12345678, Array.Empty<byte>(), cts.Token);
        }
        catch (Exception)
        {
            // 设备侧套接字已关闭导致写入失败，本身也是"已释放"的证据
        }

        await Task.Delay(400).ConfigureAwait(false);
        Assert.Equal(0, Volatile.Read(ref received));
        Assert.False(transport.IsConnected);
    }

    /// <summary>
    /// 并发多次 CloseAsync 共享同一个完成 Task，全部成功完成。
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task Concurrent_CloseAsync_Calls_Share_Single_Completion()
    {
        using var host = HsmsHostSimulator.Start();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        using var transport = await ConnectSelectedAsync(host, cts.Token).ConfigureAwait(false);

        var tasks = new List<Task>();
        for (int i = 0; i < 8; i++)
        {
            tasks.Add(transport.CloseAsync());
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        Assert.All(tasks, t => Assert.True(t.IsCompletedSuccessfully));
        Assert.False(transport.IsConnected);
    }

    /// <summary>
    /// 关闭后可以重新连接（CTS 重建、代际隔离），新代际正常进入 Selected。
    /// </summary>
    [Fact(Timeout = 60000)]
    public async Task Connect_After_Close_Rebuilds_Transport()
    {
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        using var host1 = HsmsHostSimulator.Start();
        using var transport = CreateTransport();

        await transport.ConnectAsync(true, "127.0.0.1", host1.Port, DeviceId).ConfigureAwait(false);
        await host1.WaitForDeviceAsync(cts1.Token).ConfigureAwait(false);
        await host1.CompleteSelectAsync(cts1.Token).ConfigureAwait(false);
        await WaitUntilAsync(() => transport.IsConnected, TimeSpan.FromSeconds(5), "首次 Selected").ConfigureAwait(false);

        await transport.CloseAsync().ConfigureAwait(false);
        Assert.False(transport.IsConnected);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        using var host2 = HsmsHostSimulator.Start();
        await transport.ConnectAsync(true, "127.0.0.1", host2.Port, DeviceId).ConfigureAwait(false);
        await host2.WaitForDeviceAsync(cts2.Token).ConfigureAwait(false);
        await host2.CompleteSelectAsync(cts2.Token).ConfigureAwait(false);
        await WaitUntilAsync(() => transport.IsConnected, TimeSpan.FromSeconds(5), "重连 Selected").ConfigureAwait(false);

        Assert.True(transport.IsConnected);
    }

    /// <summary>
    /// 从未连接过时关闭是安全的；同步 Close 兼容入口与 Dispose 都可用。
    /// </summary>
    [Fact(Timeout = 15000)]
    public async Task CloseAsync_Before_Connect_Completes()
    {
        using var transport = CreateTransport();

        await transport.CloseAsync().ConfigureAwait(false);
        await transport.CloseAsync().ConfigureAwait(false);
        transport.Close(); // 同步兼容入口

        Assert.False(transport.IsConnected);
    }

    /// <summary>
    /// 关闭必须等完**正在处理中**的主消息：处理器被刻意卡住时，CloseAsync 不得提前返回。
    /// <para>
    /// 奇数 F 且 S≠9 的报文经 secs4net 写入主消息通道，由本 transport 的接收循环 await 分发
    /// （见 SecsGem.ProcessDataMessageAsync:165 与 DispatchPrimaryAsync）。因此关闭的
    /// 「await 接收循环」这一步正是承重行：若被改回即发即忘，处理器仍在执行时 CloseAsync 会立刻返回。
    /// </para>
    /// </summary>
    [Fact(Timeout = 40000)]
    public async Task CloseAsync_Waits_For_InFlight_Primary_Handler()
    {
        using var host = HsmsHostSimulator.Start();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        using var transport = await ConnectSelectedAsync(host, cts.Token).ConfigureAwait(false);

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        transport.OnReceive += async (_, _) =>
        {
            entered.TrySetResult();
            await release.Task.ConfigureAwait(false);
        };

        host.SendDataMessage(1, 1, false, DeviceId, 0x11110001, Array.Empty<byte>(), cts.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);

        // 处理器仍在执行：关闭必须在 500ms 内不返回
        var closing = transport.CloseAsync();
        var finishedEarly = await Task.WhenAny(closing, Task.Delay(500, CancellationToken.None)).ConfigureAwait(false);
        Assert.NotSame(closing, finishedEarly);

        release.TrySetResult();
        await closing.WaitAsync(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
        Assert.False(transport.IsConnected);
    }

    /// <summary>
    /// 关闭必须排空**在途的 OnReply 通知**：偶数 F 报文经 <c>MessageIn</c> 派发即发即忘，
    /// 由 TrackNotification 记录、关闭时由 DrainNotificationsAsync 排空。
    /// <para>
    /// <summary>
    /// 关闭必须排空**在途的 OnReply 通知**：偶数 F 报文经 <c>MessageIn</c> 派发即发即忘，
    /// 由 TrackNotification 记录、关闭时由 DrainNotificationsAsync 排空。
    /// <para>
    /// 直接调用 public 的 <c>MessageIn</c> 而不经过真实 socket：它内部
    /// <c>TrackNotification(Task.Run(...))</c> 全程同步执行完才返回，因此「任务已登记」这一点
    /// 在调用返回时就是确定的 —— 走真实链路时 <c>Task.Run</c> 先启动、登记在后，
    /// 测试线程在 <c>entered</c> 置位后立刻关闭会排空到空列表，测到的是竞态不是排空语义。
    /// </para>
    /// <para>
    /// 若关闭不再排空（或 OnReply 又改回不跟踪的即发即忘），处理器仍在执行时 CloseAsync 会提前完成。
    /// </para>
    /// </summary>
    [Fact(Timeout = 20000)]
    public async Task CloseAsync_Drains_InFlight_Reply_Notification()
    {
        using var transport = CreateTransport();

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        transport.OnReply += async (_, _) =>
        {
            entered.TrySetResult();
            await release.Task.ConfigureAwait(false);
        };

        // 偶数 F = Host 对设备主动消息的回复。MessageIn 返回即代表该通知已登记进排空列表。
        transport.MessageIn(new SecsMessage(1, 2, false), 0x1234);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

        var closing = transport.CloseAsync();

        // 处理器仍被 release 卡住：关闭不得在此期间完成
        await Task.Delay(300).ConfigureAwait(false);
        Assert.False(closing.IsCompleted);

        release.TrySetResult();
        await closing.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
    }
}
