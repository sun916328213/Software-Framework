// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：ScriptFileStore。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO。导入不代表所有类型都会在运行时加载。
using System.IO;

namespace ScriptEditor.Runtime.Persistence;

/// <summary>
/// 无头文件存储：纯文本读写，不依赖 WPF 对话框、不创建编辑器控件。
/// </summary>
/// <remarks>
/// P4 边界：<c>Controls/FileService</c> 保留文件对话框（WPF），真正的磁盘读写委托到此类，
/// 为 P5 的 <c>ScriptProjectDto</c> / 版本化持久化保留统一入口。
/// </remarks>
public sealed class ScriptFileStore
{
    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 ReadAllText。
    /// 正文中的关键协作调用包括 ReadAllText；具体条件和先后次序见方法体。
    /// </summary>
    public string ReadAllText(string path) => File.ReadAllText(path);

    /// <summary>
    /// 执行写入操作；对应入口为 WriteAllText。
    /// 正文中的关键协作调用包括 WriteAllText；具体条件和先后次序见方法体。
    /// </summary>
    public void WriteAllText(string path, string content) => File.WriteAllText(path, content);

    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 ReadAllTextAsync。
    /// 正文中的关键协作调用包括 ReadAllTextAsync；具体条件和先后次序见方法体。
    /// </summary>
    public Task<string> ReadAllTextAsync(string path) => File.ReadAllTextAsync(path);

    /// <summary>
    /// 执行写入操作；对应入口为 WriteAllTextAsync。
    /// 正文中的关键协作调用包括 WriteAllTextAsync；具体条件和先后次序见方法体。
    /// </summary>
    public Task WriteAllTextAsync(string path, string content) => File.WriteAllTextAsync(path, content);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Exists。
    /// 正文中的关键协作调用包括 IsNullOrEmpty、Exists；具体条件和先后次序见方法体。
    /// </summary>
    public bool Exists(string? path) => !string.IsNullOrEmpty(path) && File.Exists(path);
}
