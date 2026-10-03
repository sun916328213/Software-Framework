// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：OpcFoundationClientAdapter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.OpcUa、System.Net、System.Text、Opc.Ua、Opc.Ua.Client、Opc.Ua.Configuration。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.OpcUa;
using System.Net;
using System.Text;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

namespace Communication.Runtime.Protocols.OpcUa;

/// <summary>基于 OPC Foundation .NET Standard SDK 的生产客户端 adapter。</summary>
internal sealed class OpcFoundationClientAdapter : IOpcUaClientAdapter
{
    private readonly OpcUaParam _param;
    private readonly ITelemetryContext _telemetry = DefaultTelemetry.Create(_ => { });
    private ISession? _session;

    /// <summary>使用设备配置创建 adapter。</summary>
    public OpcFoundationClientAdapter(OpcUaParam param)
    {
        ArgumentNullException.ThrowIfNull(param);
        _param = param;
    }

    /// <inheritdoc />
    public bool IsConnected => _session?.Connected == true;

    /// <inheritdoc />
    /// <remarks>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 Parse、GetHostName、Combine、GetFolderPath、ConfigureAwait；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public async ValueTask ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsConnected)
        {
            return;
        }

        var securityProfile = OpcUaSecurityProfile.Parse(_param);
        _ = new Uri(_param.EndpointUrl, UriKind.Absolute);

        var application = new ApplicationInstance(_telemetry)
        {
            ApplicationName = _param.ApplicationName,
            ApplicationType = ApplicationType.Client,
        };
        var applicationUri = $"urn:{Dns.GetHostName()}:{_param.ApplicationName}";
        var pkiRoot = _param.PkiRootPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CommunicationNew",
            "pki");
        var applicationCertificates = new CertificateIdentifierCollection
        {
            new()
            {
                StoreType = "Directory",
                StorePath = Path.Combine(pkiRoot, "own"),
                SubjectName = $"CN={_param.ApplicationName}, DC={Environment.MachineName}",
                CertificateType = ObjectTypeIds.RsaSha256ApplicationCertificateType,
            },
        };
        var configuration = await application
            .Build(applicationUri, applicationUri)
            .AsClient()
            .AddSecurityConfiguration(
                applicationCertificates,
                pkiRoot,
                Path.Combine(pkiRoot, "rejected"))
            .SetAutoAcceptUntrustedCertificates(_param.AutoAcceptUntrustedCertificates)
            .SetRejectSHA1SignedCertificates(true)
            .SetMinimumCertificateKeySize(_param.MinimumCertificateKeySize)
            .CreateAsync(cancellationToken)
            .ConfigureAwait(false);
        configuration.TransportQuotas.OperationTimeout = _param.OperationTimeout;
        var hasCertificate = await application
            .CheckApplicationInstanceCertificatesAsync(
                silent: true,
                null,
                cancellationToken)
            .ConfigureAwait(false);
        if (!hasCertificate)
        {
            throw new InvalidOperationException("无法创建或加载 OPC UA Client 应用实例证书。");
        }

        var endpointDescription = await CoreClientUtils.SelectEndpointAsync(
                configuration,
                _param.EndpointUrl,
                securityProfile.UseSecurity,
                _param.OperationTimeout,
                _telemetry,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new ServiceResultException(StatusCodes.BadServerNotConnected);
        if (endpointDescription.SecurityMode != securityProfile.Mode ||
            !string.Equals(
                endpointDescription.SecurityPolicyUri,
                securityProfile.PolicyUri,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"OPC UA 服务端未选中期望的安全端点：" +
                $"{securityProfile.Mode}/{SecurityPolicies.GetDisplayName(securityProfile.PolicyUri)}。");
        }
        var endpoint = new ConfiguredEndpoint(
            null,
            endpointDescription,
            EndpointConfiguration.Create(configuration));

        var identity = string.IsNullOrWhiteSpace(_param.UserName)
            ? new UserIdentity()
            : new UserIdentity(
                _param.UserName,
                Encoding.UTF8.GetBytes(_param.Password ?? string.Empty));

        var sessionFactory = new DefaultSessionFactory(_telemetry);
        _session = await sessionFactory.CreateAsync(
                configuration,
                endpoint,
                updateBeforeConnect: false,
                checkDomain: securityProfile.UseSecurity,
                _param.ApplicationName,
                (uint)_param.OperationTimeout,
                identity,
                preferredLocales: default,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、CloseAsync、Dispose；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public async ValueTask DisconnectAsync(CancellationToken cancellationToken)
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        _session = null;
        try
        {
            await session.CloseAsync(
                    _param.OperationTimeout,
                    closeChannel: true,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            session.Dispose();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 读取当前实现提供的数据；对应入口为 ReadAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、ReadValueAsync、GetSession、Parse、IsBad；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public async ValueTask<byte[]> ReadAsync(
        string nodeId,
        CancellationToken cancellationToken)
    {
        var value = await GetSession()
            .ReadValueAsync(NodeId.Parse(nodeId), cancellationToken)
            .ConfigureAwait(false);
        if (StatusCode.IsBad(value.StatusCode))
        {
            throw new ServiceResultException(value.StatusCode);
        }

        return value.Value switch
        {
            byte byteValue => new[] { byteValue },
            byte[] bytes => bytes.ToArray(),
            _ => throw new InvalidDataException(
                $"节点 {nodeId} 的值不是 Byte 或 ByteString，旧 byte[] 接口无法表示。"),
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行写入操作；对应入口为 WriteAsync。
    /// 正文中的关键协作调用包括 ThrowIfNull、Parse、ToArray、ConfigureAwait、WriteAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public async ValueTask WriteAsync(
        string nodeId,
        byte[] value,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        var nodes = new WriteValueCollection
        {
            new()
            {
                NodeId = NodeId.Parse(nodeId),
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(value.ToArray())),
            },
        };
        var response = await GetSession()
            .WriteAsync(null, nodes, cancellationToken)
            .ConfigureAwait(false);
        var statusCode = response.Results[0];
        if (StatusCode.IsBad(statusCode))
        {
            throw new ServiceResultException(statusCode);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 DisposeAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、DisconnectAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public async ValueTask DisposeAsync() =>
        await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetSession。
    /// </summary>
    private ISession GetSession() =>
        _session is { Connected: true } session
            ? session
            : throw new InvalidOperationException("OPC UA Session 尚未连接。");

}
