// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：INodeLogger、NullNodeLogger、CallbackNodeLogger、INodeTelemetry、NullNodeTelemetry、NodeExecutionContext。成员说明可结合下方具体实现阅读。
namespace FlowEditor.Core;

/// <summary>节点日志最小抽象（P0）。Runtime 不依赖具体日志框架，宿主负责适配。</summary>
public interface INodeLogger
{
    /// <summary>记录信息日志。</summary>
    void LogInfo(string message);

    /// <summary>记录警告日志。</summary>
    void LogWarning(string message);

    /// <summary>记录错误日志。</summary>
    void LogError(string message, Exception? ex = null);
}

/// <summary>无操作日志，用于单元测试与无头宿主。</summary>
public sealed class NullNodeLogger : INodeLogger
{
    /// <summary>共享空实现。</summary>
    public static NullNodeLogger Instance { get; } = new();

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 LogInfo。
    /// </remarks>
    public void LogInfo(string message) { }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 LogWarning。
    /// </remarks>
    public void LogWarning(string message) { }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 LogError。
    /// </remarks>
    public void LogError(string message, Exception? ex = null) { }
}

/// <summary>回调式日志，把日志转给引擎的 Action&lt;string&gt;。</summary>
public sealed class CallbackNodeLogger : INodeLogger
{
    private readonly Action<string> _log;

    /// <summary>使用日志回调创建。</summary>
    public CallbackNodeLogger(Action<string> log) => _log = log ?? (_ => { });

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 LogInfo。
    /// 正文中的关键协作调用包括 _log；具体条件和先后次序见方法体。
    /// </remarks>
    public void LogInfo(string message) => _log(message);

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 LogWarning。
    /// 正文中的关键协作调用包括 _log；具体条件和先后次序见方法体。
    /// </remarks>
    public void LogWarning(string message) => _log($"[警告] {message}");

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 LogError。
    /// 正文中的关键协作调用包括 _log、GetType；具体条件和先后次序见方法体。
    /// </remarks>
    public void LogError(string message, Exception? ex = null) =>
        _log(ex == null ? $"[错误] {message}" : $"[错误] {message} :: {ex.GetType().Name}: {ex.Message}");
}

/// <summary>节点遥测最小抽象（P0/P2）。引擎与节点只记录事实，Workbench 负责展示。</summary>
public interface INodeTelemetry
{
    /// <summary>记录节点完成一次执行（含耗时与失败类型）。</summary>
    void RecordExecution(string nodeId, string nodeName, long elapsedMs, bool success, string? errorCode);

    /// <summary>记录资源锁等待（P1/P2 并发调度使用）。</summary>
    void RecordResourceWait(string resourceKey, long waitMs);

    /// <summary>记录重试。</summary>
    void RecordRetry(string nodeId, int attempt);

    /// <summary>记录带错误码的重试（默认转调无错误码版本）。</summary>
    void RecordRetry(string nodeId, int attempt, string? errorCode) => RecordRetry(nodeId, attempt);
}

/// <summary>无操作遥测，用于单元测试与无头宿主。</summary>
public sealed class NullNodeTelemetry : INodeTelemetry
{
    /// <summary>共享空实现。</summary>
    public static NullNodeTelemetry Instance { get; } = new();

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 RecordExecution。
    /// </remarks>
    public void RecordExecution(string nodeId, string nodeName, long elapsedMs, bool success, string? errorCode) { }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 RecordResourceWait。
    /// </remarks>
    public void RecordResourceWait(string resourceKey, long waitMs) { }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 RecordRetry。
    /// </remarks>
    public void RecordRetry(string nodeId, int attempt) { }
}

/// <summary>
/// 节点执行上下文（P0）。由 FlowEditor Runtime 统一创建、节点执行时传入，
/// 逐步替代静态上下文与全局服务定位（Container/Manager.Instance、Application.Current 等）。
/// <para/>
/// 验收：单个 Runtime Node 可在单元测试中直接构造 Context 执行，不需要启动 WPF Application 或 Prism Container。
/// </summary>
public sealed class NodeExecutionContext
{
    /// <summary>创建执行上下文。</summary>
    /// <param name="runId">本次执行周期序号（引擎的 RunId）。</param>
    /// <param name="flowId">流程标识（可为空）。</param>
    /// <param name="nodeId">当前节点 Id。</param>
    /// <param name="nodeName">当前节点显示名。</param>
    /// <param name="cancellationToken">取消令牌；停止流程只靠此令牌，不得用 Thread.Abort 或纯布尔值。</param>
    /// <param name="logger">节点日志；为空时使用空实现。</param>
    /// <param name="services">受限服务容器；为空表示无外部服务（单元测试场景）。节点不得自行访问全局单例。</param>
    /// <param name="variables">流程变量/数据上下文快照；为空表示无变量。</param>
    /// <param name="telemetry">遥测接收器；为空时使用空实现。</param>
    public NodeExecutionContext(
        long runId,
        string? flowId,
        string nodeId,
        string? nodeName,
        CancellationToken cancellationToken,
        INodeLogger? logger = null,
        IServiceProvider? services = null,
        IReadOnlyDictionary<string, object?>? variables = null,
        INodeTelemetry? telemetry = null)
    {
        RunId = runId;
        FlowId = flowId;
        NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        NodeName = nodeName ?? nodeId;
        CancellationToken = cancellationToken;
        Logger = logger ?? NullNodeLogger.Instance;
        Services = services;
        Variables = variables;
        Telemetry = telemetry ?? NullNodeTelemetry.Instance;
    }

    /// <summary>本次执行周期序号。</summary>
    public long RunId { get; }

    /// <summary>流程标识。</summary>
    public string? FlowId { get; }

    /// <summary>当前节点 Id。</summary>
    public string NodeId { get; }

    /// <summary>当前节点显示名。</summary>
    public string NodeName { get; }

    /// <summary>取消令牌。</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>节点日志。</summary>
    public INodeLogger Logger { get; }

    /// <summary>受限服务容器（可为空）。优先用 <see cref="GetService{T}"/> 按需获取。</summary>
    public IServiceProvider? Services { get; }

    /// <summary>流程变量/数据上下文（可为空）。</summary>
    public IReadOnlyDictionary<string, object?>? Variables { get; }

    /// <summary>遥测接收器。</summary>
    public INodeTelemetry Telemetry { get; }

    /// <summary>快速取消检查。</summary>
    public void ThrowIfCancellationRequested() => CancellationToken.ThrowIfCancellationRequested();

    /// <summary>从服务容器获取服务；缺失时返回 null（节点应转为 Config/DeviceOffline 等分类错误，而不是 NullReferenceException）。</summary>
    public T? GetService<T>() where T : class
    {
        if (Services == null) return null;
        try { return Services.GetService(typeof(T)) as T; }
        catch { return null; }
    }

    /// <summary>按名读取变量；缺失或类型不符时返回 <paramref name="defaultValue"/>。</summary>
    public T? GetVariable<T>(string name, T? defaultValue = default)
    {
        if (Variables == null || name == null) return defaultValue;
        if (!Variables.TryGetValue(name, out var value)) return defaultValue;
        if (value is T typed) return typed;
        try
        {
            if (value == null) return defaultValue;
            return (T?)Convert.ChangeType(value, typeof(T));
        }
        catch { return defaultValue; }
    }

    /// <summary>为单元测试快速构造无外部依赖的上下文。</summary>
    public static NodeExecutionContext CreateTest(
        string nodeId = "test-node",
        CancellationToken ct = default,
        IReadOnlyDictionary<string, object?>? variables = null,
        IServiceProvider? services = null) =>
        new(0, "test-flow", nodeId, nodeId, ct, NullNodeLogger.Instance, services, variables, NullNodeTelemetry.Instance);
}
