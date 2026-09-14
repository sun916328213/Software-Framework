using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlowEditor.Models;
using FlowEditor.Nodes;

namespace FlowEditor.Core;

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
/// 1. 存在无参构造 → 直接使用（零依赖节点）；
/// 2. 否则按"参数最多且全部可解析"贪心选择构造函数，
///    参数依次来自 RegisterService 注册表 → 实例回退解析器；
///    全部不可满足时抛出含缺失服务清单的明确异常。
/// 每个实例持有自己的注册表和可选回退解析器，不共享宿主可变状态；
/// 无头宿主直接创建独立实例，桌面宿主通过 adapter 提供解析器。
/// </summary>
public sealed class ReflectionNodeFactory : INodeFactory
{
    private readonly ConcurrentDictionary<Type, Func<object>> _serviceResolvers = new();
    private readonly Func<Type, object?>? _fallbackResolver;

    private static readonly JsonSerializerOptions PropertyJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>创建具有独立依赖注册表的反射节点 factory。</summary>
    /// <param name="fallbackResolver">实例注册未命中时使用的宿主依赖解析函数。</param>
    public ReflectionNodeFactory(Func<Type, object?>? fallbackResolver = null)
    {
        _fallbackResolver = fallbackResolver;
    }

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
    public NodeModel Create(Type nodeType)
    {
        ArgumentNullException.ThrowIfNull(nodeType);
        if (nodeType.IsAbstract || !typeof(NodeModel).IsAssignableFrom(nodeType))
            throw new ArgumentException($"类型 {nodeType.Name} 不是可实例化的节点类型。", nameof(nodeType));

        return CreateInstance(nodeType);
    }

    /// <inheritdoc />
    public NodeModel Create(NodeDocumentModel descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var type = ResolveType(descriptor.TypeName)
                   ?? throw new TypeLoadException($"无法找到节点类型: {descriptor.TypeName}");
        if (!typeof(NodeModel).IsAssignableFrom(type))
            throw new InvalidOperationException($"类型 {type.Name} 不是有效的节点类型");

        var node = Create(type);

        // 基础字段
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

        // 自定义端口：按名去重还原（节点构造函数可能已创建默认自定义端口，
        // 直接追加会产生同名重复端口导致运行期读写错位），保留描述符中的原始 ID
        RestorePorts(node, input: true, descriptor.CustomInputPorts);
        RestorePorts(node, input: false, descriptor.CustomOutputPorts);

        // 附加属性
        ApplyProperties(node, descriptor.Properties);

        return node;
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
        var type = Type.GetType(typeName);
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
