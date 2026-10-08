// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：HttpServerLifecycleTests、ScriptedHttpServerListener、ScriptedTcpServerListener。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Net、Communication.Core.Abstractions、Communication.Core.Protocols.Http、Communication.Runtime.Protocols.Http、Communication.Core.Protocols.Socket、Communication.Runtime.Protocols.Socket。导入不代表所有类型都会在运行时加载。
using System.Net;
using Communication.Core.Abstractions;
using Communication.Core.Protocols.Http;
using Communication.Runtime.Protocols.Http;
using Communication.Core.Protocols.Socket;
using Communication.Runtime.Protocols.Socket;
using Communication.Core.Protocols.WebApi;
using Communication.Runtime.Protocols.WebApi;
using Xunit;

namespace Communication.Tests.Compatibility;

/// <summary>
/// HttpServerLifecycleTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class HttpServerLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedStart_RetryReconnectAndCanceledDisconnect_PreserveState(bool webApi)
    {
        var listener = new ScriptedHttpServerListener { FailuresRemaining = 1 };
        await using Communication.Runtime.Core.CommunicationDeviceBase server = webApi
            ? new WebApiServerDevice(new WebApiParam { BaseUrl = "http://localhost/", ApiPrefix = "/api" }, listener)
            : new HttpServerDevice(new HttpParam { BaseUrl = "http://localhost/" }, listener);
        var connection = (IConnection)server;

        Assert.False(await server.ConnectAsync());
        Assert.Equal(ConnectionState.Faulted, connection.State);
        Assert.True(await server.ConnectAsync());
        await server.DisconnectAsync();
        Assert.True(await server.ConnectAsync());
        Assert.True(await server.ConnectAsync());
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => server.DisconnectAsync(canceled.Token));
        Assert.Equal(ConnectionState.Connected, connection.State);
        await server.DisconnectAsync();
        Assert.Equal(ConnectionState.Disconnected, connection.State);
        Assert.Equal(2, listener.StopCount);
        Assert.Equal(3, listener.StartCount);
    }

    /// <summary>
    /// 测试相关入口 Http_server_connection_seam_stops_owned_listener_loop；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 IsAssignableFrom、True、ConnectAsync、Equal、DisconnectAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Http_server_connection_seam_stops_owned_listener_loop()
    {
        var listener = new ScriptedHttpServerListener();
        await using var server = new HttpServerDevice(new HttpParam
        {
            BaseUrl = "http://127.0.0.1:8080/",
        }, listener);

        Assert.IsAssignableFrom<IConnection>(server);
        Assert.True(await server.ConnectAsync());
        Assert.Equal(ConnectionState.Connected, ((IConnection)server).State);

        await server.DisconnectAsync();

        Assert.Equal(ConnectionState.Disconnected, ((IConnection)server).State);
        Assert.Equal(1, listener.StopCount);
    }

    /// <summary>
    /// 测试相关入口 WebApi_server_connection_seam_stops_owned_listener_loop；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 IsAssignableFrom、True、ConnectAsync、Equal、DisconnectAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task WebApi_server_connection_seam_stops_owned_listener_loop()
    {
        var listener = new ScriptedHttpServerListener();
        await using var server = new WebApiServerDevice(new WebApiParam
        {
            BaseUrl = "http://127.0.0.1:8080/",
            ApiPrefix = "/api",
        }, listener);

        Assert.IsAssignableFrom<IConnection>(server);
        Assert.True(await server.ConnectAsync());
        Assert.Equal(ConnectionState.Connected, ((IConnection)server).State);

        await server.DisconnectAsync();

        Assert.Equal(ConnectionState.Disconnected, ((IConnection)server).State);
        Assert.Equal(1, listener.StopCount);
    }

    /// <summary>
    /// 测试相关入口 Tcp_server_connection_seam_stops_owned_accept_loop；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 IsAssignableFrom、True、ConnectAsync、Equal、DisconnectAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Tcp_server_connection_seam_stops_owned_accept_loop()
    {
        var listener = new ScriptedTcpServerListener();
        await using var server = new TcpServerDevice(new SocketParam
        {
            LocalIP = "127.0.0.1",
            Port = 5020,
        }, listener);

        Assert.IsAssignableFrom<IConnection>(server);
        Assert.True(await server.ConnectAsync());
        Assert.Equal(ConnectionState.Connected, ((IConnection)server).State);

        await server.DisconnectAsync();

        Assert.Equal(ConnectionState.Disconnected, ((IConnection)server).State);
        Assert.Equal(1, listener.StopCount);
    }

    /// <summary>
    /// ScriptedHttpServerListener 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IHttpServerListener。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class ScriptedHttpServerListener : IHttpServerListener
    {
        public int FailuresRemaining { get; set; }
        public int StartCount { get; private set; }
        private TaskCompletionSource<HttpListenerContext> _pending = NewPending();

        public bool IsListening { get; private set; }
        public int StopCount { get; private set; }
        public List<string> Prefixes { get; } = new();

        /// <summary>
        /// 测试相关入口 AddPrefix；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        public void AddPrefix(string prefix) => Prefixes.Add(prefix);

        /// <summary>
        /// 测试相关入口 Start；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 NewPending；具体条件和先后次序见方法体。
        /// </summary>
        public void Start()
        {
            StartCount++;
            if (FailuresRemaining-- > 0) throw new IOException("Scripted start failure");
            IsListening = true;
            _pending = NewPending();
        }

        /// <summary>
        /// 测试相关入口 Stop；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 TrySetCanceled；具体条件和先后次序见方法体。
        /// </summary>
        public void Stop()
        {
            if (!IsListening) return;
            StopCount++;
            IsListening = false;
            _pending.TrySetCanceled();
        }

        /// <summary>
        /// 测试相关入口 GetContextAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public Task<HttpListenerContext> GetContextAsync() => _pending.Task;

        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Stop；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose() => Stop();

        /// <summary>
        /// 测试相关入口 NewPending；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        private static TaskCompletionSource<HttpListenerContext> NewPending() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// ScriptedTcpServerListener 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ITcpServerListener。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class ScriptedTcpServerListener : ITcpServerListener
    {
        private TaskCompletionSource<System.Net.Sockets.TcpClient> _pending = NewPending();

        public bool IsListening { get; private set; }
        public int StopCount { get; private set; }

        // ITcpServerListener 后来加了 LocalPort（真实实现回填系统分配的端口），脚本桩用不到端口。
        public int LocalPort { get; private set; }

        /// <summary>
        /// 测试相关入口 Start；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 NewPending；具体条件和先后次序见方法体。
        /// </summary>
        public void Start(int backlog)
        {
            IsListening = true;
            _pending = NewPending();
        }

        /// <summary>
        /// 测试相关入口 Stop；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 TrySetCanceled；具体条件和先后次序见方法体。
        /// </summary>
        public void Stop()
        {
            if (!IsListening) return;
            StopCount++;
            IsListening = false;
            _pending.TrySetCanceled();
        }

        /// <summary>
        /// 测试相关入口 AcceptTcpClientAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WaitAsync；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask<System.Net.Sockets.TcpClient> AcceptTcpClientAsync(
            CancellationToken cancellationToken) =>
            new(_pending.Task.WaitAsync(cancellationToken));

        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Stop；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose() => Stop();

        /// <summary>
        /// 测试相关入口 NewPending；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        private static TaskCompletionSource<System.Net.Sockets.TcpClient> NewPending() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
