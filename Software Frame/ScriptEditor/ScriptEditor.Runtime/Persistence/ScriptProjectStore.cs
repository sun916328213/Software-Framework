// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：ScriptProjectStore。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Text.Json、ScriptEditor.Core.Contracts、ScriptEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using System.Text.Json;
using ScriptEditor.Core.Contracts;
using ScriptEditor.Core.Models;

namespace ScriptEditor.Runtime.Persistence;

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

    /// <summary>
    /// 初始化 ScriptProjectStore 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public ScriptProjectStore(ScriptFileStore? files = null)
    {
        _files = files ?? new ScriptFileStore();
    }

    /// <summary>
    /// 执行加载操作；对应入口为 LoadAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、ReadAllTextAsync、Migrate、ToModel、GetDirectoryName；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task<ScriptProject> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var json = await _files.ReadAllTextAsync(path).ConfigureAwait(false);
        var dto = ScriptProjectMigration.Migrate(json)
            ?? throw new InvalidDataException($"任务文件格式不正确或为空：{path}");
        return ScriptProjectMigration.ToModel(dto, Path.GetDirectoryName(path));
    }

    /// <summary>
    /// 执行保存操作；对应入口为 SaveAsync。
    /// 正文中的关键协作调用包括 GetDirectoryName、FromModel、Serialize、Exists、Copy；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
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
