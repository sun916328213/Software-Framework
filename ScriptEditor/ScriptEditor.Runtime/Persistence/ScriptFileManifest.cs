using System.Text.Json;

namespace ScriptEditor.Persistence;

/// <summary>镜像清单条目：文件名 ↔ 脚本永久标识 + 内容哈希。</summary>
public sealed class ScriptFileManifestEntry
{
    public string FileName { get; set; } = string.Empty;
    public Guid ScriptId { get; set; } = Guid.Empty;
    public string ContentHash { get; set; } = string.Empty;
}

/// <summary>
/// 外部 .cs 镜像清单（与 .taskproj 同目录）。
/// </summary>
/// <remarks>
/// P5 产物：替代“仅按序号/时间戳”匹配。导出时写入
/// 文件名、<c>ScriptId</c> 与内容哈希；回填时优先按 <c>ScriptId</c> 匹配
///（改名/重排后仍能对上），再按内容哈希判定是否更新；
/// 无清单（旧任务）时回退到序号顺序兼容。
/// </remarks>
public sealed class ScriptFileManifest
{
    public const string FileName = "taskfiles.manifest.json";
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public List<ScriptFileManifestEntry> Files { get; set; } = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static ScriptFileManifest? Load(string directory)
    {
        try
        {
            var path = Path.Combine(directory, FileName);
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<ScriptFileManifest>(File.ReadAllText(path), Options);
        }
        catch { return null; }
    }

    public void Save(string directory)
    {
        var path = Path.Combine(directory, FileName);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
    }
}
