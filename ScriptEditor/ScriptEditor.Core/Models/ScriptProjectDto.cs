using System.IO;
using System.Text.Json;

namespace ScriptEditor.Models;

/// <summary>引用路径的可移植表示（绝对路径 ↔ 相对 project 目录）。</summary>
public static class ScriptReferencePath
{
    /// <summary>保存时：位于工程目录下的绝对路径转为相对路径，其余原样保留。</summary>
    public static string ToPortable(string? baseDirectory, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(baseDirectory))
            return path ?? string.Empty;
        try
        {
            var full = Path.GetFullPath(path);
            var relative = Path.GetRelativePath(baseDirectory, full);
            if (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative))
                return relative;
        }
        catch { /* 路径非法时原样保留 */ }
        return path;
    }

    /// <summary>加载时：相对路径相对工程目录解析，绝对路径规范化。</summary>
    public static string ToAbsolute(string? baseDirectory, string portable)
    {
        if (string.IsNullOrWhiteSpace(portable))
            return portable ?? string.Empty;
        try
        {
            if (!Path.IsPathRooted(portable) && !string.IsNullOrWhiteSpace(baseDirectory))
                return Path.GetFullPath(Path.Combine(baseDirectory, portable));
            return Path.GetFullPath(portable);
        }
        catch { return portable; }
    }
}

/// <summary>引用描述 DTO（可序列化，不触碰文件系统）。</summary>
public sealed class ScriptReferenceDto
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ScriptReferenceKind Kind { get; set; } = ScriptReferenceKind.AssemblyFile;

    public ScriptReference ToModel(string? baseDirectory) => new(
        ScriptReferencePath.ToAbsolute(baseDirectory, Path),
        string.IsNullOrWhiteSpace(Name) ? Path : Name,
        Kind);

    public static ScriptReferenceDto FromModel(ScriptReference reference, string? baseDirectory) => new()
    {
        Path = ScriptReferencePath.ToPortable(baseDirectory, reference.Path),
        Name = reference.Name,
        Kind = reference.Kind
    };
}

/// <summary>单脚本 DTO（可序列化）。</summary>
public sealed class ScriptItemDto
{
    public Guid ScriptId { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "新脚本";
    public string Code { get; set; } = string.Empty;
    public List<ScriptReferenceDto> References { get; set; } = new();
    public List<int> Breakpoints { get; set; } = new();
}

/// <summary>
/// 版本化任务工程 DTO（.taskproj V2）。
/// V1 无 <see cref="FormatVersion"/> 字段（旧 <c>TaskProjectModel</c> 形状）；
/// V2 起引用为结构化描述且路径优先相对工程目录。
/// </summary>
public sealed class ScriptProjectDto
{
    public int FormatVersion { get; set; } = ScriptProjectMigration.CurrentFormatVersion;
    public string Name { get; set; } = "新建任务";
    public List<ScriptReferenceDto> References { get; set; } = new();
    public List<ScriptItemDto> Scripts { get; set; } = new();
}

/// <summary>V1 → V2 数据迁移入口（纯函数，无 IO）。</summary>
public static class ScriptProjectMigration
{
    public const int CurrentFormatVersion = 2;

    private static readonly JsonSerializerOptions TolerantReader = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>解析任务文件 JSON：V2 直接读取，V1 按旧形状迁移；不可识别返回 null。</summary>
    public static ScriptProjectDto? Migrate(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var hasVersion = root.TryGetProperty("FormatVersion", out var versionElement)
                && versionElement.ValueKind == JsonValueKind.Number
                && versionElement.TryGetInt32(out var version)
                && version >= 2;

            if (hasVersion)
                return JsonSerializer.Deserialize<ScriptProjectDto>(json, TolerantReader);

            return MigrateV1(root);
        }
        catch { return null; }
    }

    /// <summary>V1：Scripts 为 ScriptItem 形状（References 为字符串路径列表）。</summary>
    private static ScriptProjectDto? MigrateV1(JsonElement root)
    {
        if (!root.TryGetProperty("Scripts", out var scriptsElement)
            || scriptsElement.ValueKind != JsonValueKind.Array)
            return null;

        var dto = new ScriptProjectDto
        {
            FormatVersion = CurrentFormatVersion,
            Name = root.TryGetProperty("Name", out var nameElement)
                && nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString() ?? "新建任务"
                : "新建任务"
        };

        if (root.TryGetProperty("References", out var refsElement)
            && refsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var path in ReadStringList(refsElement))
                dto.References.Add(new ScriptReferenceDto
                {
                    Path = path,
                    Name = FileNameOf(path),
                    Kind = ScriptReferenceKind.AssemblyFile
                });
        }

        foreach (var script in scriptsElement.EnumerateArray())
        {
            if (script.ValueKind != JsonValueKind.Object) continue;
            var item = new ScriptItemDto
            {
                ScriptId = ReadGuid(script, "ScriptId"),
                Title = ReadString(script, "Title", "新脚本"),
                Code = ReadString(script, "Code", string.Empty),
                Breakpoints = ReadIntList(script, "Breakpoints")
            };
            if (script.TryGetProperty("References", out var itemRefs)
                && itemRefs.ValueKind == JsonValueKind.Array)
            {
                foreach (var path in ReadStringList(itemRefs))
                    item.References.Add(new ScriptReferenceDto
                    {
                        Path = path,
                        Name = FileNameOf(path),
                        Kind = ScriptReferenceKind.AssemblyFile
                    });
            }
            dto.Scripts.Add(item);
        }

        return dto;
    }

    /// <summary>DTO → 运行时数据模型（相对引用相对工程目录解析）。</summary>
    public static ScriptProject ToModel(ScriptProjectDto dto, string? baseDirectory) => new()
    {
        Name = dto.Name,
        References = dto.References.Select(r => r.ToModel(baseDirectory)).ToList(),
        Scripts = dto.Scripts.Select(s => new ScriptProjectItem
        {
            ScriptId = s.ScriptId == Guid.Empty ? Guid.NewGuid() : s.ScriptId,
            Title = s.Title,
            Code = s.Code,
            References = s.References.Select(r => r.ToModel(baseDirectory)).ToList(),
            Breakpoints = s.Breakpoints.ToList()
        }).ToList()
    };

    /// <summary>运行时数据模型 → DTO（工程目录下引用转为相对路径）。</summary>
    public static ScriptProjectDto FromModel(ScriptProject project, string? baseDirectory) => new()
    {
        FormatVersion = CurrentFormatVersion,
        Name = project.Name,
        References = project.References.Select(r => ScriptReferenceDto.FromModel(r, baseDirectory)).ToList(),
        Scripts = project.Scripts.Select(s => new ScriptItemDto
        {
            ScriptId = s.ScriptId == Guid.Empty ? Guid.NewGuid() : s.ScriptId,
            Title = s.Title,
            Code = s.Code,
            References = s.References.Select(r => ScriptReferenceDto.FromModel(r, baseDirectory)).ToList(),
            Breakpoints = s.Breakpoints.ToList()
        }).ToList()
    };

    private static List<string> ReadStringList(JsonElement array)
    {
        var list = new List<string>();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    list.Add(value);
            }
            else if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
            {
                list.Add(number.ToString());
            }
        }
        return list;
    }

    private static List<int> ReadIntList(JsonElement parent, string property)
    {
        var list = new List<int>();
        if (!parent.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
                list.Add(number);
            else if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out var parsed))
                list.Add(parsed);
        }
        return list;
    }

    private static string ReadString(JsonElement parent, string property, string fallback)
        => parent.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? fallback
            : fallback;

    private static Guid ReadGuid(JsonElement parent, string property)
        => parent.TryGetProperty(property, out var element)
            && element.ValueKind == JsonValueKind.String
            && Guid.TryParse(element.GetString(), out var guid)
            && guid != Guid.Empty
            ? guid
            : Guid.NewGuid();

    private static string FileNameOf(string path)
    {
        try
        {
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(name)) return name;
        }
        catch { }
        return path;
    }
}
