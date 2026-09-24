using System.Net;
using CommunicationNew.Abstractions;
using CommunicationNew.Protocols.WebApi;
using Xunit;

namespace CommunicationNew.Tests.Protocols.WebApi;

public sealed class WebApiClientDeviceTests
{
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

    private sealed class RecordingHandler(params HttpResponseMessage[] responses)
        : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<HttpRequestMessage> Requests { get; } = new();

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
