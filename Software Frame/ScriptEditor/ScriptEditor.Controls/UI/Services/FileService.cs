// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：FileService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Controls、ScriptEditor.Runtime.Services、System.IO、Microsoft.Win32.OpenFileDialog、ScriptEditor.Runtime.Persistence。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Controls;
using ScriptEditor.Runtime.Services;
using System.IO;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using ScriptEditor.Runtime.Persistence;

namespace ScriptEditor.Controls.Services;

/// <summary>
/// 文件操作服务：文件对话框（WPF，保留在 Controls）+ 磁盘读写（委托 Runtime/Persistence）。
/// </summary>
/// <remarks>
/// <para>P4 边界：对话框属于 Controls；真正的读写由 <see cref="ScriptFileStore"/>
///（Runtime/Persistence，无头可测）执行。</para>
/// <para>当前由外部消费者（如 WpfApp1 的 ScriptManager）使用，ScriptEditor 控件本身不依赖此服务。</para>
/// </remarks>
/// <author>ScriptEditor Team</author>
/// <version>1.1</version>
public class FileService
{
    private readonly ScriptFileStore _store = new();
    /// <summary>打开文件选择对话框。</summary>
    /// <param name="filter">文件类型过滤器，如 "C# 文件 (*.cs)|*.cs"。</param>
    /// <param name="initialDirectory">初始目录路径，可为 null。</param>
    /// <returns>选中文件的完整路径，取消则返回 null。</returns>
    public string? ShowOpenDialog(string filter, string? initialDirectory = null)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            RestoreDirectory = true
        };

        if (!string.IsNullOrEmpty(initialDirectory))
            dialog.InitialDirectory = initialDirectory;

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>打开文件保存对话框。</summary>
    /// <param name="filter">文件类型过滤器。</param>
    /// <param name="defaultFileName">默认文件名。</param>
    /// <param name="initialDirectory">初始目录路径，可为 null。</param>
    /// <returns>保存路径，取消则返回 null。</returns>
    public string? ShowSaveDialog(string filter, string? defaultFileName, string? initialDirectory = null)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = filter,
            RestoreDirectory = true,
            FileName = defaultFileName
        };

        if (!string.IsNullOrEmpty(initialDirectory))
            dialog.InitialDirectory = initialDirectory;

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>同步读取文件全部文本（委托 Runtime/Persistence）。</summary>
    public string ReadAllText(string path) => _store.ReadAllText(path);

    /// <summary>同步写入文件全部文本（委托 Runtime/Persistence）。</summary>
    public void WriteAllText(string path, string content) => _store.WriteAllText(path, content);

    /// <summary>异步读取文件全部文本（委托 Runtime/Persistence）。</summary>
    public async Task<string> ReadAllTextAsync(string path)
        => await _store.ReadAllTextAsync(path).ConfigureAwait(false);

    /// <summary>异步写入文件全部文本（委托 Runtime/Persistence）。</summary>
    public async Task WriteAllTextAsync(string path, string content)
        => await _store.WriteAllTextAsync(path, content).ConfigureAwait(false);
}