using System.IO;
using System.Text.Json;
using Vision.Core.Engine;

namespace Vision.Workbench;

/// <summary>宿主只读取配置来源并形成不可变选择；不持后端表、SDK或工程文件引擎。</summary>
public static class VisionEngineConfiguration
{
    public const string FileName = "vision-engine.json";

    public static VisionEngineSelection Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new FormatException("视觉引擎配置必须是JSON对象。");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name is not ("visionBackendId" or "aiBackendId" or "aiDeviceId"))
                throw new FormatException($"未知视觉引擎配置字段：{property.Name}。");
            if (!names.Add(property.Name)) throw new FormatException($"重复视觉引擎配置字段：{property.Name}。");
        }
        string? Read(string name)
        {
            if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
            if (value.ValueKind != JsonValueKind.String) throw new FormatException($"配置字段{name}必须为字符串或null。");
            return value.GetString();
        }
        var vision = Read("visionBackendId") ?? throw new FormatException("必须明确提供visionBackendId，不自动选择默认后端。");
        return new(vision, Read("aiBackendId"), Read("aiDeviceId"));
    }
}
