// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WebApiClientDeviceTests、RecordingHandler。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Net、Communication.Core.Abstractions、Communication.Core.Protocols.WebApi、Communication.Runtime.Protocols.WebApi、Xunit。导入不代表所有类型都会在运行时加载。
using System.Net;
using Communication.Core.Abstractions;
using Communication.Core.Protocols.WebApi;
using Communication.Runtime.Protocols.WebApi;
using Xunit;

namespace Communication.Tests.Protocols.WebApi;

/// <summary>
/// WebApiClientDeviceTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class WebApiClientDeviceTests
{
    /// <summary>
    /// 测试相关入口 Injected_handler_preserves_api_prefix_and_does_not_retry_post；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 IsAssignableFrom、True、ConnectAsync、ThrowsAsync、WriteAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Injected_handler_preserves_api_prefix_and_does_not_retry_post()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.OK));
        await using var device = new WebApiClientDevice(new WebApiParam
        {
            BaseUrl = "http://localhost/",
            ApiPrefix = "/api/v1",
            RetryCount = 2,
        }, handler);

        Assert.IsAssignableFrom<IConnection>(device);
        Assert.True(await device.ConnectAsync());
        await Assert.ThrowsAsync<HttpRequestException>(
            () => device.WriteAsync("commands", new byte[] { 0x01 }));

        Assert.Single(handler.Requests);
        Assert.Equal("/api/v1/commands", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal(ConnectionState.Connected, ((IConnection)device).State);
    }

    /// <summary>
    /// RecordingHandler 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：HttpMessageHandler。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class RecordingHandler(params HttpResponseMessage[] responses)
        : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<HttpRequestMessage> Requests { get; } = new();

        /// <summary>
        /// 测试相关入口 SendAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、Add、FromResult、Dequeue；具体条件和先后次序见方法体。
        /// </summary>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
