// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：TcpSilentServerLifecycleTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Net、System.Net.Sockets、Communication.Core.Protocols.Socket、Communication.Runtime.Protocols.Socket、Xunit。导入不代表所有类型都会在运行时加载。
using System.Net;
using System.Net.Sockets;
using Communication.Core.Protocols.Socket;
using Communication.Runtime.Protocols.Socket;
using Xunit;

namespace Communication.Tests.Compatibility;

/// <summary>
/// 静默服务器（接受后既不发数据也不关闭）场景的设备生命周期测试。
/// 排查 Transport 门控等待被取消的根因：若释放/关闭被卡住，
/// 后续 OpenAsync 会一直在 _lifecycleGate.WaitAsync 排队直到令牌取消。
/// </summary>
public sealed class TcpSilentServerLifecycleTests
{
    private static readonly TimeSpan GuardTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 验证 StartSilentServerAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Start、AcceptTcpClientAsync；具体条件和先后次序见方法体。
    /// </summary>
    private static async Task<(TcpListener Listener, int Port, Task<TcpClient> Accept)> StartSilentServerAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        // 黑洞：接受连接后保持静默（不发数据、不关闭），模拟对端假死。
        var accept = listener.AcceptTcpClientAsync();
        return (listener, port, accept);
    }

    /// <summary>
    /// 验证 SilentParam 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ToString；具体条件和先后次序见方法体。
    /// </summary>
    private static SocketParam SilentParam(int port, bool autoReconnect) => new()
    {
        ServerIP = IPAddress.Loopback.ToString(),
        Port = port,
        Delimiter = null,
        TimeoutMs = 1000,
        AutoReconnect = autoReconnect,
        ReconnectIntervalMs = 10,
    };

    /// <summary>
    /// 验证 Dispose_ConnectedSilentServer_CompletesPromptly 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 StartSilentServerAsync、SilentParam、True、ConnectAsync、WaitAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_ConnectedSilentServer_CompletesPromptly(bool autoReconnect)
    {
        var (listener, port, accept) = await StartSilentServerAsync();
        try
        {
            await using var device = new TcpClientDevice(SilentParam(port, autoReconnect));
            Assert.True(await device.ConnectAsync());
            using var peer = await accept.WaitAsync(TimeSpan.FromSeconds(2));

            // 释放必须及时完成：若卡住，说明 CloseAsync 在门内等待收循环或 pipe 收尾。
            await device.DisposeAsync().AsTask().WaitAsync(GuardTimeout);
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// 验证 Disconnect_Then_Reconnect_SilentServer_Succeeds 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 StartSilentServerAsync、SilentParam、True、ConnectAsync、WaitAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Disconnect_Then_Reconnect_SilentServer_Succeeds()
    {
        var (listener, port, accept) = await StartSilentServerAsync();
        try
        {
            await using var device = new TcpClientDevice(SilentParam(port, false));
            Assert.True(await device.ConnectAsync());
            using var peer = await accept.WaitAsync(TimeSpan.FromSeconds(2));

            await device.DisconnectAsync().WaitAsync(GuardTimeout);
            Assert.True(await device.ConnectAsync().WaitAsync(GuardTimeout));
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// 验证 SecondDevice_SameEndpoint_Connects_After_First_Disposed 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 StartSilentServerAsync、SilentParam、True、ConnectAsync、WaitAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task SecondDevice_SameEndpoint_Connects_After_First_Disposed()
    {
        var (listener, port, accept) = await StartSilentServerAsync();
        try
        {
            await using var first = new TcpClientDevice(SilentParam(port, false));
            Assert.True(await first.ConnectAsync());
            using var peer = await accept.WaitAsync(TimeSpan.FromSeconds(2));
            await first.DisposeAsync().AsTask().WaitAsync(GuardTimeout);

            // 同一目标的新设备必须能建连：排除旧设备残留门控或循环污染新设备。
            await using var second = new TcpClientDevice(SilentParam(port, false));
            Assert.True(await second.ConnectAsync().WaitAsync(GuardTimeout));
        }
        finally
        {
            listener.Stop();
        }
    }
}
