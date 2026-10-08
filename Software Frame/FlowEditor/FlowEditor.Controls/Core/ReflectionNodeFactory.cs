// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：INodeFactory、ReflectionNodeFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Core、FlowEditor.Runtime、System.Collections.Concurrent、System.Reflection、System.Text.Json。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Core;
using FlowEditor.Runtime;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Discovery;

namespace FlowEditor.Controls.Core;

/// <summary>
/// 节点实例工厂抽象：从节点描述符创建独立的运行时 NodeModel 实例。
/// 编辑器宿主可注入基于 IoC 容器的实现；默认提供纯反射实现（无容器依赖，支持 headless）。
/// </summary>
public interface INodeFactory
{
    /// <summary>按运行时类型创建未应用文档状态的新节点。</summary>
    /// <param name="nodeType">需要创建的节点类型。</param>
    /// <returns>新的节点实例。</returns>
    NodeModel Create(Type nodeType);

    /// <summary>按描述符实例化节点（含基础字段、端口与附加属性还原）。</summary>
    NodeModel Create(NodeDocumentModel descriptor);
}

/// <summary>
/// 默认反射节点工厂：支持 DI 构造函数的节点物化器。
/// 实例化策略：
/// 1. 按"参数最多且全部可解析"贪心选择构造函数，无参构造作为零依赖回退；
/// 2. 有参数的构造函数中，
///    参数依次来自 RegisterService 注册表 → 实例回退解析器；
///    全部不可满足时抛出含缺失服务清单的明确异常。
/// 每个实例持有自己的注册表和可选回退解析器，不共享宿主可变状态；
/// 无头宿主直接创建独立实例，桌面宿主通过 adapter 提供解析器。
/// </summary>
public sealed class ReflectionNodeFactory : INodeFactory
{
    private readonly ConcurrentDictionary<Type, Func<object>> _serviceResolvers = new();
    private readonly Func<Type, object?>? _fallbackResolver;
    private readonly IFlowNodeCatalog? _catalog;

    private static readonly JsonSerializerOptions PropertyJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>创建具有独立依赖注册表的反射节点 factory。</summary>
    /// <param name="fallbackResolver">实例注册未命中时使用的宿主依赖解析函数。</param>
    public ReflectionNodeFactory(Func<Type, object?>? fallbackResolver = null)
        : this(fallbackResolver, null) { }

    /// <summary>使用宿主目录解析文档节点，旧式无目录路径保留反射兼容入口。</summary>
    /// <param name="fallbackResolver">宿主依赖解析入口。</param>
    /// <param name="catalog">宿主节点目录，可为空以保持直接构造兼容。</param>
    public ReflectionNodeFactory(Func<Type, object?>? fallbackResolver, IFlowNodeCatalog? catalog)
    {
        _fallbackResolver = fallbackResolver;
        _catalog = catalog;
    }

    /// <summary>
    /// P2：是否允许缺失插件兼容。开启后，无法解析的节点文档不再抛 TypeLoadException，
    /// 而是创建 <see cref="Nodes.MissingNode"/> 占位对象（完整保留原始配置和连线所需端口）；
    /// 关闭时保持旧抛异常行为。默认关闭。
    /// </summary>
    public bool AllowMissingNodes { get; set; }

    /// <summary>注册单例服务实例。</summary>
    public void RegisterService<T>(T instance) where T : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        _serviceResolvers[typeof(T)] = () => instance;
    }

    /// <summary>注册服务工厂（惰性创建）。</summary>
    public void RegisterService<T>(Func<T> factory) where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        _serviceResolvers[typeof(T)] = () => factory();
    }

    /// <summary>清空当前实例的服务注册表。</summary>
    public void ClearServices() => _serviceResolvers.Clear();

    /// <inheritdoc />
    /// <remarks>
    /// 创建当前实现提供的对象或结果；对应入口为 Create。
    /// 正文中的关键协作调用包括 ThrowIfNull、IsAssignableFrom、nameof、CreateInstance；具体条件和先后次序见方法体。
    /// </remarks>
    public NodeModel Create(Type nodeType)
    {
        ArgumentNullException.ThrowIfNull(nodeType);
        if (nodeType.IsAbstract || !typeof(NodeModel).IsAssignableFrom(nodeType))
            throw new ArgumentException($"类型 {nodeType.Name} 不是可实例化的节点类型。", nameof(nodeType));

        return CreateInstance(nodeType);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 创建当前实现提供的对象或结果；对应入口为 Create。
    /// 正文中的关键协作调用包括 ThrowIfNull、TryResolve、ResolveType、Capture、RestoreNodeState；具体条件和先后次序见方法体。
    /// </remarks>
    public NodeModel Create(NodeDocumentModel descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        // P2：稳定 NodeTypeId 优先，旧 CLR 全名走兼容路径；均失败时按开关占位或抛异常。
        var type = _catalog?.TryResolveType(descriptor.NodeTypeId, descriptor.TypeName)
                   ?? (_catalog == null ? NodeTypeRegistry.TryResolve(descriptor.NodeTypeId) : null)
                   ?? ResolveType(descriptor.TypeName);
        if (type == null)
        {
            if (!AllowMissingNodes)
                throw new TypeLoadException($"无法找到节点类型: {descriptor.TypeName}");
            var missing = Nodes.MissingNode.Capture(descriptor);
            RestoreNodeState(missing, descriptor);
            return missing;
        }
        if (!typeof(NodeModel).IsAssignableFrom(type))
            throw new InvalidOperationException($"类型 {type.Name} 不是有效的节点类型");

        var node = Create(type);

        RestoreNodeState(node, descriptor);

        // 附加属性
        ApplyProperties(node, descriptor.Properties);

        return node;
    }

    /// <summary>
    /// 还原基础字段与自定义端口，保留持久化标识和连线引用。
    /// </summary>
    /// <param name="node">待填充的节点。</param>
    /// <param name="descriptor">原始节点文档。</param>
    private static void RestoreNodeState(NodeModel node, NodeDocumentModel descriptor)
    {
        node.Id = descriptor.Id;
        node.Name = descriptor.Name;
        node.X = descriptor.X;
        node.Y = descriptor.Y;
        node.IsInEnabled = descriptor.IsInEnabled;
        node.IsOutEnabled = descriptor.IsOutEnabled;
        node.Permission = descriptor.Permission;
        node.IsExcludedFromExecution = descriptor.IsExcludedFromExecution;
        node.ParentExecutionContainerId = descriptor.ParentExecutionContainerId;
        node.ExecutionLaneId = descriptor.ExecutionLaneId;
        node.ErrorPolicy = descriptor.ErrorPolicy;
        node.RetryCount = descriptor.RetryCount;
        node.RetryDelayMs = descriptor.RetryDelayMs;
        node.TimeoutMs = descriptor.TimeoutMs;
        RestorePorts(node, input: true, descriptor.CustomInputPorts);
        RestorePorts(node, input: false, descriptor.CustomOutputPorts);
    }

    /// <summary>按名称复用或创建自定义端口，并恢复持久化标识与类型。</summary>
    private static void RestorePorts(
        NodeModel node,
        bool input,
        IReadOnlyList<CustomPortDocument> portDocs)
    {
        foreach (var portDoc in portDocs)
        {
            PortModel? port;
            if (input)
            {
                port = node.CustomInputPorts.FirstOrDefault(p =>
                    p.Name == portDoc.Name ||
                    (!string.IsNullOrEmpty(portDoc.BoundProperty) && p.PropertyName == portDoc.BoundProperty) ||
                    p.PropertyName == portDoc.Name);
                port ??= node.AddCustomInputPort(portDoc.Name, portDoc.BoundProperty);
                if (!string.IsNullOrEmpty(portDoc.BoundProperty))
                    port.PropertyName = portDoc.BoundProperty;
            }
            else
            {
                port = node.CustomOutputPorts.FirstOrDefault(p => p.Name == portDoc.Name);
                port ??= node.AddCustomOutputPort(portDoc.Name, portDoc.BoundProperty);
                if (!string.IsNullOrEmpty(portDoc.BoundProperty))
                    port.PropertyName = portDoc.BoundProperty;
            }
            if (portDoc.DataType.HasValue)
                port.DataType = portDoc.DataType.Value;
            port.Id = portDoc.Id;
            port.OwnerNodeId = node.Id;
        }
    }

    /// <summary>
    /// 实例化：与容器语义一致——按"可满足的构造函数中参数最多者"选择，
    /// 无参构造仅是参数量为零的候选（当更丰富的构造无法满足时兜底）。
    /// </summary>
    private NodeModel CreateInstance(Type type)
    {
        var candidates = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length);

        string? missingReport = null;
        foreach (var ctor in candidates)
        {
            var parameters = ctor.GetParameters();
            var args = new object?[parameters.Length];
            var missing = new List<string>();

            for (var i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                if (_serviceResolvers.TryGetValue(paramType, out var resolver))
                {
                    args[i] = resolver();
                }
                else
                {
                    object? resolved = null;
                    try { resolved = _fallbackResolver?.Invoke(paramType); }
                    catch { resolved = null; }

                    if (resolved == null)
                    {
                        missing.Add(paramType.Name);
                        continue;
                    }
                    args[i] = resolved;
                }
            }

            if (missing.Count == 0)
                return (NodeModel)ctor.Invoke(args);

            missingReport ??= string.Join(", ", missing);
        }

        throw new InvalidOperationException(
            $"无法实例化节点 {type.Name}：没有任何构造函数的依赖可全部满足（缺: {missingReport ?? "未知"}）。" +
            $"请通过当前 ReflectionNodeFactory 实例的 RegisterService 注册，或提供 fallbackResolver。");
    }

    /// <summary>类型解析：全名 → 短名扫描已加载程序集；兼容 FlowEditor.Nodes.* → FlowNodes.* 迁移。</summary>
    private static Type? ResolveType(string typeName)
    {
        var type = TryResolve(typeName);
        if (type != null) return type;

        // 旧版兼容 1：Common 组并入 FlowControl 前的 .flow 使用 FlowEditor.Nodes.Common.* 类型名
        if (typeName.Contains("FlowEditor.Nodes.Common."))
        {
            type = TryResolve(typeName.Replace("FlowEditor.Nodes.Common.", "FlowEditor.Nodes.FlowControl."));
            if (type != null) return type;
        }

        // 旧版兼容 2：节点迁移到 FlowNodes 程序集前的 .flow 使用 FlowEditor.Nodes.* 类型名
        if (typeName.Contains("FlowEditor.Nodes."))
            type = TryResolve(typeName.Replace("FlowEditor.Nodes.", "FlowNodes."));
        return type;
    }

    /// <summary>先按程序集限定名称解析，再扫描已加载程序集中的短类型名。</summary>
    private static Type? TryResolve(string typeName)
    {
        var type = global::FlowEditor.Core.FrameworkTypeResolver.ResolvePersistedType(typeName);
        if (type != null) return type;
        var shortName = typeName.Split(',')[0].Trim();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                type = asm.GetType(shortName);
                if (type != null) return type;
            }
            catch { }
        }
        return null;
    }

    /// <summary>回填派生类附加属性（与持久化服务的序列化选项保持一致）。</summary>
    private static void ApplyProperties(NodeModel node, Dictionary<string, object?> properties)
    {
        var type = node.GetType();
        foreach (var (key, value) in properties)
        {
            var prop = type.GetProperty(key, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || !prop.CanWrite) continue;
            try
            {
                if (value is JsonElement je)
                {
                    var converted = JsonSerializer.Deserialize(je.GetRawText(), prop.PropertyType, PropertyJsonOptions);
                    prop.SetValue(node, converted);
                }
                else
                {
                    prop.SetValue(node, value);
                }
            }
            catch
            {
                // 单个属性还原失败不阻断整体实例化
            }
        }
    }
}
