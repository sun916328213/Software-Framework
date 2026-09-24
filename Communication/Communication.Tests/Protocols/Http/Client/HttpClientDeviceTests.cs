using System.Net;
using CommunicationNew.Abstractions;
using CommunicationNew.Protocols.Http;
using Xunit;

namespace CommunicationNew.Tests.Protocols.Http;

public sealed class HttpClientDeviceTests
{
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

    private sealed class ScriptedHandler(params HttpResponseMessage[] responses)
        : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public int RequestCount { get; private set; }

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
