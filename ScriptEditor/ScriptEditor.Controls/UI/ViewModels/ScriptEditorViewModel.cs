using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScriptEditor.Contracts;
using ScriptEditor.Models;
using ScriptEditor.Services;

namespace ScriptEditor.ViewModels;

/// <summary>
/// 脚本编辑器 ViewModel，仅负责数据绑定和命令暴露。
/// </summary>
/// <remarks>
/// <para>所有业务逻辑委托给 <see cref="ScriptEditorService"/>，本类不含有业务逻辑。</para>
/// <para>View 层通过绑定属性获取 UI 状态，通过命令触发 Service 方法。</para>
/// </remarks>
public class ScriptEditorViewModel : ObservableObject, IDisposable
{
    private readonly ScriptEditorService _editorService;
    private bool _referencesLoaded;
    /// <summary>是否已附加到单例 Service 的事件（Attach/Detach 幂等守卫）。</summary>
    private bool _attached;

    public ScriptEditorViewModel(ScriptEditorService editorService)
    {
        _editorService = editorService;

        // 注意：不再在构造函数中订阅单例 Service 的事件。
        // 单例 Service 的生命周期长于本 VM，构造即订阅会导致废弃 VM 永远无法回收。
        // 改为 View Loaded 时调用 Attach()、最终卸载时调用 Detach()。

        #region 初始化命令

        // 命令可用性直接读当前会话（运行状态的唯一事实源），不再读共享 Model 的镜像。
        RunCommand = new AsyncRelayCommand(_editorService.RunAsync, () => !Session.IsRunning);
        TerminateCommand = new RelayCommand(_editorService.Terminate, () => Session.IsRunning);
        StartLoopCommand = new AsyncRelayCommand(_editorService.LoopRunAsync, () => !Session.IsLooping && !Session.IsRunning);
        StopLoopCommand = new RelayCommand(_editorService.StopLoop, () => Session.IsLooping);
        CompileCommand = new AsyncRelayCommand(_editorService.CompileAsync);
        NewDocumentCommand = new RelayCommand(_editorService.NewDocument);
        OpenDocumentCommand = new RelayCommand(_editorService.OpenDocument);
        SaveCommand = new RelayCommand(_editorService.Save);
        SaveAsCommand = new RelayCommand(_editorService.SaveAs);
        ToggleReferencePanelCommand = new RelayCommand(_editorService.ToggleReferencePanel);
        AddReferenceCommand = new RelayCommand(_editorService.AddReferenceFromDialog);
        RemoveReferenceCommand = new RelayCommand<object?>(param =>
        {
            if (param is ReferenceItem item)
                _editorService.RemoveReferenceItem(item);
        });
        ClearAllReferencesCommand = new RelayCommand(_editorService.ClearAllReferenceItems);
        FormatDocumentCommand = new AsyncRelayCommand(_editorService.FormatDocumentWrapperAsync);
        CommentSelectionCommand = new RelayCommand(_editorService.CommentSelection);
        UncommentSelectionCommand = new RelayCommand(_editorService.UncommentSelection);
        RenameSymbolCommand = new AsyncRelayCommand(RenameSymbolAsync);
        LoadTemplateCommand = new RelayCommand(_editorService.LoadTemplate);
        ToggleDebugModeCommand = new RelayCommand(ToggleDebugMode);
        ClearAllBreakpointsCommand = new RelayCommand(ClearAllBreakpoints);

        #endregion
    }

    /// <summary>
    /// 延迟加载引用列表（首次访问 DefaultReferences / References 时触发）。
    /// 避免构造函数中同步扫描全部框架 DLL（~1700ms），将其推迟到引用面板首次展开时。
    /// </summary>
    private void EnsureReferencesLoaded()
    {
        if (_referencesLoaded) return;
        _referencesLoaded = true;
        _editorService.LoadReferences();
    }

    // ════════════════════════════════════════════════════════════════
    //  Service 事件 → VM 属性刷新（Attach/Detach 生命周期管理）
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 附加到单例 Service 的事件（View Loaded 后调用，幂等可重入）。
    /// </summary>
    /// <remarks>
    /// 分离期间 Service 产生的事件会被跳过，恢复时在此处补一次命令状态刷新，
    /// 避免错过分离期间的 IsRunning/IsLooping 变化。
    /// </remarks>
    public void Attach()
    {
        if (_attached) return;
        _attached = true;
        _editorService.PropertyChanged += OnServicePropertyChanged;
        _editorService.StateChanged += OnServiceStateChanged;
        OnServiceStateChanged();
    }

    /// <summary>
    /// 从单例 Service 事件分离（View 最终卸载时调用，幂等可重入）。
    /// 切断单例 Service 对本 VM 的强引用，允许 VM 及其关联 UI 资源被正常回收。
    /// </summary>
    public void Detach()
    {
        if (!_attached) return;
        _attached = false;
        _editorService.PropertyChanged -= OnServicePropertyChanged;
        _editorService.StateChanged -= OnServiceStateChanged;
    }

    private void OnServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // 将 Service 的属性变更透传到 VM，驱动 WPF 绑定
        OnPropertyChanged(e.PropertyName);
    }

    private void OnServiceStateChanged()
    {
        // IsRunning / IsLooping 变化 → 刷新命令 CanExecute
        RunCommand.NotifyCanExecuteChanged();
        TerminateCommand.NotifyCanExecuteChanged();
        StartLoopCommand.NotifyCanExecuteChanged();
        StopLoopCommand.NotifyCanExecuteChanged();
    }

    // ════════════════════════════════════════════════════════════════
    //  Model 暴露（View 直接绑定 {Binding Model.XXX}）
    // ════════════════════════════════════════════════════════════════

    /// <summary>获取编辑器自身状态模型（文档标题、路径、引用、断点；不含运行状态）。</summary>
    public ScriptEditorModel Model => _editorService.Model;

    /// <summary>
    /// 当前脚本会话：脚本运行状态的唯一事实源。
    /// View 直接绑定 <c>{Binding Session.IsRunning}</c> / <c>{Binding Session.Output}</c> 等。
    /// </summary>
    public IScriptSession Session => _editorService.Session;

    /// <summary>当前关联的脚本项（ScriptManager 标签页场景），供宿主查询标题等脚本数据。</summary>
    public ScriptItem? AttachedItem => _editorService.AttachedItem;

    #region 引用搜索过滤

    private string _referenceFilter = string.Empty;
    /// <summary>引用搜索关键字，过滤 Name 和 FullPath 包含该文本的引用项。</summary>
    public string ReferenceFilter
    {
        get => _referenceFilter;
        set
        {
            if (SetProperty(ref _referenceFilter, value))
            {
                FilteredDefaultReferences.Refresh();
                FilteredReferences.Refresh();
            }
        }
    }

    private bool FilterReferenceItem(object obj)
    {
        if (string.IsNullOrWhiteSpace(_referenceFilter)) return true;
        if (obj is not ReferenceItem item) return false;
        return item.Name.Contains(_referenceFilter, StringComparison.OrdinalIgnoreCase)
            || item.FullPath.Contains(_referenceFilter, StringComparison.OrdinalIgnoreCase);
    }

    private ICollectionView? _filteredDefaultReferences;
    /// <summary>带搜索过滤的默认引用视图。</summary>
    public ICollectionView FilteredDefaultReferences
    {
        get
        {
            if (_filteredDefaultReferences == null)
            {
                _filteredDefaultReferences = CollectionViewSource.GetDefaultView(DefaultReferences);
                _filteredDefaultReferences.Filter = FilterReferenceItem;
            }
            return _filteredDefaultReferences;
        }
    }

    private ICollectionView? _filteredReferences;
    /// <summary>带搜索过滤的用户引用视图。</summary>
    public ICollectionView FilteredReferences
    {
        get
        {
            if (_filteredReferences == null)
            {
                _filteredReferences = CollectionViewSource.GetDefaultView(References);
                _filteredReferences.Filter = FilterReferenceItem;
            }
            return _filteredReferences;
        }
    }

    private RelayCommand? _clearReferenceFilterCommand;
    /// <summary>清除引用搜索文本。</summary>
    public RelayCommand ClearReferenceFilterCommand
        => _clearReferenceFilterCommand ??= new RelayCommand(() => ReferenceFilter = string.Empty);

    #endregion

    #region 调试和断点

    private bool _isDebugMode = true;
    /// <summary>是否启用调试模式。</summary>
    public bool IsDebugMode
    {
        get => _isDebugMode;
        set
        {
            if (SetProperty(ref _isDebugMode, value))
            {
                // 退出调试模式时清除所有断点
                if (!value) Breakpoints.Clear();
            }
        }
    }

    /// <summary>切换调试模式。</summary>
    private void ToggleDebugMode()
    {
        IsDebugMode = !IsDebugMode;
    }

    /// <summary>清除所有断点。</summary>
    private void ClearAllBreakpoints()
    {
        Breakpoints.Clear();
    }

    /// <summary>
    /// 切换指定行的断点状态（由 View 层在边栏点击时调用）。
    /// </summary>
    public bool ToggleBreakpoint(int lineNumber)
    {
        if (lineNumber < 1) return false;
        if (Breakpoints.Contains(lineNumber))
        {
            Breakpoints.Remove(lineNumber);
            return false;
        }
        else
        {
            Breakpoints.Add(lineNumber);
            return true;
        }
    }

    #endregion

    #region 光标位置

    private string _caretLine = "1";
    /// <summary>光标所在行号。</summary>
    public string CaretLine
    {
        get => _caretLine;
        set => SetProperty(ref _caretLine, value);
    }

    private string _caretColumn = "1";
    /// <summary>光标所在列号。</summary>
    public string CaretColumn
    {
        get => _caretColumn;
        set => SetProperty(ref _caretColumn, value);
    }

    #endregion

    #region HasModified（覆盖 Service 行为：结合 * 标记逻辑）

    /// <summary>文档是否已修改（代理到 Service，同时更新 DocumentTitle 的 * 标记）。</summary>
    public bool HasModified => _editorService.Model.HasModified;

    #endregion

    // ════════════════════════════════════════════════════════════════
    //  命令
    // ════════════════════════════════════════════════════════════════

    #region 命令

    /// <summary>执行脚本命令 (F5)。</summary>
    public IAsyncRelayCommand RunCommand { get; }
    /// <summary>终止执行命令 (Shift+F5)。</summary>
    public RelayCommand TerminateCommand { get; }
    /// <summary>开始循环执行命令。</summary>
    public IAsyncRelayCommand StartLoopCommand { get; }
    /// <summary>停止循环执行命令。</summary>
    public RelayCommand StopLoopCommand { get; }
    /// <summary>编译脚本命令。</summary>
    public IAsyncRelayCommand CompileCommand { get; }
    /// <summary>新建文档命令。</summary>
    public RelayCommand NewDocumentCommand { get; }
    /// <summary>打开文档命令。</summary>
    public RelayCommand OpenDocumentCommand { get; }
    /// <summary>保存文档命令 (Ctrl+S)。</summary>
    public RelayCommand SaveCommand { get; }
    /// <summary>另存为命令。</summary>
    public RelayCommand SaveAsCommand { get; }
    /// <summary>切换引用面板命令。</summary>
    public RelayCommand ToggleReferencePanelCommand { get; }
    /// <summary>添加引用命令。</summary>
    public RelayCommand AddReferenceCommand { get; }
    /// <summary>移除引用命令。</summary>
    public RelayCommand<object?> RemoveReferenceCommand { get; }
    /// <summary>清空引用命令。</summary>
    public RelayCommand ClearAllReferencesCommand { get; }
    /// <summary>格式化文档命令 (Ctrl+D)。</summary>
    public IAsyncRelayCommand FormatDocumentCommand { get; }
    /// <summary>注释选中行命令 (Ctrl+K)。</summary>
    public RelayCommand CommentSelectionCommand { get; }
    /// <summary>取消注释命令 (Ctrl+U)。</summary>
    public RelayCommand UncommentSelectionCommand { get; }
    /// <summary>重命名符号命令 (F2)。</summary>
    public IAsyncRelayCommand RenameSymbolCommand { get; }
    /// <summary>加载模板命令。</summary>
    public RelayCommand LoadTemplateCommand { get; }
    /// <summary>切换调试模式命令。</summary>
    public RelayCommand ToggleDebugModeCommand { get; }
    /// <summary>清除所有断点命令。</summary>
    public RelayCommand ClearAllBreakpointsCommand { get; }

    #endregion

    // ════════════════════════════════════════════════════════════════
    //  公共属性（View 层绑定 DataContext 访问）
    // ════════════════════════════════════════════════════════════════

    #region 公共属性

    /// <summary>当前打开的文件路径。</summary>
    public string? CurrentFilePath => _editorService.Model.CurrentFilePath;

    /// <summary>编辑器服务实例（供 View 层初始化时使用）。</summary>
    public ScriptEditorService EditorService => _editorService;

    /// <summary>输出面板文本（转发当前会话；写入即作为编辑器级消息替换输出）。</summary>
    public string OutputText
    {
        get => Session.Output;
        set => Session.SetOutput(value);
    }

    /// <summary>状态栏文本（转发当前会话）。</summary>
    public string StatusText
    {
        get => Session.StatusText;
        set => Session.SetStatus(value);
    }

    /// <summary>文档标题。</summary>
    public string DocumentTitle => _editorService.Model.DocumentTitle;

    /// <summary>脚本是否正在执行（转发当前会话）。</summary>
    public bool IsRunning => Session.IsRunning;

    /// <summary>脚本是否正在循环执行（转发当前会话）。</summary>
    public bool IsLooping => Session.IsLooping;

    /// <summary>循环执行间隔（毫秒，读写当前会话）。</summary>
    public int LoopInterval
    {
        get => _editorService.LoopInterval;
        set => _editorService.LoopInterval = value;
    }

    /// <summary>最近一次执行/编译耗时（转发当前会话）。</summary>
    public string ExecutionTime => Session.ExecutionTime;

    /// <summary>引用管理面板是否展开。</summary>
    public bool IsReferencePanelOpen => _editorService.Model.IsReferencePanelOpen;

    /// <summary>默认程序集引用列表（首次访问时延迟加载）。</summary>
    public ObservableCollection<ReferenceItem> DefaultReferences
    {
        get
        {
            EnsureReferencesLoaded();
            return _editorService.Model.DefaultReferences;
        }
    }

    /// <summary>用户添加的程序集引用列表（首次访问时延迟加载）。</summary>
    public ObservableCollection<ReferenceItem> References
    {
        get
        {
            EnsureReferencesLoaded();
            return _editorService.Model.References;
        }
    }

    /// <summary>断点行号集合（1-based），数据源在 Model 层，Service 执行时直接读取。</summary>
    public ObservableCollection<int> Breakpoints => _editorService.Model.Breakpoints;

    #endregion

    // ════════════════════════════════════════════════════════════════
    //  View 层回调
    // ════════════════════════════════════════════════════════════════

    #region View 层回调

    /// <summary>标记编辑器初始化完成。</summary>
    public void OnEditorInitialized()
    {
        _editorService.Model.StatusText = "编辑器已就绪";
    }

    /// <summary>光标位置变更通知。</summary>
    public void OnCaretPositionChanged(int line, int column)
    {
        CaretLine = line.ToString();
        CaretColumn = column.ToString();
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    //  公共 API（供 ScriptEditorView.xaml.cs 调用）
    // ════════════════════════════════════════════════════════════════

    #region 公共 API

    /// <summary>从文件路径加载脚本内容。</summary>
    public void LoadFromFile(string filePath) => _editorService.LoadFromFile(filePath);

    /// <summary>添加 DLL 引用（传入绝对路径）。</summary>
    public void AddReference(string path) => _editorService.AddReferenceItem(path);

    /// <summary>预编译脚本（后台静默编译，加速首次执行）。</summary>
    public async Task PreCompileScriptAsync(string code) => await _editorService.PreCompileScriptAsync(code);

    #endregion

    // ════════════════════════════════════════════════════════════════
    //  编辑器操作（带 UI 交互）
    // ════════════════════════════════════════════════════════════════

    #region 编辑器操作

    /// <summary>重命名光标处的符号（对话框与重命名逻辑由 Service 承担，VM 只暴露命令）。</summary>
    private async Task RenameSymbolAsync()
    {
        await _editorService.RenameSymbolWithPromptAsync();
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    //  IDisposable
    // ════════════════════════════════════════════════════════════════

    #region IDisposable

    /// <summary>释放资源：分离 Service 事件并终止执行订阅。</summary>
    public void Dispose()
    {
        Detach();
        _editorService.DisposeExecution();
    }

    #endregion
}
