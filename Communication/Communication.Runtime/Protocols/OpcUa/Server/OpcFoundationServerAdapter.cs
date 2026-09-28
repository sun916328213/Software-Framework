using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;

namespace CommunicationNew.Protocols.OpcUa;

/// <summary>基于 OPC Foundation <see cref="StandardServer"/> 的生产服务端 adapter。</summary>
internal sealed class OpcFoundationServerAdapter : IOpcUaServerAdapter
{
    private readonly OpcUaParam _param;
    private readonly Dictionary<string, byte[]> _registrations = new(StringComparer.Ordinal);
    private readonly ITelemetryContext _telemetry = DefaultTelemetry.Create(_ => { });
    private CommunicationStandardServer? _server;

    /// <summary>使用服务端配置创建 adapter。</summary>
    public OpcFoundationServerAdapter(OpcUaParam param)
    {
        ArgumentNullException.ThrowIfNull(param);
        _param = param;
    }

    /// <inheritdoc />
    public bool IsRunning => _server?.CurrentInstance is not null;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Nodes => _registrations.Keys.ToArray();

    /// <inheritdoc />
    public void RegisterNode(string nodeId, byte[] initialValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentNullException.ThrowIfNull(initialValue);
        if (IsRunning)
        {
            throw new InvalidOperationException("OPC UA Server 启动后不能再注册静态节点。");
        }

        _ = NodeId.Parse(nodeId);
        _registrations[nodeId] = initialValue.ToArray();
    }

    /// <inheritdoc />
    public async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        if (IsRunning)
        {
            return;
        }

        var securityProfile = OpcUaSecurityProfile.Parse(_param);
        _ = new Uri(_param.EndpointUrl, UriKind.Absolute);

        var application = new ApplicationInstance(_telemetry)
        {
            ApplicationName = _param.ApplicationName,
            ApplicationType = ApplicationType.Server,
        };
        var applicationUri = $"urn:{Environment.MachineName}:{_param.ApplicationName}";
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
            .AsServer(new[] { _param.EndpointUrl })
            .AddUnsecurePolicyNone(securityProfile.Mode == MessageSecurityMode.None)
            .AddSignPolicies(securityProfile.Mode == MessageSecurityMode.Sign)
            .AddSignAndEncryptPolicies(
                securityProfile.Mode == MessageSecurityMode.SignAndEncrypt)
            .AddSecurityConfiguration(
                applicationCertificates,
                pkiRoot,
                Path.Combine(pkiRoot, "rejected"))
            .SetAutoAcceptUntrustedCertificates(_param.AutoAcceptUntrustedCertificates)
            .SetRejectSHA1SignedCertificates(true)
            .SetMinimumCertificateKeySize(_param.MinimumCertificateKeySize)
            .CreateAsync(cancellationToken)
            .ConfigureAwait(false);
        // Builder 会为指定模式加入多个策略；这里只暴露调用方明确选择的端点，
        // 避免运行时意外降级到弱策略。
        configuration.ServerConfiguration.SecurityPolicies = new ServerSecurityPolicyCollection
        {
            new()
            {
                SecurityMode = securityProfile.Mode,
                SecurityPolicyUri = securityProfile.PolicyUri,
            },
        };
        var hasCertificate = await application
            .CheckApplicationInstanceCertificatesAsync(
                silent: true,
                null,
                cancellationToken)
            .ConfigureAwait(false);
        if (!hasCertificate)
        {
            throw new InvalidOperationException("无法创建或加载 OPC UA Server 应用实例证书。");
        }

        var server = new CommunicationStandardServer(
            _telemetry,
            _param.ServerNamespaceUri,
            _registrations);
        try
        {
            await server.StartAsync(configuration, cancellationToken).ConfigureAwait(false);
            _server = server;
        }
        catch
        {
            server.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        var server = _server;
        _server = null;

        if (server is not null)
        {
            await server.StopAsync(cancellationToken).ConfigureAwait(false);
            server.Dispose();
        }
    }

    /// <inheritdoc />
    public byte[] Read(string nodeId)
    {
        if (_server?.NodeManager is { } nodeManager)
        {
            return nodeManager.Read(nodeId);
        }
        return _registrations.TryGetValue(nodeId, out var value)
            ? value.ToArray()
            : throw new KeyNotFoundException($"未注册 OPC UA 节点：{nodeId}");
    }

    /// <inheritdoc />
    public void Write(string nodeId, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var server = _server ?? throw new InvalidOperationException("OPC UA Server 尚未启动。");
        server.NodeManager.Write(nodeId, value);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() =>
        await StopAsync(CancellationToken.None).ConfigureAwait(false);

    private sealed class CommunicationStandardServer : StandardServer
    {
        private readonly string _namespaceUri;
        private readonly IReadOnlyDictionary<string, byte[]> _registrations;

        public CommunicationStandardServer(
            ITelemetryContext telemetry,
            string namespaceUri,
            IReadOnlyDictionary<string, byte[]> registrations)
        {
            _namespaceUri = namespaceUri;
            _registrations = registrations;
        }

        public CommunicationNodeManager NodeManager { get; private set; } = null!;

        protected override MasterNodeManager CreateMasterNodeManager(
            IServerInternal server,
            ApplicationConfiguration configuration)
        {
            NodeManager = new CommunicationNodeManager(
                server,
                configuration,
                _namespaceUri,
                _registrations);
            return new MasterNodeManager(server, configuration, null, NodeManager);
        }
    }

    private sealed class CommunicationNodeManager : CustomNodeManager2
    {
        private readonly IReadOnlyDictionary<string, byte[]> _registrations;
        private readonly Dictionary<string, BaseDataVariableState<byte[]>> _variables =
            new(StringComparer.Ordinal);

        public CommunicationNodeManager(
            IServerInternal server,
            ApplicationConfiguration configuration,
            string namespaceUri,
            IReadOnlyDictionary<string, byte[]> registrations)
            : base(server, configuration, new[] { namespaceUri })
        {
            _registrations = registrations;
            SystemContext.NodeIdFactory = this;
        }

        public override void CreateAddressSpace(
            IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            var folder = new FolderState(null)
            {
                NodeId = new NodeId("CommunicationNew", NamespaceIndexes[0]),
                BrowseName = new QualifiedName("CommunicationNew", NamespaceIndexes[0]),
                DisplayName = "CommunicationNew",
                TypeDefinitionId = ObjectTypeIds.FolderType,
            };
            folder.AddReference(ReferenceTypeIds.Organizes, true, ObjectIds.ObjectsFolder);
            if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out var references))
            {
                externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
            }
            references.Add(new NodeStateReference(
                ReferenceTypeIds.Organizes,
                false,
                folder.NodeId));

            foreach (var registration in _registrations)
            {
                var parsed = NodeId.Parse(registration.Key);
                var variable = new BaseDataVariableState<byte[]>(folder)
                {
                    NodeId = new NodeId(parsed.Identifier, NamespaceIndexes[0]),
                    BrowseName = new QualifiedName(parsed.Identifier.ToString(), NamespaceIndexes[0]),
                    DisplayName = parsed.Identifier.ToString(),
                    TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
                    DataType = DataTypeIds.ByteString,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = registration.Value.ToArray(),
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow,
                };
                folder.AddChild(variable);
                _variables.Add(registration.Key, variable);
            }

            AddPredefinedNode(SystemContext, folder);
        }

        public byte[] Read(string nodeId)
        {
            lock (Lock)
            {
                return GetVariable(nodeId).Value.ToArray();
            }
        }

        public void Write(string nodeId, byte[] value)
        {
            lock (Lock)
            {
                var variable = GetVariable(nodeId);
                variable.Value = value.ToArray();
                variable.Timestamp = DateTime.UtcNow;
                variable.ClearChangeMasks(SystemContext, includeChildren: false);
            }
        }

        private BaseDataVariableState<byte[]> GetVariable(string nodeId) =>
            _variables.TryGetValue(nodeId, out var variable)
                ? variable
                : throw new KeyNotFoundException($"未注册 OPC UA 节点：{nodeId}");
    }
}
