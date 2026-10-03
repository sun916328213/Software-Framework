// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：OpcFoundationServerAdapter、CommunicationStandardServer、CommunicationNodeManager。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.OpcUa、Opc.Ua、Opc.Ua.Configuration、Opc.Ua.Server。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.OpcUa;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;

namespace Communication.Runtime.Protocols.OpcUa;

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
    /// <remarks>
    /// 注册类型、服务或入口映射；对应入口为 RegisterNode。
    /// 正文中的关键协作调用包括 ThrowIfNullOrWhiteSpace、ThrowIfNull、Parse、ToArray；具体条件和先后次序见方法体。
    /// </remarks>
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
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 StartAsync。
    /// 正文中的关键协作调用包括 Parse、Combine、GetFolderPath、ConfigureAwait、CreateAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
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
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 StopAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、StopAsync、Dispose；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
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
    /// <remarks>
    /// 读取当前实现提供的数据；对应入口为 Read。
    /// 正文中的关键协作调用包括 Read、TryGetValue、ToArray；具体条件和先后次序见方法体。
    /// </remarks>
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
    /// <remarks>
    /// 执行写入操作；对应入口为 Write。
    /// 正文中的关键协作调用包括 ThrowIfNull、Write；具体条件和先后次序见方法体。
    /// </remarks>
    public void Write(string nodeId, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var server = _server ?? throw new InvalidOperationException("OPC UA Server 尚未启动。");
        server.NodeManager.Write(nodeId, value);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 DisposeAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、StopAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public async ValueTask DisposeAsync() =>
        await StopAsync(CancellationToken.None).ConfigureAwait(false);

    /// <summary>
    /// CommunicationStandardServer 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
    /// 继承或实现的契约：StandardServer。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class CommunicationStandardServer : StandardServer
    {
        private readonly string _namespaceUri;
        private readonly IReadOnlyDictionary<string, byte[]> _registrations;

        /// <summary>
        /// 初始化 CommunicationStandardServer 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public CommunicationStandardServer(
            ITelemetryContext telemetry,
            string namespaceUri,
            IReadOnlyDictionary<string, byte[]> registrations)
        {
            _namespaceUri = namespaceUri;
            _registrations = registrations;
        }

        public CommunicationNodeManager NodeManager { get; private set; } = null!;

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateMasterNodeManager。
        /// </summary>
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

    /// <summary>
    /// CommunicationNodeManager 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
    /// 继承或实现的契约：CustomNodeManager2。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class CommunicationNodeManager : CustomNodeManager2
    {
        private readonly IReadOnlyDictionary<string, byte[]> _registrations;
        private readonly Dictionary<string, BaseDataVariableState<byte[]>> _variables =
            new(StringComparer.Ordinal);

        /// <summary>
        /// 初始化 CommunicationNodeManager 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
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

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateAddressSpace。
        /// 正文中的关键协作调用包括 AddReference、TryGetValue、Add、Parse、ToString；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 读取当前实现提供的数据；对应入口为 Read。
        /// 正文中的关键协作调用包括 ToArray、GetVariable；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public byte[] Read(string nodeId)
        {
            lock (Lock)
            {
                return GetVariable(nodeId).Value.ToArray();
            }
        }

        /// <summary>
        /// 执行写入操作；对应入口为 Write。
        /// 正文中的关键协作调用包括 GetVariable、ToArray、ClearChangeMasks；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
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

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetVariable。
        /// 正文中的关键协作调用包括 TryGetValue；具体条件和先后次序见方法体。
        /// </summary>
        private BaseDataVariableState<byte[]> GetVariable(string nodeId) =>
            _variables.TryGetValue(nodeId, out var variable)
                ? variable
                : throw new KeyNotFoundException($"未注册 OPC UA 节点：{nodeId}");
    }
}
