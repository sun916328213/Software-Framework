using CommunityToolkit.Mvvm.ComponentModel;
using ScriptEditor.Models;
using System.Collections.Generic;

namespace ScriptEditor.Models;

/// <summary>
/// 脚本管理器运行时状态模型。
/// </summary>
public class ScriptManagerModel : ObservableObject
{
    private ScriptItem? _selectedItem;
    private string _taskName = "新建任务";
    private bool _isLooping;
    private bool _isRunning;
    private string? _currentFilePath;
    private string _statusText = "就绪";

    /// <summary>当前选中的脚本项（null 表示未选中）。</summary>
    public ScriptItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    /// <summary>当前任务名称（显示在标题栏和持久化文件中）。</summary>
    public string TaskName
    {
        get => _taskName;
        set => SetProperty(ref _taskName, value);
    }

    /// <summary>是否正在循环执行。</summary>
    public bool IsLooping
    {
        get => _isLooping;
        set => SetProperty(ref _isLooping, value);
    }

    /// <summary>是否正在执行脚本。</summary>
    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    /// <summary>当前任务文件的路径（null 表示尚未保存过）。</summary>
    public string? CurrentFilePath
    {
        get => _currentFilePath;
        set => SetProperty(ref _currentFilePath, value);
    }

    /// <summary>状态栏提示文本。</summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    /// <summary>状态变化时触发。</summary>
    public event Action? StateChanged;

    /// <summary>集合变化时触发。</summary>
    public event Action? CollectionChanged;

    /// <summary>通知 StateChanged 事件订阅者。</summary>
    public void NotifyStateChanged() => StateChanged?.Invoke();

    /// <summary>通知 CollectionChanged 事件订阅者。</summary>
    public void NotifyCollectionChanged() => CollectionChanged?.Invoke();
}

/// <summary>任务工程文件模型（.taskproj JSON 格式）。</summary>
public class TaskProjectModel
{
    /// <summary>任务名称。</summary>
    public string Name { get; set; } = "新建任务";

    /// <summary>所有脚本项。</summary>
    public List<ScriptItem> Scripts { get; set; } = new();

    /// <summary>DLL 引用路径列表（全局共享引用）。</summary>
    public List<string> References { get; set; } = new();
}
