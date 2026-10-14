// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：HostControlOverRealTransportTests。真传输 + 真运行时 + 真监听器走完整链路。
using SecsGem.Core;
using SecsGem.Tests.Infrastructure;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SecsGem.Tests.Runtime;

/// <summary>
/// 端到端：真实 HSMS 套接字 + 真实 <c>SecsGemRuntime</c> + 真实控制消息监听器。
/// <para>
/// 之前的覆盖是割裂的：<c>RuntimeWithoutTcpTests</c> 用假传输（绕过 secs4net 整段收发），
/// <c>SecsTransportShutdownTests</c> 用真传输但不挂运行时和监听器。
/// 现场故障恰好落在两段之间的缝隙里——报文进了套接字却没进监听器——所以必须合起来测。
/// </para>
/// </summary>
public sealed class HostControlOverRealTransportTests
{
    /// <summary>
    /// Host 侧报文头带的 DeviceId。必须和本端一致（主动模式下本端恒为 0）：
    /// secs4net 收到 DeviceId 对不上的报文会回 S9F1 并丢弃（不改 secs4net，这是既定行为），
    /// 所以这里用 0。Host 用非 0 发的场景由
    /// <c>SecsTransportShutdownTests.Host_Device_Id_Mismatch_Gets_S9F1_And_No_Delivery</c> 覆盖。
    /// </summary>
    private const ushort HostDeviceId = 0;

    /// <summary>
    /// Host 发 S1F15 → 设备回 S1F16 → 监听器把控制状态切到 OFFLINE。
    /// 这就是现场报"发 S1F15 收不到、也切不了模式"的完整路径。
    /// </summary>
    [Fact(Timeout = 60000)]
    public async Task Host_Sent_S1F15_Over_A_Real_Socket_Reaches_The_Listener_And_Goes_Offline()
    {
        using var host = HsmsHostSimulator.Start();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        using var runtime = RuntimeTestFixture.CreateReal();
        ISecsGemRuntime service = runtime;

        await service.ConnectAsync(new SecsGemOptions
        {
            IsActiveMode = true,
            IpAddress = "127.0.0.1",
            Port = host.Port,
            DeviceId = 40000,        // 越界值：主动模式下必须被忽略
        });
        await host.WaitForDeviceAsync(cts.Token);
        await host.CompleteSelectAsync(cts.Token);
        await WaitUntil(() => service.GetState().IsConnected, cts.Token, "设备进入 Selected");

        // 先用 S1F17 上线，构造"在线后 Host 要求下线"的真实场景
        host.SendDataMessage(1, 17, true, HostDeviceId, 0x0000_0C0C, Array.Empty<byte>(), cts.Token);
        await WaitUntil(() => service.GetState().Mode == SecsGemMode.OnlineRemote, cts.Token, "S1F17 未能上线");

        // Host 要求下线
        host.SendDataMessage(1, 15, true, HostDeviceId, 0x0000_0D0D, Array.Empty<byte>(), cts.Token);
        await WaitUntil(() => service.GetState().Mode == SecsGemMode.Offline, cts.Token, "S1F15 未能下线");

        Assert.Equal(SecsGemMode.Offline, service.GetState().Mode);
    }

    /// <summary>
    /// 同样的链路，但 Host 不置 W 位（回不了包）也必须被处理。
    /// 对应 secs4net 的 <c>TryReplyAsync</c> 在 <c>!ReplyExpected</c> 时抛异常那个坑。
    /// </summary>
    [Fact(Timeout = 60000)]
    public async Task Host_Sent_S1F15_Without_The_W_Bit_Still_Reaches_The_Listener()
    {
        using var host = HsmsHostSimulator.Start();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        using var runtime = RuntimeTestFixture.CreateReal();
        ISecsGemRuntime service = runtime;

        await service.ConnectAsync(new SecsGemOptions
        {
            IsActiveMode = true, IpAddress = "127.0.0.1", Port = host.Port
        });
        await host.WaitForDeviceAsync(cts.Token);
        await host.CompleteSelectAsync(cts.Token);
        await WaitUntil(() => service.GetState().IsConnected, cts.Token, "设备进入 Selected");

        host.SendDataMessage(1, 17, false, HostDeviceId, 0x0000_0E0E, Array.Empty<byte>(), cts.Token);
        await WaitUntil(() => service.GetState().Mode == SecsGemMode.OnlineRemote, cts.Token, "S1F17(w=0) 未能上线");

        host.SendDataMessage(1, 15, false, HostDeviceId, 0x0000_0F0F, Array.Empty<byte>(), cts.Token);
        await WaitUntil(() => service.GetState().Mode == SecsGemMode.Offline, cts.Token, "S1F15(w=0) 未能下线");
    }

    private static async Task WaitUntil(Func<bool> condition, CancellationToken ct, string description)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (condition()) return;
            await Task.Delay(50);
        }
        throw new TimeoutException($"等待超时: {description}");
    }
}
