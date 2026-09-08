using System.Net;
using System.Net.Sockets;
using CommunicationNew.Protocols.Socket;
using Xunit;

namespace CommunicationNew.Tests.Compatibility;

/// <summary>
/// 静默服务器（接受后既不发数据也不关闭）场景的设备生命周期测试。
/// 排查 Transport 门控等待被取消的根因：若释放/关闭被卡住，
/// 后续 OpenAsync 会一直在 _lifecycleGate.WaitAsync 排队直到令牌取消。
/// </summary>
public sealed class TcpSilentServerLifecycleTests
{
    private static readonly TimeSpan GuardTimeout = TimeSpan.FromSeconds(5);

    private static async Task<(TcpListener Listener, int Port, Task<TcpClient> Accept)> StartSilentServerAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        // 黑洞：接受连接后保持静默（不发数据、不关闭），模拟对端假死。
        var accept = listener.AcceptTcpClientAsync();
        return (listener, port, accept);
    }

    private static SocketParam SilentParam(int port, bool autoReconnect) => new()
    {
        ServerIP = IPAddress.Loopback.ToString(),
        Port = port,
        Delimiter = null,
        TimeoutMs = 1000,
        AutoReconnect = autoReconnect,
        ReconnectIntervalMs = 10,
    };

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
