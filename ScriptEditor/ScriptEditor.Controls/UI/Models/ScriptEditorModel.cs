using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ScriptEditor.Models;

/// <summary>
/// 脚本编辑器可绑定状态，不持有 Roslyn 宿主或已加载的程序集引用。
/// </summary>
/// <remarks>
/// <para>此模型遵循与 <see cref="ScriptManagerModel"/> 一致的设计模式：继承
/// ObservableObject，所有可绑定属性通过 SetProperty 触发通知。</para>
/// <para>P3 起本模型**不再保存运行状态**（IsRunning / IsLooping / Output / ExecutionTime 等）——
/// 运行状态由 <see cref="Contracts.IScriptSession"/> 独占持有，UI 直接绑定当前会话。
/// 这里只保留编辑器自身状态：文档标题、文件路径、修改标记、引用列表、断点与状态提示。</para>
/// </remarks>
public class ScriptEditorModel : ObservableObject
{
    // ════════════════════════════════════════════════════════════════
    //  原有属性
    // ════════════════════════════════════════════════════════════════

    /// <summary>用户添加的额外引用文件路径列表。</summary>
    public List<string> AdditionalReferencePaths { get; } = new();

    private string _statusText = "就绪";
    /// <summary>编辑器级状态提示（保存/打开/引用操作结果），与脚本运行状态无关。</summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    // ════════════════════════════════════════════════════════════════
    //  编辑器状态
    // ════════════════════════════════════════════════════════════════

    private string _documentTitle = "无标题";
    /// <summary>文档标题。</summary>
    public string DocumentTitle
    {
        get => _documentTitle;
        set => SetProperty(ref _documentTitle, value);
    }

    private string? _currentFilePath;
    /// <summary>当前打开的文件路径。</summary>
    public string? CurrentFilePath
    {
        get => _currentFilePath;
        set => SetProperty(ref _currentFilePath, value);
    }

    private bool _hasModified;
    /// <summary>文档是否已修改（同步更新 DocumentTitle 的 * 标记）。</summary>
    public bool HasModified
    {
        get => _hasModified;
        set
        {
            if (SetProperty(ref _hasModified, value))
            {
                // 同步更新标题中的 * 标记
                DocumentTitle = value
                    ? DocumentTitle.TrimEnd('*') + "*"
                    : DocumentTitle.TrimEnd('*');
            }
        }
    }

    private bool _isReferencePanelOpen;
    /// <summary>引用管理面板是否展开。</summary>
    public bool IsReferencePanelOpen
    {
        get => _isReferencePanelOpen;
        set => SetProperty(ref _isReferencePanelOpen, value);
    }

    /// <summary>默认程序集引用列表。</summary>
    public ObservableCollection<ReferenceItem> DefaultReferences { get; } = new();

    /// <summary>用户添加的程序集引用列表。</summary>
    public ObservableCollection<ReferenceItem> References { get; } = new();

    /// <summary>断点行号集合（1-based），由 View 层断点边栏操作，Service 层执行时读取。</summary>
    public ObservableCollection<int> Breakpoints { get; } = new();

    // ════════════════════════════════════════════════════════════════
    //  引用数据操作（纯数据层，不涉及 Roslyn 工作区同步）
    // ════════════════════════════════════════════════════════════════

    /// <summary>清空所有用户添加的引用数据（不影响 ObservableCollection UI）。</summary>
    public void ClearReferenceData()
    {
        AdditionalReferencePaths.Clear();
    }

    /// <summary>添加一条 DLL 引用数据。返回 true 表示成功添加。</summary>
    public bool AddReferenceData(string dllPath)
    {
        if (string.IsNullOrWhiteSpace(dllPath)) return false;
        if (AdditionalReferencePaths.Contains(dllPath, StringComparer.OrdinalIgnoreCase)) return false;
        AdditionalReferencePaths.Add(dllPath);
        return true;
    }

    /// <summary>移除一条 DLL 引用数据。返回 true 表示成功移除。</summary>
    public bool RemoveReferenceData(string dllPath)
    {
        var index = AdditionalReferencePaths.FindIndex(
            p => string.Equals(p, dllPath, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;
        AdditionalReferencePaths.RemoveAt(index);
        return true;
    }

    /// <summary>刷新 UI 引用列表（从内部数据重新加载 ObservableCollection）。</summary>
    public void RefreshReferenceUI(IEnumerable<string> defaultReferenceNames)
    {
        DefaultReferences.Clear();
        References.Clear();
        foreach (var name in defaultReferenceNames)
            DefaultReferences.Add(ReferenceItem.FromDisplay(name));
        foreach (var filePath in AdditionalReferencePaths)
            References.Add(ReferenceItem.FromPath(filePath));
    }

}

/// <summary>
/// 表示一个程序集引用项，用于 UI 绑定展示。
/// </summary>
public class ReferenceItem
{
    /// <summary>引用显示名称（文件名不含路径）。</summary>
    public string Name { get; }

    /// <summary>引用文件的完整绝对路径。</summary>
    public string FullPath { get; }

    public ReferenceItem(string name, string fullPath)
    {
        Name = name;
        FullPath = fullPath;
    }

    /// <summary>从完整文件路径创建引用项，自动提取文件名作为显示名称。</summary>
    public static ReferenceItem FromPath(string fullPath)
    {
        return new ReferenceItem(Path.GetFileName(fullPath), fullPath);
    }

    /// <summary>从显示字符串创建引用项。</summary>
    public static ReferenceItem FromDisplay(string display)
    {
        try
        {
            var name = Path.GetFileName(display);
            if (!string.IsNullOrEmpty(name))
                return new ReferenceItem(name, display);
        }
        catch { }
        return new ReferenceItem(display, display);
    }
}
