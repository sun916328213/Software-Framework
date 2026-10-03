// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：NodeTypeIdAttribute、NodeTypeRegistry、PluginManifest、PluginRegistry。成员说明可结合下方具体实现阅读。
namespace FlowEditor.Core;

/// <summary>
/// 稳定节点类型标识（P2）。流程文件长期依赖此标识而非 CLR FullName，
/// 类名、命名空间、程序集未来允许调整，旧流程仍可打开。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class NodeTypeIdAttribute : Attribute
{
    /// <summary>稳定类型标识，如 "flow.loop"、"device.camera-capture"。</summary>
    public new string TypeId { get; }

    /// <summary>使用稳定标识标记节点类型。</summary>
    public NodeTypeIdAttribute(string typeId)
    {
        TypeId = string.IsNullOrWhiteSpace(typeId)
            ? throw new ArgumentException("节点类型标识不能为空。", nameof(typeId))
            : typeId.Trim();
    }
}

/// <summary>
/// 节点类型注册表（P2）。维护稳定 NodeTypeId 与 CLR 类型的双向映射，
/// 以及旧 CLR 全名的兼容别名；插件加载/卸载时注册/注销。
/// </summary>
public static class NodeTypeRegistry
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, Type> IdToType = new(StringComparer.Ordinal);
    private static readonly Dictionary<Type, string> TypeToId = new();
    private static readonly Dictionary<string, string> LegacyAliasToId = new(StringComparer.Ordinal);

    /// <summary>注册单个节点类型（读取 NodeTypeIdAttribute；无标记时不注册）。</summary>
    /// <returns>注册的稳定标识；无标记返回 null。</returns>
    public static string? Register(Type nodeType)
    {
        ArgumentNullException.ThrowIfNull(nodeType);
        var attr = nodeType.GetCustomAttributes(typeof(NodeTypeIdAttribute), inherit: false)
            .Cast<NodeTypeIdAttribute>().FirstOrDefault();
        if (attr == null) return null;
        lock (Gate)
        {
            IdToType[attr.TypeId] = nodeType;
            TypeToId[nodeType] = attr.TypeId;
        }
        return attr.TypeId;
    }

    /// <summary>注册程序集中全部带 NodeTypeIdAttribute 的类型。</summary>
    /// <returns>实际注册数量。</returns>
    public static int RegisterAssembly(System.Reflection.Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var count = 0;
        foreach (var type in assembly.GetTypes())
        {
            try
            {
                if (Register(type) != null) count++;
            }
            catch { /* 跳过无法加载的类型 */ }
        }
        return count;
    }

    /// <summary>注册旧 CLR 全名到稳定标识的兼容别名（旧流程迁移用）。</summary>
    public static void RegisterLegacyAlias(string clrFullName, string typeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clrFullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        lock (Gate) LegacyAliasToId[clrFullName.Trim()] = typeId.Trim();
    }

    /// <summary>按稳定标识解析类型；未注册返回 null（调用方创建 MissingNode 占位）。</summary>
    public static Type? TryResolve(string? typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId)) return null;
        lock (Gate) return IdToType.TryGetValue(typeId.Trim(), out var type) ? type : null;
    }

    /// <summary>获取类型的稳定标识；未注册返回 null。</summary>
    public static string? TryGetId(Type nodeType)
    {
        ArgumentNullException.ThrowIfNull(nodeType);
        lock (Gate) return TypeToId.TryGetValue(nodeType, out var id) ? id : null;
    }

    /// <summary>旧 CLR 全名是否有兼容别名。</summary>
    public static bool TryResolveLegacyAlias(string? clrFullName, out string typeId)
    {
        typeId = string.Empty;
        if (string.IsNullOrWhiteSpace(clrFullName)) return false;
        lock (Gate) return LegacyAliasToId.TryGetValue(clrFullName.Trim(), out typeId!);
    }

    /// <summary>解析节点文档的类型：NodeTypeId 优先，兼容别名次之，CLR 反射最后（旧文档）。</summary>
    public static Type? ResolveDocumentType(string? nodeTypeId, string? clrTypeName)
    {
        var byId = TryResolve(nodeTypeId);
        if (byId != null) return byId;
        if (!string.IsNullOrWhiteSpace(clrTypeName))
        {
            if (TryResolveLegacyAlias(clrTypeName, out var aliasedId))
            {
                var byAlias = TryResolve(aliasedId);
                if (byAlias != null) return byAlias;
            }
            var direct = FrameworkTypeResolver.ResolvePersistedType(clrTypeName);
            if (direct != null) return direct;
            var shortName = clrTypeName.Split(',')[0].Trim();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(shortName);
                    if (t != null) return t;
                }
                catch { }
            }
        }
        return null;
    }

    /// <summary>清空全部注册（仅单元测试使用）。</summary>
    public static void Clear()
    {
        lock (Gate)
        {
            IdToType.Clear();
            TypeToId.Clear();
            LegacyAliasToId.Clear();
        }
    }
}

/// <summary>
/// 插件清单（P2）。描述一个节点包提供的能力与依赖，
/// Workbench 节点目录据此显示 PluginId、NodeTypeId、版本、是否可用、缺失依赖。
/// </summary>
/// <param name="PluginId">插件稳定标识，如 "FlowNodes.Device"。</param>
/// <param name="Version">插件版本。</param>
/// <param name="NodeTypeIds">本插件提供的节点类型标识。</param>
/// <param name="Dependencies">必需的插件依赖。</param>
/// <param name="OptionalNativeDependencies">可选的原生依赖（如 Halcon、相机厂商 SDK）。</param>
public sealed record PluginManifest(
    string PluginId,
    string Version,
    IReadOnlyList<string> NodeTypeIds,
    IReadOnlyList<string>? Dependencies = null,
    IReadOnlyList<string>? OptionalNativeDependencies = null);

/// <summary>插件注册表（P2）：记录已安装节点包，回答"某节点类型是否可用"。</summary>
public static class PluginRegistry
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, PluginManifest> Manifests = new(StringComparer.Ordinal);

    /// <summary>注册插件清单（同 PluginId 覆盖）。</summary>
    public static void Register(PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.PluginId);
        lock (Gate) Manifests[manifest.PluginId] = manifest;
    }

    /// <summary>注销插件。</summary>
    public static bool Unregister(string pluginId)
    {
        lock (Gate) return Manifests.Remove(pluginId);
    }

    /// <summary>某节点类型是否有已安装插件提供。</summary>
    public static bool IsNodeAvailable(string? nodeTypeId)
    {
        if (string.IsNullOrWhiteSpace(nodeTypeId)) return false;
        lock (Gate) return Manifests.Values.Any(m => m.NodeTypeIds.Contains(nodeTypeId.Trim(), StringComparer.Ordinal));
    }

    /// <summary>获取全部已注册清单。</summary>
    public static IReadOnlyList<PluginManifest> GetAll()
    {
        lock (Gate) return [.. Manifests.Values];
    }

    /// <summary>清空全部注册（仅单元测试使用）。</summary>
    public static void Clear()
    {
        lock (Gate) Manifests.Clear();
    }
}
