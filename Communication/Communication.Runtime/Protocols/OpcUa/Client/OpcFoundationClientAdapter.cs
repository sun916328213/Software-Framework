using System.Net;
using System.Text;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

namespace CommunicationNew.Protocols.OpcUa;

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
    public async ValueTask DisposeAsync() =>
        await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);

    private ISession GetSession() =>
        _session is { Connected: true } session
            ? session
            : throw new InvalidOperationException("OPC UA Session 尚未连接。");

}
