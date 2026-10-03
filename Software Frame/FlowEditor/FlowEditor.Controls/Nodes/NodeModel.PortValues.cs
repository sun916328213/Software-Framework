// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：NodeModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Core.Nodes、System.Collections.Concurrent、System.Reflection、FlowEditor.Controls.Core、FlowEditor.Core。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Core.Nodes;
using System.Collections.Concurrent;
using System.Reflection;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;

namespace FlowEditor.Controls.Nodes;

/// <summary>
/// 为 <see cref="NodeModel"/> 集中提供端口查找、运行时属性同步和反射缓存。
/// </summary>
public abstract partial class NodeModel
{
    /// <summary>根据 ID 查找端口（输入或输出）。</summary>
    public PortModel? FindPortById(string portId)
        => InputPorts.FirstOrDefault(p => p.Id == portId)
           ?? OutputPorts.FirstOrDefault(p => p.Id == portId);

    /// <summary>根据名称查找输入端口。</summary>
    public PortModel? FindInputPort(string name)
        => InputPorts.FirstOrDefault(p => p.Name == name);

    /// <summary>根据名称查找输出端口。</summary>
    public PortModel? FindOutputPort(string name)
        => OutputPorts.FirstOrDefault(p => p.Name == name);

    /// <summary>
    /// 执行前把输入端口值同步到节点属性。
    /// </summary>
    public void SyncInputValues()
    {
        foreach (var port in InputPorts)
        {
            if (port.Value == null) continue;
            var propName = port.PropertyName ?? port.Name;
            SetPropertyValue(propName, port.Value);
        }
    }

    /// <summary>
    /// 执行后把节点属性值同步到输出端口。
    /// </summary>
    public void SyncOutputValues()
    {
        var type = GetType();
        foreach (var port in OutputPorts)
        {
            var propName = port.PropertyName ?? port.Name;
            var prop = FindPropertyByName(type, propName);
            if (prop == null) continue;
            port.Value = prop.GetValue(this);
        }
    }

    // 端口值同步在每个节点的每次执行都会触发属性查找，缓存反射结果以避免重复扫描继承链。
    private static readonly ConcurrentDictionary<(Type Type, string Name), PropertyInfo?> PropertyLookupCache = new();

    /// <summary>各节点类型可绑定属性名缓存（类型不变则结果恒定）。</summary>
    private static readonly ConcurrentDictionary<Type, string[]> BindablePropertiesCache = new();

    /// <summary>从类型继承链缓存查找公开实例属性。</summary>
    private static PropertyInfo? FindPropertyByName(Type type, string name)
        => PropertyLookupCache.GetOrAdd((type, name), static key =>
        {
            for (var current = key.Type; current != null && current != typeof(object); current = current.BaseType)
            {
                var property = current.GetProperty(
                    key.Name,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (property != null) return property;
            }

            return null;
        });

    /// <summary>向节点属性写入端口值，并把反射失败转换为类型诊断异常。</summary>
    private void SetPropertyValue(string name, object? value)
    {
        var property = FindPropertyByName(GetType(), name);
        if (property == null || !property.CanWrite) return;

        try
        {
            property.SetValue(this, value);
        }
        catch (Exception ex)
        {
            throw new FlowChartTypeMismatchException(
                $"无法将值 '{value}'（类型: {value?.GetType().Name ?? "null"}）" +
                $"写入节点 [{Name}] 的属性 {name}（类型: {property.PropertyType.Name}）: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取所有可绑定属性名列表（用于输出端口属性绑定下拉框）。
    /// 结果按节点类型缓存，返回副本防止调用方修改缓存。
    /// </summary>
    public List<string> GetBindableProperties()
    {
        var cached = BindablePropertiesCache.GetOrAdd(GetType(), static type =>
        {
            var properties = new List<string> { "Name" };
            var seen = new HashSet<string>(StringComparer.Ordinal) { "Name" };

            // 节点工具常通过多层基类复用领域属性；扫描到 NodeModel 为止，避免暴露编辑器基础设施。
            for (var current = type; current != null && current != typeof(NodeModel); current = current.BaseType)
            {
                foreach (var property in current.GetProperties(
                             BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (!property.CanRead || !seen.Add(property.Name)) continue;
                    properties.Add(property.Name);
                }
            }

            return properties.ToArray();
        });

        return new List<string>(cached);
    }
}
