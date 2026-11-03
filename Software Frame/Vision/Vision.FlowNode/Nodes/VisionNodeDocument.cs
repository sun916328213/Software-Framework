using System.Text.Json;

namespace Vision.FlowNode;

/// <summary>仅保存明确配置；未知版本、缺字段和额外字段均拒绝，不把旧会话文档猜成新配方。</summary>
internal static class VisionNodeDocument
{
    internal static void Check(Dictionary<string, object?> state, params string[] parameters)
    {
        ArgumentNullException.ThrowIfNull(state);
        var expected = parameters.Append("Version").ToHashSet(StringComparer.Ordinal);
        if (state.Count != expected.Count || state.Keys.Any(key => !expected.Contains(key))
            || Read<int>(state, "Version") != 1)
        {
            throw new InvalidOperationException("视觉节点文档版本或字段集合不匹配。");
        }
    }

    internal static T Read<T>(Dictionary<string, object?> state, string key)
    {
        if (!state.TryGetValue(key, out var value) || value == null)
        {
            throw new InvalidOperationException($"缺少节点配置：{key}。");
        }

        return value is T typed ? typed
            : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))
                ?? throw new InvalidOperationException($"节点配置为空：{key}。");
    }
}
