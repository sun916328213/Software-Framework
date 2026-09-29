using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CodeAnalysis;
using RoslynPad.Roslyn;
using ScriptEditor.Services;

namespace ScriptEditor.Models;

/// <summary>
/// 脚本编辑器运行时状态模型，承载 Roslyn 引擎和所有 UI 状态。
/// </summary>
/// <remarks>
/// 此模型遵循与 <see cref="ScriptManagerModel"/> 一致的设计模式：继承
/// ObservableObject，所有可绑定属性通过 SetProperty 触发通知。
/// </remarks>
public class ScriptEditorModel : ScriptStateBase
{
    // ════════════════════════════════════════════════════════════════
    //  原有属性
    // ════════════════════════════════════════════════════════════════

    /// <summary>Roslyn 编译/执行宿主实例（CustomRoslynHost）。</summary>
    public RoslynHost? RoslynHost { get; set; }

    /// <summary>初始化过程中发生的异常，null 表示初始化成功。</summary>
    public Exception? InitError { get; set; }

    /// <summary>用户添加的额外元数据引用列表（MetadataReference 对象）。</summary>
    public List<MetadataReference> AdditionalReferences { get; } = new();

    /// <summary>用户添加的额外引用文件路径列表。</summary>
    public List<string> AdditionalReferencePaths { get; } = new();

    /// <summary>脚本存储目录路径（指向输出目录下的 Config 文件夹）。</summary>
    public string ScriptsDirectoryPath { get; set; } =
        Path.Combine(
            Path.GetDirectoryName(typeof(ScriptEditorModel).Assembly.Location) ?? ".", "Config");

    // ════════════════════════════════════════════════════════════════
    //  新增：执行引擎状态
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
        AdditionalReferences.Clear();
        AdditionalReferencePaths.Clear();
    }

    /// <summary>添加一条 DLL 引用数据。返回 true 表示成功添加。</summary>
    public bool AddReferenceData(string dllPath)
    {
        if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath)) return false;
        if (AdditionalReferencePaths.Contains(dllPath, StringComparer.OrdinalIgnoreCase)) return false;
        try
        {
            AdditionalReferences.Add(MetadataReference.CreateFromFile(dllPath));
            AdditionalReferencePaths.Add(dllPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>移除一条 DLL 引用数据。返回 true 表示成功移除。</summary>
    public bool RemoveReferenceData(string dllPath)
    {
        var index = AdditionalReferencePaths.FindIndex(
            p => string.Equals(p, dllPath, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;
        AdditionalReferencePaths.RemoveAt(index);
        AdditionalReferences.RemoveAt(index);
        return true;
    }

    /// <summary>刷新 UI 引用列表（从内部数据重新加载 ObservableCollection）。</summary>
    public void RefreshReferenceUI()
    {
        DefaultReferences.Clear();
        References.Clear();
        foreach (var name in GetDefaultReferenceNames())
            DefaultReferences.Add(ReferenceItem.FromDisplay(name));
        foreach (var filePath in AdditionalReferencePaths)
            References.Add(ReferenceItem.FromPath(filePath));
    }

    private IReadOnlyList<string> GetDefaultReferenceNames()
    {
        if (RoslynHost == null) return Array.Empty<string>();
        IEnumerable<MetadataReference> references = RoslynHost.DefaultReferences;
        if (RoslynHost is CustomRoslynHost customHost)
            references = references.Concat(CustomRoslynHost.GetAdditionalDefaultReferences());
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in references)
        {
            var display = reference.Display;
            if (string.IsNullOrWhiteSpace(display)) continue;
            names.Add(display);
        }
        return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
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
