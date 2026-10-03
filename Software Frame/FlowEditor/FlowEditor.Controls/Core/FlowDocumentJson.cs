// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowDocumentJson。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Core、FlowEditor.Runtime、System.Text.Json、System.Text.Json.Serialization。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Core;
using FlowEditor.Runtime;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowEditor.Controls.Core;

/// <summary>集中维护 `.flow`、`.flows` 与内存快照共用的 JSON 格式约束。</summary>
internal static class FlowDocumentJson
{
    /// <summary>保持现有 camelCase、枚举字符串和空值忽略规则的共享选项。</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
}
