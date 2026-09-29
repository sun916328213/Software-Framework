using System.IO;

namespace ScriptEditor.Persistence;

/// <summary>
/// 无头文件存储：纯文本读写，不依赖 WPF 对话框、不创建编辑器控件。
/// </summary>
/// <remarks>
/// P4 边界：<c>Controls/FileService</c> 保留文件对话框（WPF），真正的磁盘读写委托到此类，
/// 为 P5 的 <c>ScriptProjectDto</c> / 版本化持久化保留统一入口。
/// </remarks>
public sealed class ScriptFileStore
{
    public string ReadAllText(string path) => File.ReadAllText(path);

    public void WriteAllText(string path, string content) => File.WriteAllText(path, content);

    public Task<string> ReadAllTextAsync(string path) => File.ReadAllTextAsync(path);

    public Task WriteAllTextAsync(string path, string content) => File.WriteAllTextAsync(path, content);

    public bool Exists(string? path) => !string.IsNullOrEmpty(path) && File.Exists(path);
}
