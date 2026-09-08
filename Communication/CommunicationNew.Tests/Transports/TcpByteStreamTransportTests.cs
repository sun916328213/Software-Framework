using System.Buffers;
using System.Net;
using System.Net.Sockets;
using CommunicationNew.Transports;
using Xunit;

namespace CommunicationNew.Tests.Transports;

public sealed class TcpByteStreamTransportTests
{
    [Fact]
    public async Task Repeated_open_is_idempotent()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            await using var transport = new TcpByteStreamTransport(
                IPAddress.Loopback.ToString(),
                endpoint.Port);
            var acceptTask = listener.AcceptTcpClientAsync();

            await transport.OpenAsync();
            using var peer = await acceptTask;
            await transport.OpenAsync();

            Assert.True(transport.IsOpen);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Open_transport_exchanges_bytes_with_a_loopback_peer()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            await using var transport = new TcpByteStreamTransport(
                IPAddress.Loopback.ToString(),
                endpoint.Port);

            var acceptTask = listener.AcceptTcpClientAsync();
            await transport.OpenAsync();
            using var peer = await acceptTask;
            await using var peerStream = peer.GetStream();

            await transport.Output.WriteAsync(new byte[] { 0x11, 0x22 });
            var sent = new byte[2];
            await peerStream.ReadExactlyAsync(sent);
            Assert.Equal(new byte[] { 0x11, 0x22 }, sent);

            await peerStream.WriteAsync(new byte[] { 0x33, 0x44 });
            var received = await transport.Input.ReadAsync();
            Assert.Equal(new byte[] { 0x33, 0x44 }, received.Buffer.Slice(0, 2).ToArray());
            transport.Input.AdvanceTo(received.Buffer.GetPosition(2));

            await transport.CloseAsync();
            Assert.False(transport.IsOpen);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Pending_read_observes_caller_cancellation()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            await using var transport = new TcpByteStreamTransport(
                IPAddress.Loopback.ToString(),
                endpoint.Port);
            var acceptTask = listener.AcceptTcpClientAsync();
            await transport.OpenAsync();
            using var peer = await acceptTask;
            using var cancellation = new CancellationTokenSource();

            // 对端保持连接但不发送数据，确保读取只能由调用方取消结束。
            var readTask = transport.Input.ReadAsync(cancellation.Token).AsTask();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => readTask);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Remote_close_completes_input_and_local_close_remains_idempotent()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            await using var transport = new TcpByteStreamTransport(
                IPAddress.Loopback.ToString(),
                endpoint.Port);
            var acceptTask = listener.AcceptTcpClientAsync();
            await transport.OpenAsync();
            using var peer = await acceptTask;

            peer.Dispose();
            var read = await transport.Input.ReadAsync().AsTask()
                .WaitAsync(TimeSpan.FromSeconds(1));
            transport.Input.AdvanceTo(read.Buffer.End);

            Assert.True(read.IsCompleted);
            await transport.CloseAsync();
            await transport.CloseAsync();
            Assert.False(transport.IsOpen);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Dispose_is_idempotent_and_prevents_reopening()
    {
        var transport = new TcpByteStreamTransport(
            IPAddress.Loopback.ToString(),
            1);

        await transport.DisposeAsync();
        await transport.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => transport.OpenAsync().AsTask());
        Assert.False(transport.IsOpen);
    }
}
