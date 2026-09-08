using System.Net;
using System.Net.Sockets;
using CommunicationNew.Protocols.OpcUa;
using Xunit;

namespace CommunicationNew.Tests.Protocols.OpcUa;

public sealed class OpcUaLoopbackTests
{
    [Theory]
    [InlineData("None", "None", false)]
    [InlineData("SignAndEncrypt", "Basic256Sha256", true)]
    public async Task Production_server_exposes_registered_byte_string_to_production_client(
        string securityMode,
        string securityPolicy,
        bool autoAcceptUntrustedCertificates)
    {
        var port = ReserveLoopbackPort();
        var endpoint = $"opc.tcp://localhost:{port}/CommunicationNewTest";
        var applicationName = $"CommunicationNewTest-{Guid.NewGuid():N}";
        var pkiRoot = Path.Combine(Path.GetTempPath(), applicationName);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await using var server = new OpcUaServerDevice(new OpcUaParam
            {
                EndpointUrl = endpoint,
                ApplicationName = applicationName + "-Server",
                ServerNamespaceUri = "urn:communicationnew:test",
                PkiRootPath = pkiRoot,
                SecurityMode = securityMode,
                SecurityPolicy = securityPolicy,
                // 回环测试每次生成临时证书，没有可预先部署的信任链。
                AutoAcceptUntrustedCertificates = autoAcceptUntrustedCertificates,
            });
            Exception? serverError = null;
            server.ErrorOccurred += (_, args) => serverError = args.Exception;
            server.RegisterNode("ns=2;s=Value", new byte[] { 0x2A });

            Assert.True(
                await server.ConnectAsync(timeout.Token),
                serverError?.ToString() ?? "OPC UA Server 启动未返回错误详情。");

            await using var client = new OpcUaClientDevice(new OpcUaParam
            {
                EndpointUrl = endpoint,
                ApplicationName = applicationName + "-Client",
                OperationTimeout = 5000,
                PkiRootPath = Path.Combine(pkiRoot, "client"),
                SecurityMode = securityMode,
                SecurityPolicy = securityPolicy,
                AutoAcceptUntrustedCertificates = autoAcceptUntrustedCertificates,
            });
            Exception? clientError = null;
            client.ErrorOccurred += (_, args) => clientError = args.Exception;
            Assert.True(
                await client.ConnectAsync(timeout.Token),
                clientError?.ToString() ?? "OPC UA Client 连接未返回错误详情。");
            Assert.Equal(
                new byte[] { 0x2A },
                await client.ReadAsync("ns=2;s=Value", 1, timeout.Token));

            Assert.True(await client.WriteAsync(
                "ns=2;s=Value",
                new byte[] { 0x2B },
                timeout.Token));
            Assert.Equal(
                new byte[] { 0x2B },
                await server.ReadAsync("ns=2;s=Value", 1, timeout.Token));
        }
        finally
        {
            if (Directory.Exists(pkiRoot))
            {
                Directory.Delete(pkiRoot, recursive: true);
            }
        }
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
