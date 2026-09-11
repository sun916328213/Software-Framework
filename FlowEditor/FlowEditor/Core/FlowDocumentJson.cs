using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowEditor.Core;

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
