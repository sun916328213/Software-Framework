using System.Net;
using CommunicationNew.Abstractions;
using CommunicationNew.Protocols.Http;
using CommunicationNew.Protocols.Socket;
using CommunicationNew.Protocols.WebApi;
using Xunit;

namespace CommunicationNew.Tests.Compatibility;

public sealed class HttpServerLifecycleTests
{
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

    private sealed class ScriptedHttpServerListener : IHttpServerListener
    {
        private TaskCompletionSource<HttpListenerContext> _pending = NewPending();

        public bool IsListening { get; private set; }
        public int StopCount { get; private set; }
        public List<string> Prefixes { get; } = new();

        public void AddPrefix(string prefix) => Prefixes.Add(prefix);

        public void Start()
        {
            IsListening = true;
            _pending = NewPending();
        }

        public void Stop()
        {
            if (!IsListening) return;
            StopCount++;
            IsListening = false;
            _pending.TrySetCanceled();
        }

        public Task<HttpListenerContext> GetContextAsync() => _pending.Task;

        public void Dispose() => Stop();

        private static TaskCompletionSource<HttpListenerContext> NewPending() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ScriptedTcpServerListener : ITcpServerListener
    {
        private TaskCompletionSource<System.Net.Sockets.TcpClient> _pending = NewPending();

        public bool IsListening { get; private set; }
        public int StopCount { get; private set; }

        public void Start(int backlog)
        {
            IsListening = true;
            _pending = NewPending();
        }

        public void Stop()
        {
            if (!IsListening) return;
            StopCount++;
            IsListening = false;
            _pending.TrySetCanceled();
        }

        public ValueTask<System.Net.Sockets.TcpClient> AcceptTcpClientAsync(
            CancellationToken cancellationToken) =>
            new(_pending.Task.WaitAsync(cancellationToken));

        public void Dispose() => Stop();

        private static TaskCompletionSource<System.Net.Sockets.TcpClient> NewPending() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
