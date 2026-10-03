// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：ScriptEditorView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Controls、System.Collections.Generic、System.Diagnostics、System.IO、System.Linq、System.Threading。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Controls;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScriptEditor.Controls.Editors;
using ScriptEditor.Controls.Lifecycle;
using ScriptEditor.Controls.Models;
using ScriptEditor.Core.Models;
using ScriptEditor.Controls.Services;
using ScriptEditor.Runtime.Services;
using ScriptEditor.Controls.ViewModels;

namespace ScriptEditor.Controls.Views;

/// <summary>
/// 脚本编辑器可复用控件（View 层，仅编排）。
/// </summary>
/// <remarks>
/// <para>P4 瘦身后本类只保留：依赖属性契约、公共 API、后端装配与初始化编排。</para>
/// <list type="bullet">
///   <item>RoslynPad 操作 → <see cref="RoslynEditorAdapter"/></item>
///   <item>会话订阅/DP 同步 → <see cref="ScriptSessionBinder"/></item>
///   <item>Loaded/Unloaded/Tab 切页/滚动记忆/快捷键 → <see cref="EditorLifecycleController"/></item>
///   <item>重命名对话框 → <see cref="Dialogs.SymbolRenameDialog"/>（经 Service）</item>
///   <item>文件读写 → Runtime/Persistence（经 <see cref="FileService"/>）</item>
/// </list>
/// <para>服务创建：优先使用宿主通过 <see cref="EditorService"/> 附加属性注入的实例，
/// 否则内部新建，不依赖 Prism 容器定位。</para>
/// </remarks>
public partial class ScriptEditorView : UserControl
{
    private ScriptEditorService? _editorService;

    /// <summary>Inherited service supplied by a host composition root before editor initialization.</summary>
    public static readonly DependencyProperty EditorServiceProperty = DependencyProperty.RegisterAttached(
        "EditorService", typeof(ScriptEditorService), typeof(ScriptEditorView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetEditorService。
    /// 正文中的关键协作调用包括 SetValue；具体条件和先后次序见方法体。
    /// </summary>
    public static void SetEditorService(DependencyObject element, ScriptEditorService service)
        => element.SetValue(EditorServiceProperty, service);

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetEditorService。
    /// 正文中的关键协作调用包括 GetValue；具体条件和先后次序见方法体。
    /// </summary>
    public static ScriptEditorService? GetEditorService(DependencyObject element)
        => (ScriptEditorService?)element.GetValue(EditorServiceProperty);

    public ScriptEditorService? EditorService
    {
        get => GetEditorService(this);
        set
        {
            if (_viewModel != null) throw new InvalidOperationException("Editor service cannot change after initialization.");
            if (value is not null) SetEditorService(this, value);
            else ClearValue(EditorServiceProperty);
        }
    }

    private ScriptEditorViewModel? _viewModel;
    private readonly RoslynEditorAdapter _adapter = new();
    private readonly ScriptSessionBinder _binder;
    private readonly EditorLifecycleController _lifecycle;

    private bool _isInitialized;
    /// <summary>压制 ScriptCode 依赖属性变更回调的标志（防止 SetText 循环触发）。</summary>
    private bool _suppressScriptCodeChange;
    private TaskCompletionSource<bool>? _initTcs;
    private DateTime _lastAutoScrollTime = DateTime.MinValue;
    private ScriptItem? _attachedItem;

    /// <summary>构造时暂存的初始文档/输入/输出（宿主窗口场景，后端初始化时回填）。</summary>
    private ScriptEditorState? _initialDocument;
    private object? _initialInputData;
    private string? _initialOutput;

    #region 依赖属性（公共 API 契约）

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ScriptEditorView),
            new PropertyMetadata("脚本"));

    public string ItemStatus
    {
        get => (string)GetValue(ItemStatusProperty);
        set => SetValue(ItemStatusProperty, value);
    }

    public static readonly DependencyProperty ItemStatusProperty =
        DependencyProperty.Register(nameof(ItemStatus), typeof(string), typeof(ScriptEditorView),
            new PropertyMetadata("就绪"));

    public string ScriptCode
    {
        get => (string)GetValue(ScriptCodeProperty);
        set => SetValue(ScriptCodeProperty, value);
    }

    public static readonly DependencyProperty ScriptCodeProperty =
        DependencyProperty.Register(nameof(ScriptCode), typeof(string), typeof(ScriptEditorView),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnScriptCodeChanged));

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnScriptCodeChanged。
    /// 正文中的关键协作调用包括 SetText；具体条件和先后次序见方法体。
    /// </summary>
    private static void OnScriptCodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (ScriptEditorView)d;
        if (view._suppressScriptCodeChange || view._editorService == null) return;
        var newCode = e.NewValue as string ?? string.Empty;
        view._editorService.SetText(newCode);
        if (view._adapter.Editor != null)
            view._adapter.Text = newCode;
    }

    public string OutputText
    {
        get => (string)GetValue(OutputTextProperty);
        set => SetValue(OutputTextProperty, value);
    }

    public static readonly DependencyProperty OutputTextProperty =
        DependencyProperty.Register(nameof(OutputText), typeof(string), typeof(ScriptEditorView),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnOutputTextChanged));

    /// <summary>外部写入输出时转发给当前会话（会话是输出的事实源；内部同步由 Binder 防回环）。</summary>
    private static void OnOutputTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (ScriptEditorView)d;
        view._binder.TryForwardOutputToSession(e.NewValue as string ?? string.Empty);
    }

    public bool IsRunning
    {
        get => (bool)GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    public static readonly DependencyProperty IsRunningProperty =
        DependencyProperty.Register(nameof(IsRunning), typeof(bool), typeof(ScriptEditorView),
            new PropertyMetadata(false));

    public bool IsLooping
    {
        get => (bool)GetValue(IsLoopingProperty);
        set => SetValue(IsLoopingProperty, value);
    }

    public static readonly DependencyProperty IsLoopingProperty =
        DependencyProperty.Register(nameof(IsLooping), typeof(bool), typeof(ScriptEditorView),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public int LoopInterval
    {
        get => (int)GetValue(LoopIntervalProperty);
        set => SetValue(LoopIntervalProperty, value);
    }

    public static readonly DependencyProperty LoopIntervalProperty =
        DependencyProperty.Register(nameof(LoopInterval), typeof(int), typeof(ScriptEditorView),
            new FrameworkPropertyMetadata(1000, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnLoopIntervalChanged));

    /// <summary>外部设置循环间隔时写入当前会话（内部同步由 Binder 防回环）。</summary>
    private static void OnLoopIntervalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (ScriptEditorView)d;
        if (e.NewValue is int interval)
            view._binder.TryForwardLoopIntervalToSession(interval);
    }

    #endregion

    /// <summary>编辑器是否已初始化完成。</summary>
    public new bool IsInitialized => _isInitialized;

    /// <summary>等待编辑器初始化完成（超时默认 10 秒）。</summary>
    public async Task<bool> EnsureInitializedAsync(int timeoutMs = 10000)
    {
        if (_isInitialized) return true;

        if (_lifecycle.DeferLayout)
        {
            _lifecycle.DeferLayout = false;
            if (IsLoaded)
                InvalidateMeasure();
        }

        _initTcs = new TaskCompletionSource<bool>();
        using var cts = new CancellationTokenSource(timeoutMs);
        cts.Token.Register(() => _initTcs.TrySetResult(false));

        if (_isInitialized)
        {
            _initTcs.TrySetResult(true);
            return true;
        }

        return await _initTcs.Task;
    }

    private static int s_instanceCounter;

    /// <summary>
    /// 初始化 ScriptEditorView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 Increment、StartNew、WriteLine、InitializeComponent、AutoScrollOutputToEnd；具体条件和先后次序见方法体。
    /// </summary>
    public ScriptEditorView()
    {
        var instanceId = Interlocked.Increment(ref s_instanceCounter);
        var sw = Stopwatch.StartNew();
        Debug.WriteLine($"[ScriptEditor] Ctor #{instanceId} START");

        InitializeComponent();
        Debug.WriteLine($"[ScriptEditor] Ctor #{instanceId} InitializeComponent: {sw.ElapsedMilliseconds}ms");

        _binder = new ScriptSessionBinder(
            v => OutputText = v,
            v => ItemStatus = v,
            v => IsRunning = v,
            v => IsLooping = v,
            v => LoopInterval = v,
            () => OutputText,
            () => LoopInterval,
            () => AutoScrollOutputToEnd());

        _lifecycle = new EditorLifecycleController(
            this,
            () => DataContext,
            () => _editorService,
            () => _viewModel,
            () => _attachedItem,
            v => _attachedItem = v,
            _adapter,
            _binder,
            OutputTextBox,
            () => InvalidateMeasure(),
            () => _adapter.IsInitialized,
            code => _editorService?.SetText(code),
            paths => SetReferences(paths),
            () => CloseLoadingWindow(),
            () => InitializeCoreAsync());

        Debug.WriteLine($"[ScriptEditor] Ctor #{instanceId} InitializeComponent: {sw.ElapsedMilliseconds}ms");

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;

        OutputTextBox.AddHandler(
            ScrollViewer.ScrollChangedEvent,
            new ScrollChangedEventHandler((_, e) =>
            {
                if (_attachedItem != null && ReferenceEquals(_attachedItem, DataContext))
                    _attachedItem.ScrollOffset = e.VerticalOffset;
            }));

        Debug.WriteLine($"[ScriptEditor] Ctor #{instanceId} TOTAL: {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnLoaded。
    /// 正文中的关键协作调用包括 OnLoaded；具体条件和先后次序见方法体。
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e) => _lifecycle.OnLoaded();

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnUnloaded。
    /// 正文中的关键协作调用包括 OnUnloaded；具体条件和先后次序见方法体。
    /// </summary>
    private void OnUnloaded(object sender, RoutedEventArgs e)
        => _lifecycle.OnUnloaded(Dispatcher, () => IsLoaded, () => _viewModel);

    /// <summary>
    /// TabControl 复用同一视图展示不同脚本时，DataContext 会切换。
    /// 同步切页由控制器处理；首次走异步延迟初始化。
    /// </summary>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not ScriptItem newItem) return;
        if (_attachedItem == newItem) return;

        Debug.WriteLine($"[ScriptEditor] DataContext changed: '{(e.OldValue as ScriptItem)?.Title}' -> '{newItem.Title}'");

        if (_adapter.IsInitialized)
            _isInitialized = false;
        else
        {
            _isInitialized = false;
            _initTcs = new TaskCompletionSource<bool>();
        }

        var handledSync = _lifecycle.OnDataContextChanged(newItem, _ => { _initTcs ??= new TaskCompletionSource<bool>(); });
        if (handledSync)
        {
            _isInitialized = true;
            _initTcs?.TrySetResult(true);
            InvalidateMeasure();
            CloseLoadingWindow();
        }
    }

    /// <summary>带初始数据的构造重载（宿主窗口场景）。暂存到后端初始化时应用。</summary>
    public ScriptEditorView(string title, global::ScriptEditor.Core.Models.ScriptEditorState document, object? inputData, string? initialOutput = null) : this()
    {
        Title = title;
        _initialDocument = document;
        _initialInputData = inputData;
        _initialOutput = initialOutput;
    }

    #region 公共 API（委托 Service/ViewModel/Session，不含业务逻辑）

    public string? FilePath => _viewModel?.CurrentFilePath;

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetScriptCode。
    /// </summary>
    public void SetScriptCode(string code) => _editorService?.SetText(code);

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetCurrentCode。
    /// </summary>
    public string GetCurrentCode() => _editorService?.GetText() ?? string.Empty;

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetLastScriptResult。
    /// </summary>
    public object? GetLastScriptResult() => _editorService?.LastScriptResult;

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetInputData。
    /// </summary>
    public void SetInputData(object? data) => _editorService?.SetInputData(data);

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetReferencePaths。
    /// 正文中的关键协作调用包括 ToList、Select、Empty；具体条件和先后次序见方法体。
    /// </summary>
    public IReadOnlyList<string> GetReferencePaths() =>
        _viewModel?.References.Select(r => r.FullPath).ToList() ?? (IReadOnlyList<string>)Array.Empty<string>();

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetReferences。
    /// 正文中的关键协作调用包括 Clear、AddReference；具体条件和先后次序见方法体。
    /// </summary>
    public void SetReferences(IEnumerable<string> paths)
    {
        if (_viewModel == null) return;
        _viewModel.References.Clear();
        foreach (var path in paths)
            _viewModel.AddReference(path);
    }

    public global::ScriptEditor.Core.Models.ScriptEditorState Document => new()
    {
        Code = GetCurrentCode(),
        References = GetReferencePaths().ToList()
    };

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ClearOutput。
    /// 正文中的关键协作调用包括 SetOutput；具体条件和先后次序见方法体。
    /// </summary>
    public void ClearOutput() => _editorService?.Session.SetOutput(string.Empty);

    /// <summary>
    /// 执行保存操作；对应入口为 Save。
    /// 正文中的关键协作调用包括 TryExecuteCommand；具体条件和先后次序见方法体。
    /// </summary>
    public void Save() => TryExecuteCommand(_viewModel?.SaveCommand);

    /// <summary>
    /// 执行保存操作；对应入口为 SaveAs。
    /// 正文中的关键协作调用包括 TryExecuteCommand；具体条件和先后次序见方法体。
    /// </summary>
    public void SaveAs() => TryExecuteCommand(_viewModel?.SaveAsCommand);

    /// <summary>
    /// 执行当前入口的处理流程；对应入口为 Execute。
    /// 正文中的关键协作调用包括 TryExecuteCommand；具体条件和先后次序见方法体。
    /// </summary>
    public void Execute() => TryExecuteCommand(_viewModel?.RunCommand);

    /// <summary>
    /// 执行当前入口的处理流程；对应入口为 ExecuteAsync。
    /// 正文中的关键协作调用包括 ExecuteAsync；具体条件和先后次序见方法体。
    /// </summary>
    public Task ExecuteAsync() => _viewModel?.RunCommand.ExecuteAsync(null) ?? Task.CompletedTask;

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Terminate。
    /// 正文中的关键协作调用包括 TryExecuteCommand；具体条件和先后次序见方法体。
    /// </summary>
    public void Terminate() => TryExecuteCommand(_viewModel?.TerminateCommand);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 StartLoop。
    /// 正文中的关键协作调用包括 TryExecuteCommand；具体条件和先后次序见方法体。
    /// </summary>
    public void StartLoop() => TryExecuteCommand(_viewModel?.StartLoopCommand);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 StartLoopAsync。
    /// 正文中的关键协作调用包括 CheckAccess、DispatchLoopToUiAsync、ExecuteAsync；具体条件和先后次序见方法体。
    /// </summary>
    public Task StartLoopAsync()
    {
        if (_viewModel == null) return Task.CompletedTask;
        if (!Dispatcher.CheckAccess())
            return DispatchLoopToUiAsync();

        if (_viewModel.IsLooping)
        {
            var task = _viewModel.StartLoopCommand.ExecutionTask;
            return task ?? Task.CompletedTask;
        }

        return _viewModel.StartLoopCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 DispatchLoopToUiAsync。
    /// 正文中的关键协作调用包括 InvokeAsync、StartLoopAsync、TrySetResult、TrySetException；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private Task DispatchLoopToUiAsync()
    {
        var tcs = new TaskCompletionSource();
        Dispatcher.InvokeAsync(async () =>
        {
            try { await StartLoopAsync(); tcs.TrySetResult(); }
            catch (Exception ex) { tcs.TrySetException(ex); }
        });
        return tcs.Task;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 StopLoop。
    /// 正文中的关键协作调用包括 TryExecuteCommand；具体条件和先后次序见方法体。
    /// </summary>
    public void StopLoop() => TryExecuteCommand(_viewModel?.StopLoopCommand);

    /// <summary>
    /// 执行加载操作；对应入口为 LoadFromFile。
    /// </summary>
    public void LoadFromFile(string filePath) => _viewModel?.LoadFromFile(filePath);

    /// <summary>
    /// 执行添加操作；对应入口为 AddReference。
    /// </summary>
    public void AddReference(string path) => _viewModel?.AddReference(path);

    /// <summary>
    /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryExecuteCommand。
    /// 正文中的关键协作调用包括 Execute；具体条件和先后次序见方法体。
    /// </summary>
    private static void TryExecuteCommand(ICommand? command)
    {
        if (command?.CanExecute(null) == true)
            command.Execute(null);
    }

    #endregion

    #region 初始化编排（重量级工作委托 Adapter/Service，状态同步委托 Binder/Lifecycle）

    public async Task<bool> InitializeAsync()
    {
        if (_isInitialized) return true;
        return await InitializeCoreAsync();
    }

    /// <summary>延迟初始化后端：解析服务、创建 ViewModel、装配工具栏与引用面板。</summary>
    private void EnsureBackendInitialized()
    {
        if (_viewModel != null) return;

        var sw = Stopwatch.StartNew();
        _editorService ??= EditorService ?? new ScriptEditorService();

        SetInputData(_initialInputData!);

        _viewModel = new ScriptEditorViewModel(_editorService);
        _viewModel.Attach();

        if (_initialDocument != null)
            SetReferences(_initialDocument.References);

        if (_attachedItem != null)
        {
            _lifecycle.SuppressBreakpointSync = true;
            try
            {
                foreach (var bp in _attachedItem.Breakpoints)
                    _viewModel.Breakpoints.Add(bp);
            }
            finally
            {
                _lifecycle.SuppressBreakpointSync = false;
            }
        }

        LayoutRoot.DataContext = _viewModel;

        EditorLifecycleController.SetupKeyBindings(this, _viewModel);

        var toolbar = new ScriptEditorToolbar { DataContext = _viewModel };
        ToolbarHost.Content = toolbar;

        var refPanel = new ScriptEditorReferencePanel
        {
            Visibility = Visibility.Collapsed,
            DataContext = _viewModel
        };
        var visibilityBinding = new System.Windows.Data.Binding(nameof(ScriptEditorViewModel.IsReferencePanelOpen))
        {
            Source = _viewModel,
            Converter = new System.Windows.Controls.BooleanToVisibilityConverter()
        };
        refPanel.SetBinding(UIElement.VisibilityProperty, visibilityBinding);
        ReferencePanelHost.Content = refPanel;

        _viewModel.PropertyChanged += _lifecycle.OnViewModelSessionChanged;
        _binder.Bind(_editorService.Session);

        if (_attachedItem == null && !string.IsNullOrEmpty(_initialOutput))
        {
            _editorService.Session.SetOutput(_initialOutput);
            _initialOutput = null;
        }

        Debug.WriteLine($"[ScriptEditor] EnsureBackend TOTAL: {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>初始化核心逻辑：RoslynPad 首启或内容切换。</summary>
    private async Task<bool> InitializeCoreAsync()
    {
        var totalSw = Stopwatch.StartNew();

        EnsureBackendInitialized();
        if (_adapter.IsInitialized)
        {
            _lifecycle.SaveEditorScrollState();
            _lifecycle.SaveOutputScrollState();
            if (_attachedItem != null)
            {
                _editorService!.SetText(_attachedItem.Code);
                _editorService!.AttachedItem = _attachedItem;
                _binder.Bind(_editorService.Session);
                _lifecycle.RestoreEditorScrollState(_attachedItem);
                _lifecycle.RestoreOutputScrollState(_attachedItem);
            }
            _isInitialized = true;
            _initTcs?.TrySetResult(true);
            CloseLoadingWindow();
            return true;
        }

        if (_editorService!.InitError != null)
        {
            _viewModel!.OutputText = $"RoslynHost 初始化失败:\n{_editorService.InitError.Message}\n\n{_editorService.InitError.StackTrace}";
            _viewModel.StatusText = "初始化失败";
            return false;
        }

        var roslynHost = _editorService.RoslynHost;
        if (roslynHost == null)
            return false;

        try
        {
            var workingDir = Path.GetDirectoryName(typeof(ScriptEditorService).Assembly.Location) ?? ".";

            var template = !string.IsNullOrEmpty(_initialDocument?.Code)
                ? _initialDocument.Code
                : !string.IsNullOrEmpty(_attachedItem?.Code)
                    ? _attachedItem.Code
                    : _editorService!.GetDefaultTemplate();
            _initialDocument = null;

            _editorService.SetText(template);
            _adapter.EnsureCreated(editorRoot, () => (System.Windows.Media.Brush)FindResource("Color_Gray5"));

            var sw = Stopwatch.StartNew();
            var documentId = await _adapter.InitializeAsync(roslynHost, workingDir, template);
            Debug.WriteLine($"[ScriptEditor] InitializeAsync: {sw.ElapsedMilliseconds}ms");

            sw.Restart();
            _editorService!.Initialize(_adapter.Editor!, roslynHost, documentId);
            _viewModel!.OnEditorInitialized();

            _adapter.TextChanged += code =>
            {
                _suppressScriptCodeChange = true;
                try { ScriptCode = code; }
                finally { _suppressScriptCodeChange = false; }

                if (_attachedItem != null && ReferenceEquals(_attachedItem, DataContext))
                    _attachedItem.Code = code;
            };

            _adapter.CaretChanged += (line, column, offset) =>
            {
                _viewModel!.OnCaretPositionChanged(line, column);
                if (_attachedItem != null && ReferenceEquals(_attachedItem, DataContext))
                    _attachedItem.CaretOffset = offset;
            };

            _adapter.ScrollChanged += _ =>
            {
                if (_attachedItem != null && ReferenceEquals(_attachedItem, DataContext))
                    _attachedItem.EditorScrollOffset = _adapter.VerticalOffset;
            };

            if (_attachedItem != null)
                _attachedItem.Code = _editorService!.GetText();

            if (_attachedItem != null)
            {
                _adapter.CaretOffset = _attachedItem.CaretOffset;
                _lifecycle.RestoreEditorScrollState(_attachedItem);
                _lifecycle.RestoreOutputScrollState(_attachedItem);
            }
            Debug.WriteLine($"[ScriptEditor] Events: {sw.ElapsedMilliseconds}ms");

            _isInitialized = true;
            _initTcs?.TrySetResult(true);
            CloseLoadingWindow();

            sw.Restart();
            _adapter.AttachBreakpoints(
                _viewModel!.Breakpoints,
                line => _viewModel.ToggleBreakpoint(line),
                _viewModel,
                nameof(ScriptEditorViewModel.IsDebugMode));
            _viewModel.Breakpoints.CollectionChanged += _lifecycle.OnBreakpointsChanged;
            Debug.WriteLine($"[ScriptEditor] SetupBreakpointGutter: {sw.ElapsedMilliseconds}ms");

            var currentCode = _editorService!.GetText();
            if (!string.IsNullOrEmpty(currentCode))
                _ = _viewModel!.PreCompileScriptAsync(currentCode);

            Debug.WriteLine($"[ScriptEditor] InitializeCoreAsync TOTAL: {totalSw.ElapsedMilliseconds}ms");
            return true;
        }
        catch (Exception ex)
        {
            _viewModel!.OutputText = $"编辑器初始化失败:\n{ex.Message}\n\n{ex.StackTrace}";
            _viewModel.StatusText = "初始化失败";
            return false;
        }
    }

    /// <summary>通知当前关联脚本项关闭 FileLoading 窗口。</summary>
    private void CloseLoadingWindow()
    {
        if (_attachedItem != null)
        {
            _attachedItem.IsEditorInitialized = true;
            ScriptLoadingWindowRegistry.Close(_attachedItem);
        }
    }

    /// <summary>输出面板自动滚到底部（50ms 防抖；切页同步期间由 Binder 抑制）。</summary>
    private void AutoScrollOutputToEnd()
    {
        var now = DateTime.Now;
        if (_binder.SuppressAutoScroll || (now - _lastAutoScrollTime).TotalMilliseconds < 50) return;
        _lastAutoScrollTime = now;
        OutputTextBox.Dispatcher.InvokeAsync(() =>
        {
            OutputTextBox.CaretIndex = OutputTextBox.Text.Length;
            OutputTextBox.ScrollToEnd();
        });
    }

    /// <summary>延迟布局：所属 Tab 未选中时跳过测量，直到选中后恢复。</summary>
    protected override Size MeasureOverride(Size constraint)
    {
        if (_lifecycle.DeferLayout)
            return Size.Empty;
        return base.MeasureOverride(constraint);
    }

    #endregion
}
