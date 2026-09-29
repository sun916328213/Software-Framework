namespace FlowEditor.Core;

/// <summary>
/// Flow 输入契约（P1）。只描述"运行时由连线或绑定提供的值"，不描述面板展示。
/// 与 <c>NodeProperty</c>（PropertyGrid 参数编辑）解耦后，即使更换 PropertyGrid
/// 或运行在无 WPF 环境中，Flow 端口模型仍然独立。
/// <para/>
/// 迁移规则：新节点用此特性声明输入；旧节点 <c>NodeProperty(IsInput=true)</c> 仍兼容，
/// 由宿主桥接层回退识别（见 NodeModel.GetDeclaredFlowPorts）。
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class NodeInputAttribute : Attribute
{
    /// <summary>端口名；为空时使用 CLR 属性名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>端口描述。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>是否为必要输入：未连线时验证阶段报错。</summary>
    public bool Required { get; set; } = false;
}

/// <summary>
/// Flow 输出契约（P1）。只描述"运行时生成的值"，不保存结果值，不描述面板展示。
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class NodeOutputAttribute : Attribute
{
    /// <summary>端口名；为空时使用 CLR 属性名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>端口描述。</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// 运行态标记（P1）。标注连接状态、耗时、缓存、Handle 等内部状态：
/// 不参与流程序列化（须配合 <c>JsonIgnore</c>），不作为端口契约，不在面板编辑。
/// 与"配置（保存）/输入（连线提供）/输出（运行生成）"正交，用于明确序列化边界、减少状态污染。
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class NodeRuntimeStateAttribute : Attribute
{
    /// <summary>状态说明。</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>单个 Flow 端口的声明（来源可能是新契约或旧 NodeProperty 回退）。</summary>
/// <param name="PropertyName">绑定的 CLR 属性名。</param>
/// <param name="PortName">端口名。</param>
/// <param name="IsInput">是否为输入。</param>
/// <param name="Required">是否为必要输入。</param>
/// <param name="FromLegacy">是否来自旧 NodeProperty 回退（迁移提示用）。</param>
public sealed record NodePortDeclaration(
    string PropertyName,
    string PortName,
    bool IsInput,
    bool Required,
    bool FromLegacy);

/// <summary>
/// Flow 端口元数据读取器（P1，Runtime 侧）。只识别本程序集的新契约
/// （<see cref="NodeInputAttribute"/>/<see cref="NodeOutputAttribute"/>），
/// 不引用 PropertyGrid，保持 Runtime 与 UI 元数据解耦；
/// 旧 <c>NodeProperty(IsInput/IsOutput)</c> 由宿主桥接层回退识别。
/// </summary>
public static class NodePortMetadata
{
    /// <summary>读取类型上用新契约声明的 Flow 端口。</summary>
    public static IReadOnlyList<NodePortDeclaration> GetDeclaredPorts(Type nodeType)
    {
        ArgumentNullException.ThrowIfNull(nodeType);
        var result = new List<NodePortDeclaration>();
        for (var t = nodeType; t != null && t != typeof(object); t = t.BaseType)
        {
            foreach (var prop in t.GetProperties(
                         System.Reflection.BindingFlags.Public |
                         System.Reflection.BindingFlags.Instance |
                         System.Reflection.BindingFlags.DeclaredOnly))
            {
                var input = prop.GetCustomAttributes(typeof(NodeInputAttribute), inherit: false)
                    .Cast<NodeInputAttribute>().FirstOrDefault();
                if (input != null)
                {
                    result.Add(new NodePortDeclaration(
                        prop.Name,
                        string.IsNullOrWhiteSpace(input.Name) ? prop.Name : input.Name,
                        IsInput: true, Required: input.Required, FromLegacy: false));
                    continue;
                }
                var output = prop.GetCustomAttributes(typeof(NodeOutputAttribute), inherit: false)
                    .Cast<NodeOutputAttribute>().FirstOrDefault();
                if (output != null)
                {
                    result.Add(new NodePortDeclaration(
                        prop.Name,
                        string.IsNullOrWhiteSpace(output.Name) ? prop.Name : output.Name,
                        IsInput: false, Required: false, FromLegacy: false));
                }
            }
        }
        return result;
    }

    /// <summary>读取类型上标记为运行态的属性（含字段），用于序列化边界审计。</summary>
    public static IReadOnlyList<string> GetRuntimeStateMembers(Type nodeType)
    {
        ArgumentNullException.ThrowIfNull(nodeType);
        var result = new List<string>();
        for (var t = nodeType; t != null && t != typeof(object); t = t.BaseType)
        {
            foreach (var prop in t.GetProperties(
                         System.Reflection.BindingFlags.Public |
                         System.Reflection.BindingFlags.NonPublic |
                         System.Reflection.BindingFlags.Instance |
                         System.Reflection.BindingFlags.DeclaredOnly))
            {
                if (prop.GetCustomAttributes(typeof(NodeRuntimeStateAttribute), inherit: false).Any())
                    result.Add(prop.Name);
            }
            foreach (var field in t.GetFields(
                         System.Reflection.BindingFlags.NonPublic |
                         System.Reflection.BindingFlags.Instance |
                         System.Reflection.BindingFlags.DeclaredOnly))
            {
                if (field.GetCustomAttributes(typeof(NodeRuntimeStateAttribute), inherit: false).Any())
                    result.Add(field.Name);
            }
        }
        return result;
    }
}
