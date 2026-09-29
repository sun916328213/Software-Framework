using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using ScriptEditor.Helpers;

namespace ScriptEditor.Models;

/// <summary>
/// 脚本执行状态基类，提供 ScriptItem 和 ScriptEditorModel 共享的运行状态属性。
/// </summary>
public abstract class ScriptStateBase : ObservableObject
{
    private string _outputText = string.Empty;
    /// <summary>输出面板文本（自动截断超长内容，保留最多 200 行）。</summary>
    [JsonIgnore]
    public string OutputText
    {
        get => _outputText;
        set => SetProperty(ref _outputText, OutputTruncator.Truncate(value));
    }

    private string _statusText = "就绪";
    /// <summary>状态栏文本。</summary>
    [JsonIgnore]
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private bool _isRunning;
    /// <summary>是否正在执行。</summary>
    [JsonIgnore]
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
                NotifyStateChanged();
        }
    }

    private bool _isLooping;
    /// <summary>是否正在循环执行。</summary>
    [JsonIgnore]
    public bool IsLooping
    {
        get => _isLooping;
        set
        {
            if (SetProperty(ref _isLooping, value))
                NotifyStateChanged();
        }
    }

    private int _loopInterval = 200;
    /// <summary>循环执行间隔（毫秒）。</summary>
    [JsonIgnore]
    public int LoopInterval
    {
        get => _loopInterval;
        set => SetProperty(ref _loopInterval, value);
    }

    private string _executionTime = string.Empty;
    /// <summary>最近一次执行耗时。</summary>
    [JsonIgnore]
    public string ExecutionTime
    {
        get => _executionTime;
        set => SetProperty(ref _executionTime, value);
    }

    /// <summary>综合状态变更通知（IsRunning / IsLooping / StatusText 变化时触发）。</summary>
    public event Action? StateChanged;

    /// <summary>通知 StateChanged 事件订阅者。</summary>
    public void NotifyStateChanged() => StateChanged?.Invoke();
}