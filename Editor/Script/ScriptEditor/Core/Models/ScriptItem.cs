using System.ComponentModel;
using System.IO;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CodeAnalysis;
using ScriptEditor.Services;

namespace ScriptEditor.Models;

/// <summary>
/// 单个脚本项（前端/后端分离后的轻量数据模型）。
/// </summary>
/// <remarks>
/// <para>承载脚本的标题、源代码、DLL 引用等纯数据，以及执行时状态。
/// 不包含任何 WPF 控件——脚本管理器列表/启动加载只构造本对象，</para>
/// <para>真正的 RoslynPad 编辑器由 ScriptEditorView 在所属标签页被选中时按需初始化。</para>
/// <para>执行通过内部懒加载的 <see cref="ScriptEngine"/> 完成（无头执行，
/// 复用共享 RoslynHost 与静态 <see cref="ScriptExecutionService"/>）。</para>
/// </remarks>
public class ScriptItem : ScriptStateBase
{
    private string _title = "新脚本";
    private string _code = string.Empty;
    private string _itemStatus = "就绪";
    private bool _isEditorInitialized;
    private readonly List<MetadataReference> _references = new();

    private readonly object _engineLock = new();
    private ScriptEngine? _engine;

    /// <summary>编辑器初始化期间的加载窗口引用（初始化完成后关闭并置 null）。</summary>
    [JsonIgnore]
    internal WPFCustomControls.Window.Views.FileLoading? LoadingWindow { get; set; }

    /// <summary>脚本标题（Tab 头显示，支持重命名）。</summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>脚本源代码。</summary>
    public string Code
    {
        get => _code;
        set => SetProperty(ref _code, value);
    }

    /// <summary>列表项状态（执行中…/就绪），由管理器在批量执行时更新。</summary>
    [JsonIgnore]
    public string ItemStatus
    {
        get => _itemStatus;
        set => SetProperty(ref _itemStatus, value);
    }

    private int _caretOffset;
    /// <summary>
    /// 光标偏移量（编辑器内位置，按脚本独立保存）。
    /// 切换标签页时由 ScriptEditorView 保存/恢复，避免不同脚本共享行号。
    /// </summary>
    [JsonIgnore]
    public int CaretOffset
    {
        get => _caretOffset;
        set => SetProperty(ref _caretOffset, value);
    }

    private double _editorScrollOffset;
    /// <summary>
    /// 代码编辑器垂直滚动位置（像素，按脚本独立保存）。
    /// 切换标签页时由 ScriptEditorView 保存/恢复——AvalonEdit 替换文档后会
    /// 保留旧视口偏移，不恢复则新脚本会继承上一脚本的滚动位置。
    /// </summary>
    [JsonIgnore]
    public double EditorScrollOffset
    {
        get => _editorScrollOffset;
        set => SetProperty(ref _editorScrollOffset, value);
    }

    private double _scrollOffset;
    /// <summary>
    /// 输出面板垂直滚动位置（按脚本独立保存）。
    /// 切换标签页时由 ScriptEditorView 保存/恢复，避免切页后继承上一脚本的滚动状态。
    /// </summary>
    [JsonIgnore]
    public double ScrollOffset
    {
        get => _scrollOffset;
        set => SetProperty(ref _scrollOffset, value);
    }

    /// <summary>编辑器是否已初始化完成（设置 true 时自动关闭独立 UI 线程的 LoadingWindow）。</summary>
    [JsonIgnore]
    public bool IsEditorInitialized
    {
        get => _isEditorInitialized;
        set
        {
            if (SetProperty(ref _isEditorInitialized, value) && value)
            {
                if (LoadingWindow != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ScriptItem] IsEditorInitialized=true, closing LoadingWindow for '{Title}'");
                    // 异步关闭独立 UI 线程上的窗口，避免同步 Invoke 导致跨线程死锁
                    var win = LoadingWindow;
                    LoadingWindow = null;
                    win.Dispatcher.BeginInvoke(() =>
                    {
                        win.Close();
                        // 使用 BeginInvokeShutdown 而非 InvokeShutdown，避免在
                        // Dispatcher 回调线程内同步等待关停导致死锁
                        win.Dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Background);
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ScriptItem] IsEditorInitialized=true, but LoadingWindow is null for '{Title}'");
                }
            }
        }
    }

    /// <summary>当前脚本的 DLL 引用路径列表（用于序列化）。</summary>
    public List<string> References { get; set; } = new();

    /// <summary>当前脚本的 DLL 引用路径列表（只读快捷访问）。</summary>
    [JsonIgnore]
    public IReadOnlyList<string> ReferencePaths => _references.Select(r => r.Display ?? string.Empty).ToList();

    /// <summary>设置当前脚本的 DLL 引用路径。</summary>
    public void SetReferences(IEnumerable<string> paths)
    {
        lock (_references)
        {
            _references.Clear();
            References.Clear();
            if (paths == null) return;
            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;
                try
                {
                    _references.Add(MetadataReference.CreateFromFile(path));
                    References.Add(path);
                }
                catch
                {
                    // 单个 DLL 加载失败时忽略，不影响整体
                }
            }
        }
    }

    #region 执行（委托给懒加载的 ScriptEngine）

    /// <summary>获取（或懒创建）本脚本的无头执行引擎。</summary>
    [JsonIgnore]
    public ScriptEngine Engine
    {
        get
        {
            lock (_engineLock)
            {
                return _engine ??= CreateEngine();
            }
        }
    }

    private ScriptEngine CreateEngine()
    {
        var engine = new ScriptEngine(
            () => Code,
            GetReferences,
            () => Array.Empty<int>())
        {
            LoopInterval = LoopInterval
        };
        engine.PropertyChanged += OnEnginePropertyChanged;
        return engine;
    }

    private IReadOnlyList<MetadataReference> GetReferences()
    {
        lock (_references)
        {
            return _references.ToList();
        }
    }

    /// <summary>异步执行脚本。</summary>
    public Task<bool> ExecuteAsync() => Engine.RunAsync();
 
    /// <summary>异步循环执行脚本。</summary>
    public Task LoopRunAsync() => Engine.LoopRunAsync();

    /// <summary>终止当前执行。</summary>
    public void Terminate() => _engine?.Terminate();

    /// <summary>停止循环执行。</summary>
    public void StopLoop() => _engine?.StopLoop();

    /// <summary>后台预编译（加速首次执行）。</summary>
    public Task PreCompileAsync() => Engine.PreCompileAsync(Code);

    private void OnEnginePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        void Apply()
        {
            var engine = _engine;
            if (engine == null) return;
            switch (e.PropertyName)
            {
                case nameof(ScriptEngine.OutputText):
                    OutputText = engine.OutputText;
                    break;
                case nameof(ScriptEngine.StatusText):
                    StatusText = engine.StatusText;
                    break;
                case nameof(ScriptEngine.IsRunning):
                    IsRunning = engine.IsRunning;
                    break;
                case nameof(ScriptEngine.IsLooping):
                    IsLooping = engine.IsLooping;
                    break;
                case nameof(ScriptEngine.ExecutionTime):
                    ExecutionTime = engine.ExecutionTime;
                    break;
            }
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
            dispatcher.InvokeAsync(Apply);
        else
            Apply();
    }

    #endregion

    /// <summary>释放执行相关资源。</summary>
    public void DisposeExecution()
    {
        if (_engine == null) return;
        _engine.PropertyChanged -= OnEnginePropertyChanged;
        _engine.Terminate();
        _engine = null;
    }
}