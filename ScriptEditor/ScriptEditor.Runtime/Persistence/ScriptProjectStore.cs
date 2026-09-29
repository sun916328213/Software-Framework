using System.Text.Json;
using ScriptEditor.Contracts;
using ScriptEditor.Models;

namespace ScriptEditor.Persistence;

/// <summary>
/// 版本化任务工程存储（.taskproj）：V2 写入 / V1→V2 迁移读取。
/// </summary>
/// <remarks>
/// P5 产物，实现 <see cref="IScriptProjectStore"/>：
/// <list type="bullet">
/// <item>保存：模型 → DTO（工程目录下引用转相对路径）→ JSON 落盘；</item>
/// <item>加载：JSON → V2 直读或 V1 迁移 → 模型（相对引用相对工程目录解析）；</item>
/// <item>无头可用：只经 <see cref="ScriptFileStore"/> 做磁盘 IO，不弹对话框、不碰 WPF。</item>
/// </list>
/// </remarks>
public sealed class ScriptProjectStore : IScriptProjectStore
{
    private readonly ScriptFileStore _files;

    private static readonly JsonSerializerOptions WriterOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public ScriptProjectStore(ScriptFileStore? files = null)
    {
        _files = files ?? new ScriptFileStore();
    }

    public async Task<ScriptProject> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var json = await _files.ReadAllTextAsync(path).ConfigureAwait(false);
        var dto = ScriptProjectMigration.Migrate(json)
            ?? throw new InvalidDataException($"任务文件格式不正确或为空：{path}");
        return ScriptProjectMigration.ToModel(dto, Path.GetDirectoryName(path));
    }

    public async Task SaveAsync(string path, ScriptProject project, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        var dto = ScriptProjectMigration.FromModel(project, directory);
        var json = JsonSerializer.Serialize(dto, WriterOptions);
        // 覆盖前将旧文件轮换为 .bak（仅保留一代；备份失败不阻断写入，与旧 FileEx.Write 行为一致）
        try
        {
            if (File.Exists(path))
                File.Copy(path, path + ".bak", true);
        }
        catch { /* 备份失败不阻断写入 */ }
        await _files.WriteAllTextAsync(path, json).ConfigureAwait(false);
    }
}
