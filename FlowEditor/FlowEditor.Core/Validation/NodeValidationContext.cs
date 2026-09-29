namespace FlowEditor.Core;

/// <summary>
/// 节点验证上下文（P1）。流程保存、部署、启动前做整图验证时传入，
/// 目标是在真正执行设备动作前列出全部可发现问题（缺设备、缺输入、参数错误、缺插件）。
/// </summary>
public sealed class NodeValidationContext
{
    /// <summary>创建验证上下文。</summary>
    /// <param name="nodeId">待验证节点 Id。</param>
    /// <param name="nodeName">待验证节点显示名。</param>
    /// <param name="services">受限服务容器（可为空，用于解析设备/插件可用性）。</param>
    /// <param name="variables">流程变量快照（可为空）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public NodeValidationContext(
        string nodeId,
        string? nodeName,
        IServiceProvider? services = null,
        IReadOnlyDictionary<string, object?>? variables = null,
        CancellationToken cancellationToken = default)
    {
        NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        NodeName = nodeName ?? nodeId;
        Services = services;
        Variables = variables;
        CancellationToken = cancellationToken;
    }

    /// <summary>待验证节点 Id。</summary>
    public string NodeId { get; }

    /// <summary>待验证节点显示名。</summary>
    public string NodeName { get; }

    /// <summary>受限服务容器（可为空）。</summary>
    public IServiceProvider? Services { get; }

    /// <summary>流程变量快照（可为空）。</summary>
    public IReadOnlyDictionary<string, object?>? Variables { get; }

    /// <summary>取消令牌。</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>从服务容器获取服务；缺失时返回 null。</summary>
    public T? GetService<T>() where T : class
    {
        if (Services == null) return null;
        try { return Services.GetService(typeof(T)) as T; }
        catch { return null; }
    }

    /// <summary>为单元测试快速构造无外部依赖的验证上下文。</summary>
    public static NodeValidationContext CreateTest(string nodeId = "test-node") =>
        new(nodeId, nodeId);
}
