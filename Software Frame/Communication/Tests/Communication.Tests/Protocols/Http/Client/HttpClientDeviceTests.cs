// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：HttpClientDeviceTests、ScriptedHandler。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Net、Communication.Core.Abstractions、Communication.Core.Protocols.Http、Communication.Runtime.Protocols.Http、Xunit。导入不代表所有类型都会在运行时加载。
using System.Net;
using Communication.Core.Abstractions;
using Communication.Core.Protocols.Http;
using Communication.Runtime.Protocols.Http;
using Xunit;

namespace Communication.Tests.Protocols.Http;

/// <summary>
/// HttpClientDeviceTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class HttpClientDeviceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReconnectAndDispose_PreserveInjectedHandlerOwnership(bool webApi)
    {
        using var handler = new ScriptedHandler(
            new HttpResponseMessage(HttpStatusCode.OK),
            new HttpResponseMessage(HttpStatusCode.OK));
        await using Communication.Runtime.Core.CommunicationDeviceBase device = webApi
            ? new Communication.Runtime.Protocols.WebApi.WebApiClientDevice(
                new Communication.Core.Protocols.WebApi.WebApiParam { BaseUrl = "http://localhost/" }, handler)
            : new HttpClientDevice(new HttpParam { BaseUrl = "http://localhost/" }, handler);

        Assert.True(await device.ConnectAsync());
        await device.ReadAsync("status", 0);
        await device.DisconnectAsync();
        Assert.False(handler.Disposed);
        Assert.True(await device.ConnectAsync());
        await device.ReadAsync("status", 0);
        await device.DisposeAsync();
        Assert.False(handler.Disposed);
        Assert.Equal(2, handler.RequestCount);
    }

    /// <summary>
    /// 测试相关入口 Injected_http_handler_supports_connection_seam_and_read_retry；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 IsAssignableFrom、True、ConnectAsync、Equal、ReadAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Injected_http_handler_supports_connection_seam_and_read_retry()
    {
        var handler = new ScriptedHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 0x2A }),
            });
        await using var device = new HttpClientDevice(new HttpParam
        {
            BaseUrl = "http://localhost/",
            RetryCount = 2,
            RetryDelayMs = 1,
        }, handler);

        Assert.IsAssignableFrom<IConnection>(device);
        Assert.True(await device.ConnectAsync());
        Assert.Equal(new byte[] { 0x2A }, await device.ReadAsync("status", 0));
        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(ConnectionState.Connected, ((IConnection)device).State);
    }

    /// <summary>
    /// 测试相关入口 Post_failure_is_not_retried_implicitly；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 True、ConnectAsync、ThrowsAsync、WriteAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Post_failure_is_not_retried_implicitly()
    {
        var handler = new ScriptedHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.OK));
        await using var device = new HttpClientDevice(new HttpParam
        {
            BaseUrl = "http://localhost/",
            RetryCount = 2,
            RetryDelayMs = 1,
        }, handler);
        Assert.True(await device.ConnectAsync());

        await Assert.ThrowsAsync<HttpRequestException>(
            () => device.WriteAsync("commands", new byte[] { 0x01 }));

        Assert.Equal(1, handler.RequestCount);
    }

    /// <summary>
    /// ScriptedHandler 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：HttpMessageHandler。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class ScriptedHandler(params HttpResponseMessage[] responses)
        : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public int RequestCount { get; private set; }
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }

        /// <summary>
        /// 测试相关入口 SendAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、FromResult、Dequeue；具体条件和先后次序见方法体。
        /// </summary>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCount++;
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
