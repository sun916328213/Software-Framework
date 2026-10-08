// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：OpcUaLoopbackTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Net、System.Net.Sockets、Communication.Core.Protocols.OpcUa、Communication.Runtime.Protocols.OpcUa、Xunit。导入不代表所有类型都会在运行时加载。
using System.Net;
using System.Net.Sockets;
using Communication.Core.Protocols.OpcUa;
using Communication.Runtime.Protocols.OpcUa;
using Xunit;

namespace Communication.Tests.Protocols.OpcUa;

/// <summary>
/// OpcUaLoopbackTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class OpcUaLoopbackTests
{
    /// <summary>
    /// 测试相关入口 Production_server_exposes_registered_byte_string_to_production_client；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ReserveLoopbackPort、NewGuid、Combine、GetTempPath、FromSeconds；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 ReserveLoopbackPort；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Start、Stop；具体条件和先后次序见方法体。
    /// </summary>
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
