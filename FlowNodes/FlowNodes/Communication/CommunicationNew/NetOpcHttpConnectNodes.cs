using CommunicationNew.Core;
using CommunicationNew.Protocols.Http;
using CommunicationNew.Protocols.OpcUa;
using CommunicationNew.Protocols.WebApi;
using FlowEditor.Core;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.CommunicationNew;

/// <summary>新通讯连接（OPC UA）：OPC UA 客户端/服务端。</summary>
[FlowTool("新通讯连接（OPC UA）", "LanConnect", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 16, Description = "创建并连接OPC UA设备")]
internal class NetOpcUaConnectNode : NetConnectNodeBase
{
    public NetOpcUaConnectNode(CommunicationManager manager) : base(manager) { }

    public override ProtocolType Protocol => ProtocolType.OpcUa;
    protected override string DefaultDeviceName => "OpcUA1";

    private DeviceRole _role = DeviceRole.Client;
    [NodeProperty(DisplayName = "角色", Group = "1.基础", Order = 1, Description = "客户端主动连接，服务端被动监听")]
    public DeviceRole Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

    private string _endpointUrl = "opc.tcp://127.0.0.1:4840";
    [NodeProperty(DisplayName = "端点URL", Group = "2.参数", Order = 10, Description = "如 opc.tcp://127.0.0.1:4840")]
    public string EndpointUrl
    {
        get => _endpointUrl;
        set => SetProperty(ref _endpointUrl, value);
    }

    private string _userName = "";
    [NodeProperty(DisplayName = "用户名", Group = "2.参数", Order = 11, Description = "为空表示匿名")]
    public string UserName
    {
        get => _userName;
        set => SetProperty(ref _userName, value);
    }

    private string _password = "";
    [NodeProperty(DisplayName = "密码", Group = "2.参数", Order = 12)]
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    private bool _autoAcceptUntrustedCertificates;
    [NodeProperty(DisplayName = "接受未授信证书", Group = "2.参数", Order = 13, Description = "仅本地回环或测试环境开启")]
    public bool AutoAcceptUntrustedCertificates
    {
        get => _autoAcceptUntrustedCertificates;
        set => SetProperty(ref _autoAcceptUntrustedCertificates, value);
    }

    protected override CommunicationParam BuildParam() => new OpcUaParam
    {
        Role = Role,
        EndpointUrl = EndpointUrl,
        UserName = string.IsNullOrWhiteSpace(UserName) ? null : UserName,
        Password = string.IsNullOrEmpty(Password) ? null : Password,
        AutoAcceptUntrustedCertificates = AutoAcceptUntrustedCertificates,
    };
}

/// <summary>新通讯连接（HTTP）：HTTP 客户端/服务端。</summary>
[FlowTool("新通讯连接（HTTP）", "LanConnect", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 17, Description = "创建并连接HTTP设备")]
internal class NetHttpConnectNode : NetConnectNodeBase
{
    public NetHttpConnectNode(CommunicationManager manager) : base(manager) { }

    public override ProtocolType Protocol => ProtocolType.Http;
    protected override string DefaultDeviceName => "Http1";

    private DeviceRole _role = DeviceRole.Client;
    [NodeProperty(DisplayName = "角色", Group = "1.基础", Order = 1, Description = "客户端主动请求，服务端被动监听")]
    public DeviceRole Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

    private string _baseUrl = "http://127.0.0.1:8080";
    [NodeProperty(DisplayName = "基地址", Group = "2.参数", Order = 10, Description = "客户端填API基地址，服务端填监听前缀")]
    public string BaseUrl
    {
        get => _baseUrl;
        set => SetProperty(ref _baseUrl, value);
    }

    private bool _ignoreSslErrors;
    [NodeProperty(DisplayName = "忽略证书错误", Group = "2.参数", Order = 11, Description = "HTTPS 自签证书时开启")]
    public bool IgnoreSslErrors
    {
        get => _ignoreSslErrors;
        set => SetProperty(ref _ignoreSslErrors, value);
    }

    protected override CommunicationParam BuildParam() => new HttpParam
    {
        Role = Role,
        BaseUrl = BaseUrl,
        IgnoreSslErrors = IgnoreSslErrors,
    };
}

/// <summary>新通讯连接（WebAPI）：RESTful WebAPI 客户端/服务端。</summary>
[FlowTool("新通讯连接（WebAPI）", "LanConnect", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 18, Description = "创建并连接WebAPI设备")]
internal class NetWebApiConnectNode : NetConnectNodeBase
{
    public NetWebApiConnectNode(CommunicationManager manager) : base(manager) { }

    public override ProtocolType Protocol => ProtocolType.WebApi;
    protected override string DefaultDeviceName => "WebAPI1";

    private DeviceRole _role = DeviceRole.Client;
    [NodeProperty(DisplayName = "角色", Group = "1.基础", Order = 1, Description = "客户端主动请求，服务端被动监听")]
    public DeviceRole Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

    private string _baseUrl = "http://127.0.0.1:5000";
    [NodeProperty(DisplayName = "基地址", Group = "2.参数", Order = 10)]
    public string BaseUrl
    {
        get => _baseUrl;
        set => SetProperty(ref _baseUrl, value);
    }

    private string _apiPrefix = "/api";
    [NodeProperty(DisplayName = "API前缀", Group = "2.参数", Order = 11, Description = "如 /api/v1")]
    public string ApiPrefix
    {
        get => _apiPrefix;
        set => SetProperty(ref _apiPrefix, value);
    }

    private string _bearerToken = "";
    [NodeProperty(DisplayName = "BearerToken", Group = "2.参数", Order = 12, Description = "认证用，为空表示不启用")]
    public string BearerToken
    {
        get => _bearerToken;
        set => SetProperty(ref _bearerToken, value);
    }

    private bool _ignoreSslErrors;
    [NodeProperty(DisplayName = "忽略证书错误", Group = "2.参数", Order = 13)]
    public bool IgnoreSslErrors
    {
        get => _ignoreSslErrors;
        set => SetProperty(ref _ignoreSslErrors, value);
    }

    protected override CommunicationParam BuildParam() => new WebApiParam
    {
        Role = Role,
        BaseUrl = BaseUrl,
        ApiPrefix = ApiPrefix,
        BearerToken = string.IsNullOrWhiteSpace(BearerToken) ? null : BearerToken,
        IgnoreSslErrors = IgnoreSslErrors,
    };
}
