namespace Communication.Controls.Services;

/// <summary>
/// 参数中文字典 —— CommunicationNew 的 Param 属性名是英文，
/// 对话框反射生成表单时查此表显示中文名与中文提示；查不到回退英文名。
/// 新增协议参数后只需在此加一行，无需改对话框逻辑。
/// </summary>
public static class ParamMetadata
{
    private sealed record Meta(string Label, string Hint);

    private static readonly Dictionary<string, Meta> Map = new(StringComparer.Ordinal)
    {
        // ===== CommunicationParam 基类（通用参数）=====
        ["ProtocolType"] = new("协议类型", "通信协议，走 CommunicationNew 对应 adapter"),
        ["Role"] = new("角色", "客户端 / 服务端"),
        ["DeviceName"] = new("设备名称", "工作台内显示名"),
        ["DeviceId"] = new("设备ID", "自动生成，持久化恢复时保留"),
        ["TimeoutMs"] = new("读写超时（毫秒）", "单次读写/连接超时，超时会取消真实 IO"),
        ["RetryCount"] = new("重试次数", "读操作失败重试次数，写操作默认不隐式重试"),
        ["RetryDelayMs"] = new("重试间隔（毫秒）", "每次重试前等待时间"),
        ["AutoReconnect"] = new("自动重连", "断线后由 Runtime supervisor 自动恢复"),
        ["ReconnectIntervalMs"] = new("重连间隔（毫秒）", "自动重连等待间隔"),
        ["EventQueueCapacity"] = new("事件队列容量", "用户事件通知队列上限，满时丢弃最旧并计数"),
        ["ReceiveQueueCapacity"] = new("接收队列容量", "被动收件箱缓冲的报文条数上限"),
        ["RequestQueueCapacity"] = new("请求队列容量", "等待执行权的请求上限，满时明确拒绝"),
        ["ServerRequestCapacity"] = new("服务端在途容量", "服务端同时处理请求上限，满时返回忙"),

        // ===== Socket（TCP/UDP 共用）=====
        ["ServerIP"] = new("服务器IP", "如 127.0.0.1（客户端目标 / 服务端监听）"),
        ["Port"] = new("端口", "TCP/UDP 端口，如 ModbusTCP 默认 502"),
        ["LocalIP"] = new("本地绑定IP", "服务端监听或 UDP 客户端绑定，默认 0.0.0.0"),
        ["LocalPort"] = new("本地端口", "0 = 自动分配"),
        ["BufferSize"] = new("缓冲区大小", "接收/读写缓冲区字节数"),
        ["Encoding"] = new("编码", "文本收发编码，默认 UTF-8"),
        ["Delimiter"] = new("分包结束符", "如 \\r\\n；为空则不按结束符分包"),
        ["EnableHeartbeat"] = new("启用心跳", "定时发送心跳保持连接"),
        ["HeartbeatIntervalMs"] = new("心跳间隔（毫秒）", "心跳发送周期"),
        ["HeartbeatData"] = new("心跳数据", "十六进制，如 00 01；为空则不带负载"),
        ["MaxConnections"] = new("最大连接数", "服务端允许的最大连接数"),

        // ===== Modbus =====
        ["SlaveId"] = new("从站地址", "1～247"),
        ["HoldingRegisterSize"] = new("保持寄存器区大小", "服务端模式数据区长度"),
        ["InputRegisterSize"] = new("输入寄存器区大小", "服务端模式数据区长度"),
        ["CoilSize"] = new("线圈区大小", "服务端模式数据区长度"),
        ["DiscreteInputSize"] = new("离散输入区大小", "服务端模式数据区长度"),
        ["PortName"] = new("串口号", "如 COM1，可下拉选择或手工输入"),
        ["BaudRate"] = new("波特率", "如 9600 / 115200"),
        ["DataBits"] = new("数据位", "一般为 8"),
        ["StopBits"] = new("停止位", "One / Two / OnePointFive"),
        ["Parity"] = new("校验位", "None / Odd / Even"),
        ["Handshake"] = new("握手协议", "一般 None"),

        // ===== 三菱 MC =====
        ["Backend"] = new("MC 后端", "Slmp（免 MX 驱动）/ MxComponent（需安装 MX）"),
        ["MxBridgeExecutablePath"] = new("桥接程序路径", "为空则直接调用 64 位 MX Interop"),
        ["MxLogicalStationNumber"] = new("逻辑站号", "0～1023，MX Setup 中配置"),
        ["MxPassword"] = new("逻辑站密码", "为空表示无密码"),
        ["MxBridgeStartupTimeoutMs"] = new("桥接启动超时（毫秒）", "等待桥接进程建连"),
        ["SerialPortName"] = new("串口名（已停用）", "当前 MC 只支持 TCP，设置后会显式拒绝"),
        ["NetworkNo"] = new("网络号", "0～15"),
        ["StationNo"] = new("站号", "0～255，默认 255"),
        ["ModuleNo"] = new("模块号", "默认 1023（主模块）"),
        ["FrameType"] = new("帧类型", "3E / 4E"),
        ["CommunicationTimeout"] = new("通信超时（毫秒）", "未显式设置时跟随读写超时"),

        // ===== 串口 Raw =====
        ["DtrEnable"] = new("DTR 使能", "数据终端就绪信号"),
        ["RtsEnable"] = new("RTS 使能", "请求发送信号"),

        // ===== OPC UA =====
        ["EndpointUrl"] = new("端点URL", "如 opc.tcp://127.0.0.1:4840"),
        ["SecurityMode"] = new("安全模式", "None / Sign / SignAndEncrypt，生产用 SignAndEncrypt"),
        ["SecurityPolicy"] = new("安全策略", "None / Basic256Sha256 / Aes128 / Aes256"),
        ["UserName"] = new("用户名", "为空 = 匿名；None 模式下拒绝发送凭据"),
        ["Password"] = new("密码", "OPC UA 用户密码"),
        ["OperationTimeout"] = new("操作超时（毫秒）", "OPC UA 会话操作超时"),
        ["ApplicationName"] = new("应用名称", "OPC UA 应用实例名"),
        ["PkiRootPath"] = new("PKI 根目录", "为空则用系统默认目录存证书"),
        ["AutoAcceptUntrustedCertificates"] = new("自动接受未信任证书", "仅本地回环/测试开启，生产必须关闭"),
        ["MinimumCertificateKeySize"] = new("证书最小密钥长度", "默认 2048 位"),
        ["ServerNamespaceUri"] = new("命名空间URI", "服务端模式节点命名空间"),
        ["DefaultSamplingInterval"] = new("默认采样间隔（毫秒）", "服务端模式订阅采样周期"),

        // ===== HTTP =====
        ["BaseUrl"] = new("基础URL", "客户端 API 基地址 / 服务端监听前缀"),
        ["ContentType"] = new("内容类型", "如 application/json"),
        ["DefaultMethod"] = new("默认方法", "GET / POST / PUT / DELETE"),
        ["KeepAlive"] = new("长连接", "复用 HTTP 连接"),
        ["IgnoreSslErrors"] = new("忽略SSL错误", "仅测试环境开启"),

        // ===== WebApi =====
        ["ApiPrefix"] = new("API 前缀", "如 /api"),
        ["BearerToken"] = new("Bearer 令牌", "认证头 Authorization"),
        ["ApiKey"] = new("API Key", "认证用 Key"),
        ["ApiKeyHeaderName"] = new("Key 头名称", "默认 X-API-Key"),
        ["OperationTimeoutMs"] = new("操作超时（毫秒）", "WebApi 请求超时"),
    };

    /// <summary>中文显示名，查不到回退英文属性名。</summary>
    public static string GetLabel(string propertyName) =>
        Map.TryGetValue(propertyName, out var meta) ? meta.Label : propertyName;

    /// <summary>中文提示（含英文名后缀，方便对照代码），查不到返回英文名。</summary>
    public static string GetHint(string propertyName) =>
        Map.TryGetValue(propertyName, out var meta) ? $"{meta.Hint}（{propertyName}）" : propertyName;
}
