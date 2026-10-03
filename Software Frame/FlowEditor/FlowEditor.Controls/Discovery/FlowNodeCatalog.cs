// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：IFlowNodeCatalog、FlowNodeDescriptor、FlowNodeDiscoveryDiagnostic、FlowNodeCatalog。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Reflection、FlowEditor.Controls.Nodes、FlowEditor.Core、FlowEditor.Core.Nodes。导入不代表所有类型都会在运行时加载。
using System.Reflection;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core;
using FlowEditor.Core.Nodes;

namespace FlowEditor.Controls.Discovery;

/// <summary>宿主内共享的节点目录；注册、工具箱与文档映射消费同一份描述。</summary>
public interface IFlowNodeCatalog
{
    /// <summary>全部具体节点类型，包括隐藏工具和无工具元数据的兼容类型。</summary>
    IReadOnlyList<FlowNodeDescriptor> Nodes { get; }
    /// <summary>程序集发现诊断，保留来源与原始异常。</summary>
    IReadOnlyList<FlowNodeDiscoveryDiagnostic> Diagnostics { get; }
    /// <summary>完成一次发现并返回节点数；后续调用使用同一快照。</summary>
    int Discover();
    /// <summary>按稳定标识或旧 CLR 身份在当前目录中解析类型。</summary>
    Type? TryResolveType(string? nodeTypeId, string? typeName);
}

/// <summary>只读节点描述，图标保持字符串，由表现层转换。</summary>
/// <param name="NodeType">运行时节点类型。</param>
/// <param name="NodeTypeId">已有稳定标识，无标记时为空。</param>
/// <param name="DisplayName">工具显示名，无 FlowTool 标记时为空。</param>
/// <param name="ToolGroup">原工具分组。</param>
/// <param name="ToolSubGroup">原工具子分组。</param>
/// <param name="IconResource">原图标键。</param>
/// <param name="SortIndex">原排序权重。</param>
/// <param name="Description">原描述。</param>
/// <param name="VisibleInToolbox">原工具箱可见标志。</param>
public sealed record FlowNodeDescriptor(Type NodeType, string? NodeTypeId, string? DisplayName,
    string ToolGroup, string ToolSubGroup, string IconResource, int SortIndex,
    string Description, bool VisibleInToolbox);

/// <summary>目录发现诊断。</summary>
/// <param name="Source">程序集身份。</param>
/// <param name="Message">加载问题说明。</param>
/// <param name="Cause">原始异常。</param>
public sealed record FlowNodeDiscoveryDiagnostic(string Source, string Message, Exception? Cause);

/// <summary>集中读取程序集与节点元数据；不构造节点、运行工具或创建窗口。</summary>
public sealed class FlowNodeCatalog : IFlowNodeCatalog
{
    private readonly object _gate = new();
    private readonly IReadOnlyList<Assembly> _assemblies;
    private readonly Func<Assembly, Type[]> _getTypes;
    private readonly bool _strict;
    private readonly List<FlowNodeDiscoveryDiagnostic> _diagnostics = new();
    private IReadOnlyList<FlowNodeDescriptor>? _nodes;

    /// <summary>兼容默认宿主：内建、可选 FlowNodes 与当前已加载的非系统程序集。</summary>
    public FlowNodeCatalog() : this(GetDefaultAssemblies(), assembly => assembly.GetTypes(), false) { }

    /// <summary>宿主指定允许的程序集；这些显式来源加载失败时直接报告错误。</summary>
    /// <param name="assemblies">允许的节点程序集，重复项只扫描一次。</param>
    public FlowNodeCatalog(IEnumerable<Assembly> assemblies)
        : this(assemblies, assembly => assembly.GetTypes(), true) { }

    /// <summary>为发现失败回归提供类型枚举入口，生产仍使用 Assembly.GetTypes。</summary>
    /// <param name="assemblies">允许的程序集。</param>
    /// <param name="getTypes">类型枚举入口。</param>
    /// <param name="strict">是否将所有来源视为必需来源。</param>
    internal FlowNodeCatalog(IEnumerable<Assembly> assemblies, Func<Assembly, Type[]> getTypes, bool strict)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        _assemblies = Array.AsReadOnly(assemblies.Where(assembly => !assembly.IsDynamic).Distinct().ToArray());
        _getTypes = getTypes ?? throw new ArgumentNullException(nameof(getTypes));
        _strict = strict;
    }

    /// <inheritdoc />
    public IReadOnlyList<FlowNodeDescriptor> Nodes
    {
        get { Discover(); return _nodes!; }
    }

    /// <inheritdoc />
    public IReadOnlyList<FlowNodeDiscoveryDiagnostic> Diagnostics
    {
        get { lock (_gate) return Array.AsReadOnly(_diagnostics.ToArray()); }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Discover。
    /// 正文中的关键协作调用包括 Clear、_getTypes、Join、Select、Where；具体条件和先后次序见方法体。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public int Discover()
    {
        lock (_gate)
        {
            if (_nodes != null) return _nodes.Count;
            _diagnostics.Clear();
            var nodes = new List<FlowNodeDescriptor>();
            foreach (var assembly in _assemblies)
            {
                Type[] types;
                try { types = _getTypes(assembly); }
                catch (Exception error)
                {
                    var details = error is ReflectionTypeLoadException loadError
                        ? string.Join("; ", loadError.LoaderExceptions.Where(cause => cause != null).Select(cause => cause!.Message))
                        : error.Message;
                    var message = $"Cannot discover flow nodes in {assembly.FullName}: {details}";
                    _diagnostics.Add(new(assembly.FullName ?? assembly.GetName().Name ?? "unknown", message, error));
                    if (_strict || assembly == typeof(NodeModel).Assembly || assembly.GetName().Name == "FlowNodes")
                        throw new InvalidOperationException(message, error);
                    continue;
                }

                foreach (var type in types.Where(type => !type.IsAbstract && !type.ContainsGenericParameters && type.IsSubclassOf(typeof(NodeModel))))
                {
                    var tool = type.GetCustomAttribute<FlowToolAttribute>();
                    var id = type.GetCustomAttribute<NodeTypeIdAttribute>()?.TypeId;
                    nodes.Add(new(type, id, tool?.ToolName, tool?.ToolGroup ?? "常规", tool?.ToolSubGroup ?? "",
                        tool?.IconResource ?? "", tool?.SortIndex ?? 0, tool?.Description ?? "", tool?.VisibleInToolbox ?? false));
                }
            }

            var distinct = nodes.DistinctBy(node => node.NodeType).ToArray();
            var duplicate = distinct.Where(node => node.NodeTypeId != null)
                .GroupBy(node => node.NodeTypeId, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
            {
                var error = new InvalidOperationException($"Duplicate node identity '{duplicate.Key}': " +
                    string.Join("; ", duplicate.Select(node => node.NodeType.AssemblyQualifiedName)));
                _diagnostics.Add(new("NodeTypeId", error.Message, error));
                throw error;
            }

            foreach (var node in distinct) NodeTypeRegistry.Register(node.NodeType);
            _nodes = Array.AsReadOnly(distinct);
            return _nodes.Count;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryResolveType。
    /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、FirstOrDefault、Trim、TryResolveLegacyAlias、Split；具体条件和先后次序见方法体。
    /// </remarks>
    public Type? TryResolveType(string? nodeTypeId, string? typeName)
    {
        var nodes = Nodes;
        if (!string.IsNullOrWhiteSpace(nodeTypeId))
        {
            var byId = nodes.FirstOrDefault(node => node.NodeTypeId == nodeTypeId.Trim());
            if (byId != null) return byId.NodeType;
        }
        if (string.IsNullOrWhiteSpace(typeName)) return null;
        if (NodeTypeRegistry.TryResolveLegacyAlias(typeName, out var alias))
        {
            var byAlias = nodes.FirstOrDefault(node => node.NodeTypeId == alias);
            if (byAlias != null) return byAlias.NodeType;
        }
        var fullName = typeName.Split(',')[0].Trim();
        var exact = nodes.FirstOrDefault(node => node.NodeType.FullName == fullName);
        if (exact != null) return exact.NodeType;
        var migrated = FrameworkTypeResolver.ResolvePersistedType(typeName);
        return nodes.FirstOrDefault(node => node.NodeType == migrated)?.NodeType;
    }

    /// <summary>默认兼容来源只在创建目录时取一次，后续消费者不再枚举 AppDomain。</summary>
    private static IEnumerable<Assembly> GetDefaultAssemblies()
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic &&
            !assembly.FullName!.StartsWith("System.", StringComparison.Ordinal) &&
            !assembly.FullName.StartsWith("Microsoft.", StringComparison.Ordinal)).ToList();
        if (!loaded.Any(assembly => assembly.GetName().Name == "FlowNodes"))
        {
            var optional = LoadOptionalFlowNodes(() => Assembly.Load("FlowNodes"));
            if (optional != null) loaded.Add(optional);
        }
        loaded.Add(typeof(NodeModel).Assembly);
        return loaded;
    }

    /// <summary>保留可选 FlowNodes 缺失与依赖损坏的原始区分。</summary>
    /// <param name="load">程序集加载入口。</param>
    internal static Assembly? LoadOptionalFlowNodes(Func<Assembly> load)
    {
        try { return load(); }
        catch (System.IO.FileNotFoundException error) when (string.Equals(error.FileName?.Split(',')[0], "FlowNodes", StringComparison.OrdinalIgnoreCase))
        {
            System.Diagnostics.Trace.TraceInformation("Optional FlowNodes assembly is not deployed: {0}", error.Message);
            return null;
        }
        catch (Exception error) { throw new InvalidOperationException("Cannot load FlowNodes or its dependencies.", error); }
    }
}
