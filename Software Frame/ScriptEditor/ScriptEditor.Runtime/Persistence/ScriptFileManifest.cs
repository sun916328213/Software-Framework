// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：ScriptFileManifestEntry、ScriptFileManifest。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Text.Json。导入不代表所有类型都会在运行时加载。
using System.Text.Json;

namespace ScriptEditor.Runtime.Persistence;

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

    /// <summary>
    /// 执行加载操作；对应入口为 Load。
    /// 正文中的关键协作调用包括 Combine、Exists、Deserialize、ReadAllText；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
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

    /// <summary>
    /// 执行保存操作；对应入口为 Save。
    /// 正文中的关键协作调用包括 Combine、WriteAllText、Serialize；具体条件和先后次序见方法体。
    /// </summary>
    public void Save(string directory)
    {
        var path = Path.Combine(directory, FileName);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
    }
}
