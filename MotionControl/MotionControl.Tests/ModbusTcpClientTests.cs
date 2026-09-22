using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using MotionControl.Vendors.Inovance;
using Xunit;

namespace MotionControl.Tests;

public sealed class ModbusTcpClientTests
{
    [Fact]
    public async Task ReadHoldingRegisters_FragmentedResponse_IsReassembledAndValidated()
    {
        using var listener = StartListener(out var port);
        var server = ServeOnceAsync(listener, async (stream, request) =>
        {
            var response = BuildResponse(request, 0x03, [0x04, 0x12, 0x34, 0xFE, 0xDC]);
            await stream.WriteAsync(response.AsMemory(0, 3));
            await stream.WriteAsync(response.AsMemory(3));
        });
        using var client = new ModbusTcpClient { TimeoutMs = 1000 };
        Assert.True(client.Connect(IPAddress.Loopback.ToString(), port));

        var values = client.ReadHoldingRegisters(2, 100, 2);

        Assert.NotNull(values);
        Assert.Equal([unchecked((short)0x1234), unchecked((short)0xFEDC)], values);
        await server;
    }

    [Fact]
    public async Task ReadHoldingRegisters_ExceptionResponse_ReturnsWithoutWaitingForSuccessLength()
    {
        using var listener = StartListener(out var port);
        var releaseServer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = ServeOnceAsync(listener, async (stream, request) =>
        {
            await stream.WriteAsync(BuildResponse(request, 0x83, [0x02]));
            await releaseServer.Task.WaitAsync(TimeSpan.FromSeconds(3));
        });
        using var client = new ModbusTcpClient { TimeoutMs = 2000 };
        Assert.True(client.Connect(IPAddress.Loopback.ToString(), port));
        var stopwatch = Stopwatch.StartNew();

        var values = client.ReadHoldingRegisters(1, 0, 1);

        stopwatch.Stop();
        releaseServer.SetResult();
        Assert.Null(values);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1),
            $"异常响应处理耗时 {stopwatch.Elapsed.TotalMilliseconds:F0}ms，应按MBAP长度立即返回。");
        await server;
    }

    [Fact]
    public async Task ResponseWithWrongTransactionId_IsRejectedAndConnectionClosed()
    {
        using var listener = StartListener(out var port);
        var server = ServeOnceAsync(listener, async (stream, request) =>
        {
            var response = BuildResponse(request, 0x03, [0x02, 0x00, 0x01]);
            response[1]++;
            await stream.WriteAsync(response);
        });
        using var client = new ModbusTcpClient { TimeoutMs = 1000 };
        Assert.True(client.Connect(IPAddress.Loopback.ToString(), port));

        var values = client.ReadHoldingRegisters(1, 0, 1);

        Assert.Null(values);
        Assert.False(client.IsConnected);
        await server;
    }

    [Fact]
    public async Task WriteSingleRegister_WrongEcho_IsRejected()
    {
        using var listener = StartListener(out var port);
        var server = ServeOnceAsync(listener, async (stream, request) =>
        {
            var response = BuildResponse(request, 0x06, request.AsSpan(8, 4).ToArray());
            response[11]++;
            await stream.WriteAsync(response);
        });
        using var client = new ModbusTcpClient { TimeoutMs = 1000 };
        Assert.True(client.Connect(IPAddress.Loopback.ToString(), port));

        var succeeded = client.WriteSingleRegister(1, 100, 42);

        Assert.False(succeeded);
        await server;
    }

    private static TcpListener StartListener(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }

    private static async Task ServeOnceAsync(TcpListener listener,
        Func<NetworkStream, byte[], Task> respond)
    {
        using var server = await listener.AcceptTcpClientAsync();
        using var stream = server.GetStream();
        var request = await ReadAduAsync(stream);
        await respond(stream, request);
    }

    private static async Task<byte[]> ReadAduAsync(NetworkStream stream)
    {
        var header = new byte[7];
        await stream.ReadExactlyAsync(header);
        var length = (header[4] << 8) | header[5];
        var request = new byte[6 + length];
        Buffer.BlockCopy(header, 0, request, 0, header.Length);
        await stream.ReadExactlyAsync(request.AsMemory(7, length - 1));
        return request;
    }

    private static byte[] BuildResponse(byte[] request, byte functionCode, byte[] payload)
    {
        var length = 2 + payload.Length;
        var response = new byte[8 + payload.Length];
        response[0] = request[0];
        response[1] = request[1];
        response[4] = (byte)(length >> 8);
        response[5] = (byte)length;
        response[6] = request[6];
        response[7] = functionCode;
        payload.CopyTo(response, 8);
        return response;
    }
}
