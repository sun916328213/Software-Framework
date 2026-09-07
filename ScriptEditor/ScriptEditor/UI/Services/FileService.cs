using System.IO;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace ScriptEditor.Services;

/// <summary>
/// 文件操作服务，封装文件对话框和基础读写操作。
/// </summary>
/// <remarks>
/// <para>封装 Win32 文件对话框和同步/异步文件 IO 操作。</para>
/// <para>当前由外部消费者（如 WpfApp1 的 ScriptManager）使用，ScriptEditor 控件本身不依赖此服务。</para>
/// </remarks>
/// <author>ScriptEditor Team</author>
/// <version>1.0</version>
public class FileService
{
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

    /// <summary>同步读取文件全部文本。</summary>
    public string ReadAllText(string path) => File.ReadAllText(path);

    /// <summary>同步写入文件全部文本。</summary>
    public void WriteAllText(string path, string content) => File.WriteAllText(path, content);

    /// <summary>异步读取文件全部文本。</summary>
    public async Task<string> ReadAllTextAsync(string path)
        => await File.ReadAllTextAsync(path).ConfigureAwait(false);

    /// <summary>异步写入文件全部文本。</summary>
    public async Task WriteAllTextAsync(string path, string content)
        => await File.WriteAllTextAsync(path, content).ConfigureAwait(false);
}