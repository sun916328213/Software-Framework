// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：NodeModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Core.Nodes、System.Reflection、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Core.Nodes;
using System.Reflection;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace FlowEditor.Controls.Nodes;

/// <summary>
/// NodeModel 的 P1 端口元数据桥接：新契约（NodeInput/NodeOutput）优先，
/// 旧 NodeProperty(IsInput/IsOutput) 回退，保证既有节点零改动可运行。
/// <para/>
/// 职责划分：NodeProperty 只管 PropertyGrid 参数编辑；NodeInput/NodeOutput 只管 Flow 契约；
/// 两者可同时标注在同一属性上（过渡期），最终以新契约为准。
/// </summary>
public abstract partial class NodeModel
{
    /// <summary>
    /// 获取本节点声明的 Flow 端口：优先读取新契约，旧 NodeProperty 声明作为回退。
    /// 同一属性同时标注新旧契约时以新契约为准（FromLegacy=false）。
    /// </summary>
    public IReadOnlyList<NodePortDeclaration> GetDeclaredFlowPorts()
    {
        var fresh = NodePortMetadata.GetDeclaredPorts(GetType());
        var freshProps = fresh.Select(d => d.PropertyName).ToHashSet(StringComparer.Ordinal);
        var result = new List<NodePortDeclaration>(fresh);
        foreach (var legacy in GetLegacyPortDeclarations())
        {
            if (freshProps.Contains(legacy.PropertyName)) continue;
            result.Add(legacy);
        }
        return result;
    }

    /// <summary>回退读取旧 NodeProperty(IsInput/IsOutput) 声明的端口；已被新契约覆盖的属性除外。</summary>
    private IEnumerable<NodePortDeclaration> GetLegacyPortDeclarations()
    {
        for (var t = GetType(); t != null && t != typeof(object) && t != typeof(NodeModel); t = t.BaseType)
        {
            foreach (var prop in t.GetProperties(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var attr = prop.GetCustomAttribute<NodePropertyAttribute>(inherit: false);
                if (attr == null) continue;
                if (attr.IsInput)
                    yield return new NodePortDeclaration(prop.Name, prop.Name, IsInput: true, Required: false, FromLegacy: true);
                else if (attr.IsOutput)
                    yield return new NodePortDeclaration(prop.Name, prop.Name, IsInput: false, Required: false, FromLegacy: true);
            }
        }
    }
}
