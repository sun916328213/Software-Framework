using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.CodeAnalysis;
using Prism.Ioc;
using RoslynPad.Editor;
using ScriptEditor.Models;
using ScriptEditor.Services;
using ScriptEditor.ViewModels;

namespace ScriptEditor.Views;

/// <summary>
/// 脚本编辑器可复用控件（View 层）。
/// </summary>
/// <remarks>
/// <para>职责范围：</para>
/// <list type="bullet">
///   <item>UI 布局和 RoslynPad 编辑器初始化</item>
///   <item>通过依赖属性暴露脚本代码、输出、状态供外部绑定</item>
///   <item>提供 Execute/ExecuteAsync/Terminate/SetScriptCode/LoadFromFile/AddReference 等公共 API</item>
/// </list>
/// <para>可复用性：直接放入任意 WPF 容器的 XAML 中即可使用，通过 ScriptCode 绑定
/// 传入脚本代码，通过 Execute() 触发执行，通过 OutputText 获取执行结果。</para>
/// <para>服务创建：内部直接实例化 ScriptEditorService 和 FileService，不依赖 Prism 容器定位。</para>
/// </remarks>
/// <author>ScriptEditor Team</author>
/// <version>1.0</version>
public partial class ScriptEditorView : UserControl
{
    /// <summary>
    /// 脚本编辑器后端服务（前后端分离：不再在构造函数中同步解析，改为 InitializeCoreAsync 时延迟初始化）。
    /// </summary>
    private ScriptEditorService? _editorService;
    /// <summary>
    /// 脚本编辑器 ViewModel（前后端分离：不再在构造函数中同步创建，改为 InitializeCoreAsync 时延迟初始化）。
    /// </summary>
    private ScriptEditorViewModel? _viewModel;
    /// <summary>标记编辑器 RoslynPad 初始化是否已完成。</summary>
    private bool _isInitialized;
    /// <summary>
    /// 标记 RoslynPad 的 scriptEditor.InitializeAsync 是否已完成（首次成功后不再重置）。
    /// Tab 切换时复用同一 ScriptEditorView 实例，RoslynPad 只需初始化一次，
    /// 后续切换仅做轻量内容替换。
    /// </summary>
    private bool _roslynPadInitialized;
    /// <summary>压制 ScriptCode 依赖属性变更回调的标志（防止 SetText 循环触发）。</summary>
    private bool _suppressScriptCodeChange;
    /// <summary>压制 OutputText 依赖属性变更回调的标志（防止双向绑定循环触发）。</summary>
    private bool _suppressOutputChange;
    /// <summary>抑制输出面板自动滚动的标志。切页播种（AttachToItem/VM 种子同步）会触发
    /// ViewModel 输出变化事件，若不抑制会把新脚本的输出强制滚到底部。
    /// </summary>
    private bool _suppressAutoScroll;
    /// <summary>抑制断点集合变更同步到 ScriptItem 的标志（切页保存/恢复期间使用）。</summary>
    private bool _suppressBreakpointSync;
    /// <summary>异步等待初始化完成的 TaskCompletionSource。</summary>
    private TaskCompletionSource<bool>? _initTcs;
    /// <summary>
    /// 延迟布局标志。为 true 时 MeasureOverride 返回空尺寸，
    /// 避免 TabControl 一次性 realized 大量脚本时测量所有编辑器模板。
    /// 当所属 TabItem 被选中后才设为 false 并恢复布局。
    /// </summary>
    private bool _deferLayout = true;
    /// <summary>上次自动滚动时间，用于防节流。</summary>
    private DateTime _lastAutoScrollTime = DateTime.MinValue;
    /// <summary>当前绑定的脚本项（ScriptManager 标签页场景）。</summary>
    private ScriptItem? _attachedItem;
    /// <summary>OnLoaded 是否已处理（防止重复挂接）。</summary>
    private bool _onLoadedHandled;
    /// <summary>初始化延迟挂接是否已完成。</summary>
    private bool _initDeferralHooked;
    /// <summary>
    /// 构造时传入的初始文档快照（代码 + DLL 引用，ScriptNode 宿主窗口场景）。
    /// 后端初始化前 SetScriptCode/SetReferences 是空操作，此处统一暂存，
    /// 确保初始化时回填到编辑器，避免每次打开都被默认模板覆盖。
    /// null 表示 ScriptManager 标签页场景（由 _attachedItem 提供）。
    /// </summary>
    private ScriptEditorState? _initialDocument;
    /// <summary>构造时传入的输入数据（与 _initialDocument 解耦：document 可空但 inputData 可独立传入）。</summary>
    private object? _initialInputData;

    /// <summary>
    /// RoslynPad 代码编辑器控件（前后端分离：不再由 XAML 自动生成，改为 InitializeCoreAsync 中动态创建）。
    /// </summary>
    private RoslynCodeEditorView? scriptEditor;

    /// <summary>
    /// VS 风格断点边栏控件（前后端分离：不再由 XAML 自动生成，改为 InitializeCoreAsync 中动态创建）。
    /// </summary>
    private BreakpointGutter? breakpointGutter;

    #region 依赖属性（公共 API 契约）

    /// <summary>脚本标题（用于 TabControl 头部显示）。</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ScriptEditorView),
            new PropertyMetadata("脚本"));

    /// <summary>执行状态文本（供外部管理器跟踪状态）。</summary>
    public string ItemStatus
    {
        get => (string)GetValue(ItemStatusProperty);
        set => SetValue(ItemStatusProperty, value);
    }

    public static readonly DependencyProperty ItemStatusProperty =
        DependencyProperty.Register(nameof(ItemStatus), typeof(string), typeof(ScriptEditorView),
            new PropertyMetadata("就绪"));

    /// <summary>脚本源代码文本，支持双向绑定。</summary>
    public string ScriptCode
    {
        get => (string)GetValue(ScriptCodeProperty);
        set => SetValue(ScriptCodeProperty, value);
    }

    public static readonly DependencyProperty ScriptCodeProperty =
        DependencyProperty.Register(nameof(ScriptCode), typeof(string), typeof(ScriptEditorView),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnScriptCodeChanged));

    private static void OnScriptCodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (ScriptEditorView)d;
        if (view._suppressScriptCodeChange || view._editorService == null) return;
        var newCode = e.NewValue as string ?? string.Empty;
        view._editorService.SetText(newCode);
        // 直接更新本地编辑器，确保共享 Service 场景下正确的编辑器被更新
        if (view.scriptEditor != null)
            view.scriptEditor.Text = newCode;
    }

    /// <summary>输出面板文本（双向绑定，供外部获取执行结果）。</summary>
    public string OutputText
    {
        get => (string)GetValue(OutputTextProperty);
        set => SetValue(OutputTextProperty, value);
    }

    public static readonly DependencyProperty OutputTextProperty =
        DependencyProperty.Register(nameof(OutputText), typeof(string), typeof(ScriptEditorView),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnOutputTextChanged));

    /// <summary>
    /// 外部（脚本项宿主绑定）设置 OutputText 时，同步到 ViewModel/Service 与当前脚本项，
    /// 使输出面板展示外部引擎（如启动流程无头执行）产生的输出。
    /// </summary>
    private static void OnOutputTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (ScriptEditorView)d;
        if (view._suppressOutputChange || view._viewModel == null) return;
        view._viewModel.OutputText = e.NewValue as string ?? string.Empty;
        // 模板绑定为 OneWay，此处显式镜像到当前脚本项，保证切页后数据不丢
        if (ReferenceEquals(view._attachedItem, view.DataContext))
            view._attachedItem.OutputText = view._viewModel.OutputText;
    }

    /// <summary>脚本是否正在执行中。</summary>
    public bool IsRunning
    {
        get => (bool)GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    public static readonly DependencyProperty IsRunningProperty =
        DependencyProperty.Register(nameof(IsRunning), typeof(bool), typeof(ScriptEditorView),
            new PropertyMetadata(false));

    /// <summary>脚本是否正在循环执行。</summary>
    public bool IsLooping
    {
        get => (bool)GetValue(IsLoopingProperty);
        set => SetValue(IsLoopingProperty, value);
    }

    public static readonly DependencyProperty IsLoopingProperty =
        DependencyProperty.Register(nameof(IsLooping), typeof(bool), typeof(ScriptEditorView),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>循环执行间隔（毫秒）。</summary>
    public int LoopInterval
    {
        get => (int)GetValue(LoopIntervalProperty);
        set => SetValue(LoopIntervalProperty, value);
    }

    public static readonly DependencyProperty LoopIntervalProperty =
        DependencyProperty.Register(nameof(LoopInterval), typeof(int), typeof(ScriptEditorView),
            new FrameworkPropertyMetadata(1000, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnLoopIntervalChanged));

    /// <summary>外部（脚本项宿主绑定）设置循环间隔时，同步到 ViewModel/Service（再转发到后端引擎）。</summary>
    private static void OnLoopIntervalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (ScriptEditorView)d;
        if (view._viewModel == null) return;
        if (e.NewValue is int interval)
            view._viewModel.LoopInterval = interval;
    }

    #endregion

    /// <summary>编辑器是否已初始化完成。</summary>
    public new bool IsInitialized => _isInitialized;

    /// <summary>等待编辑器初始化完成（超时默认 10 秒）。</summary>
    /// <returns>是否成功初始化。</returns>
    public async Task<bool> EnsureInitializedAsync(int timeoutMs = 10000)
    {
        if (_isInitialized) return true;

        // 主动调用时也需清除延迟布局标志并刷新布局
        if (_deferLayout)
        {
            _deferLayout = false;
            if (IsLoaded)
                InvalidateMeasure();
        }

        _initTcs = new TaskCompletionSource<bool>();
        using var cts = new CancellationTokenSource(timeoutMs);
        cts.Token.Register(() => _initTcs.TrySetResult(false));

        // 再次检查（防止竞态）
        if (_isInitialized)
        {
            _initTcs.TrySetResult(true);
            return true;
        }

        return await _initTcs.Task;
    }

    /// <summary>
    /// 缓存的语法高亮配色方案（所有 ScriptEditorView 实例共享，避免每次 InitializeAsync 重复创建）。
    /// </summary>
    private static readonly ClassificationHighlightColors SharedHighlightColors = new();

    private static int s_instanceCounter;

    public ScriptEditorView()
    {
        var instanceId = Interlocked.Increment(ref s_instanceCounter);
        var sw = Stopwatch.StartNew();
        Debug.WriteLine($"[ScriptEditor] Ctor #{instanceId} START");

        InitializeComponent();
        Debug.WriteLine($"[ScriptEditor] Ctor #{instanceId} InitializeComponent: {sw.ElapsedMilliseconds}ms");

        // 前后端分离：Service/ViewModel 延迟到 InitializeCoreAsync 中初始化，
        // 构造函数只做轻量工作：BAML 加载 + 生命周期事件挂接
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;

        // 输出面板滚动位置实时记录到当前脚本项（按脚本独立记忆）。
        // 与编辑器滚动同理：切页瞬间保存会因绑定已换内容而失效，必须实时记录。
        // TextBox 无 ScrollChanged 事件，挂接其内部 ScrollViewer 冒泡上来的路由事件
        OutputTextBox.AddHandler(
            System.Windows.Controls.ScrollViewer.ScrollChangedEvent,
            new System.Windows.Controls.ScrollChangedEventHandler((_, e) =>
            {
                if (_attachedItem != null && ReferenceEquals(_attachedItem, DataContext))
                    _attachedItem.ScrollOffset = e.VerticalOffset;
            }));

        Debug.WriteLine($"[ScriptEditor] Ctor #{instanceId} TOTAL: {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// 当 TabControl 复用同一 ScriptEditorView 实例来展示不同 Tab 的内容时，
    /// DataContext 会从旧 ScriptItem 切换为新 ScriptItem。此时需要重置初始化状态
    /// 并重新运行初始化链路，否则新 Tab 的 FileLoading 永远不会关闭。
    /// </summary>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not ScriptItem newItem) return;
        if (_attachedItem == newItem) return; // 同一个 item，无需重初始化

        var oldItem = e.OldValue as ScriptItem;
        Debug.WriteLine($"[ScriptEditor] DataContext changed: '{oldItem?.Title}' -> '{newItem.Title}'");

        // 注意：不在此时保存旧 item 状态——WPF TwoWay 绑定在 DataContextChanged 之前
        // 已将 newItem.Code 推入编辑器，此时读编辑器得到的是新数据而非旧数据。
        // 状态由 Document.TextChanged 事件实时写回 _attachedItem，无需手动保存。

        // RoslynPad 已初始化：同步完成内容替换，不走异步链路
        if (_roslynPadInitialized)
        {
            Debug.WriteLine("[ScriptEditor] RoslynPad initialized, doing synchronous tab switch");
            _isInitialized = false;
            _deferLayout = false;

            // 切页前保存旧脚本项光标（滚动位置已由 ScrollOffsetChanged/ScrollChanged
            // 实时记录，此处无需也无法可靠保存——绑定此刻已完成内容替换）
            if (_attachedItem != null && scriptEditor != null)
                _attachedItem.CaretOffset = scriptEditor.TextArea.Caret.Offset;

            // 立即把输出面板归零：文本替换时 WPF 会保留旧偏移，
            // 不先归零则新脚本会视觉上继承上一脚本的滚动位置
            OutputTextBox.ScrollToVerticalOffset(0);

            EnsureBackendInitialized();
            AttachToItem(newItem);
            _editorService!.SetText(newItem.Code);
            // 直接更新本地编辑器，确保共享 Service 场景下正确的编辑器被更新
            if (scriptEditor != null)
            {
                scriptEditor.Text = newItem.Code;
                // 恢复新脚本项自己的光标位置（钳制到文本长度内），
                // 触发 PositionChanged 同步刷新状态栏 Ln/Col 显示
                var codeLength = newItem.Code?.Length ?? 0;
                scriptEditor.TextArea.Caret.Offset = Math.Min(newItem.CaretOffset, codeLength);
            }
            // 恢复新脚本项自己的代码滚动位置与输出滚动位置（不继承上一脚本）
            RestoreEditorScrollState(newItem);
            RestoreOutputScrollState(newItem);
            _isInitialized = true;
            _initTcs?.TrySetResult(true);
            InvalidateMeasure();
            CloseLoadingWindow();
            return;
        }

        // 首次初始化：走异步延迟初始化链路
        _isInitialized = false;
        _initDeferralHooked = false;
        _deferLayout = true;
        _initTcs = new TaskCompletionSource<bool>();

        AttachToItem(newItem);
        EnsureDeferredInitialization();
    }

    /// <summary>
    /// 带初始数据的构造重载（宿主窗口场景）：设置标题、代码、DLL 引用与输入数据。
    /// </summary>
    public ScriptEditorView(string title, Models.ScriptEditorState document, object? inputData) : this()
    {
        Title = title;
        // 暂存初始数据：后端未初始化时 SetScriptCode/SetReferences/SetInputData 都是空操作，
        // 必须延后到 EnsureBackendInitialized 时应用，否则每次打开都会被默认模板覆盖。
        _initialDocument = document;
        _initialInputData = inputData;
    }

    #region 公共 API

    /// <summary>当前关联的文件路径（用于保存/加载），null 表示尚未保存。</summary>
    public string? FilePath => _viewModel?.CurrentFilePath;

    /// <summary>设置脚本代码文本（替换全部内容）。</summary>
    /// <param name="code">C# 脚本源代码。</param>
    public void SetScriptCode(string code) => _editorService?.SetText(code);

    /// <summary>获取当前脚本代码文本（未初始化时也可正确返回暂存代码）。</summary>
    public string GetCurrentCode() => _editorService?.GetText() ?? string.Empty;

    /// <summary>获取最近一次脚本执行的原始返回值，适用于外部获取脚本计算结果。</summary>
    public object? GetLastScriptResult() => _editorService?.LastScriptResult;

    /// <summary>设置脚本执行时的输入数据（作为 globals 注入脚本上下文，脚本中可直接访问）。</summary>
    public void SetInputData(object? data) => _editorService?.SetInputData(data);

    /// <summary>获取当前脚本的用户 DLL 引用路径列表。</summary>
    public IReadOnlyList<string> GetReferencePaths() =>
        _viewModel?.References.Select(r => r.FullPath).ToList() ?? (IReadOnlyList<string>)Array.Empty<string>();

    /// <summary>设置当前脚本的用户 DLL 引用路径。</summary>
    public void SetReferences(IEnumerable<string> paths)
    {
        if (_viewModel == null) return;
        _viewModel.References.Clear();
        foreach (var path in paths)
            _viewModel.AddReference(path);
    }

    /// <summary>获取当前编辑器完整状态（代码 + DLL 引用），用于宿主窗口关闭后回写持久化。</summary>
    public Models.ScriptEditorState Document => new()
    {
        Code = GetCurrentCode(),
        References = GetReferencePaths().ToList()
    };

    /// <summary>清空输出面板。</summary>
    public void ClearOutput()
    {
        if (_viewModel != null) _viewModel.OutputText = string.Empty;
        // 模板绑定为 OneWay，需显式清空当前脚本项
        if (ReferenceEquals(_attachedItem, DataContext))
            _attachedItem!.OutputText = string.Empty;
    }

    /// <summary>保存当前脚本到关联文件。尚未关联文件路径时弹出另存为对话框。</summary>
    public void Save() => TryExecuteCommand(_viewModel?.SaveCommand);

    /// <summary>弹出另存为对话框保存当前脚本。</summary>
    public void SaveAs() => TryExecuteCommand(_viewModel?.SaveAsCommand);

    /// <summary>同步触发脚本执行（命令模式，需等 RoslynPad 初始化完成）。</summary>
    public void Execute() => TryExecuteCommand(_viewModel?.RunCommand);

    /// <summary>异步执行脚本，返回一个在脚本执行完成时完成的 Task。</summary>
    public Task ExecuteAsync() => _viewModel?.RunCommand.ExecuteAsync(null) ?? Task.CompletedTask;
  

    /// <summary>终止当前执行的脚本。</summary>
    public void Terminate() => TryExecuteCommand(_viewModel?.TerminateCommand);

    /// <summary>同步触发循环执行。</summary>
    public void StartLoop() => TryExecuteCommand(_viewModel?.StartLoopCommand);

    /// <summary>异步循环执行脚本，返回一个在循环完成时完成的 Task。</summary>
    public Task StartLoopAsync()
    {
        if (_viewModel == null) return Task.CompletedTask;
        if (!Dispatcher.CheckAccess())
            return DispatchLoopToUiAsync();

        // 如果已在循环中，返回当前执行中的 Task
        if (_viewModel.IsLooping)
        {
            var task = _viewModel.StartLoopCommand.ExecutionTask;
            return task ?? Task.CompletedTask;
        }
 
        return _viewModel.StartLoopCommand.ExecuteAsync(null);
    }

    /// <summary>从非 UI 线程将 StartLoop 调度到 UI 线程。</summary>
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

    /// <summary>停止循环执行。</summary>
    public void StopLoop() => TryExecuteCommand(_viewModel?.StopLoopCommand);

    /// <summary>从文件路径加载脚本内容。</summary>
    /// <param name="filePath">脚本文件的绝对路径。</param>
    public void LoadFromFile(string filePath) => _viewModel?.LoadFromFile(filePath);

    /// <summary>添加 DLL 引用（传入绝对路径，保存到持久化文件）。</summary>
    /// <param name="path">DLL 文件的绝对路径。</param>
    public void AddReference(string path) => _viewModel?.AddReference(path);

    /// <summary>安全执行命令（检查 CanExecute 后再执行）。</summary>
    private static void TryExecuteCommand(ICommand? command)
    {
        if (command?.CanExecute(null) == true)
            command.Execute(null);
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 主动初始化编辑器。可在控件未加载到可视化树时调用。
    /// RoslynPad 的 InitializeAsync 需要控件最终在可视化树中才能工作，
    /// 如果初始化失败，当控件加载后 OnLoaded 会自动重试。
    /// </summary>
    /// <returns>初始化是否成功。</returns>
    public async Task<bool> InitializeAsync()
    {
        if (_isInitialized) return true;
        return await InitializeCoreAsync();
    }

    /// <summary>
    /// 延迟初始化后端（前后端分离核心）。
    /// 只在首次需要时解析 ScriptEditorService 单例并创建 ViewModel，
    /// 避免构造函数中同步阻塞 UI 线程。
    /// </summary>
    private void EnsureBackendInitialized()
    {
        if (_viewModel != null) return;

        var sw = Stopwatch.StartNew();
        Debug.WriteLine("[ScriptEditor] EnsureBackendInitialized START");

        _editorService = ContainerLocator.Container.Resolve<ScriptEditorService>();
        Debug.WriteLine($"[ScriptEditor] EnsureBackend ScriptEditorService: {sw.ElapsedMilliseconds}ms");

        // 应用 ctor 暂存的初始数据：SetInputData 仅依赖 Service 可立即应用；
        // SetReferences 依赖 ViewModel 必须在创建后调用。
        SetInputData(_initialInputData!);

        _viewModel = new ScriptEditorViewModel(_editorService);
        Debug.WriteLine($"[ScriptEditor] EnsureBackend ScriptEditorViewModel: {sw.ElapsedMilliseconds}ms");

        if (_initialDocument != null)
            SetReferences(_initialDocument.References);

        // 用当前脚本项的既有输出（如启动加载流程的无头执行结果）为 ViewModel 做种子，
        // 保证打开界面即可看到历史输出，且编辑器路径的后续输出在此基础上衔接。
        // 注意：只写数据源侧，依赖属性由模板单向绑定自动跟随；播种期间抑制自动滚动。
        if (_attachedItem != null)
        {
            _suppressAutoScroll = true;
            _viewModel.OutputText = _attachedItem.OutputText;
            _suppressAutoScroll = false;

            // 播种断点：首次创建 ViewModel 时从 ScriptItem 恢复断点
            _suppressBreakpointSync = true;
            foreach (var bp in _attachedItem.Breakpoints)
                _viewModel.Breakpoints.Add(bp);
            _suppressBreakpointSync = false;
        }

        LayoutRoot.DataContext = _viewModel;

        // InputBindings 不属于视觉树，XAML 中 ElementName 绑定无法解析（F5 等快捷键失效），
        // 改为在此处用代码显式绑定 ViewModel 命令。
        SetupKeyBindings(_viewModel);

        // 前后端分离：工具栏和引用面板不在 XAML 中同步加载，在此处动态创建。
        // 显式设置 DataContext，避免依赖 DataContext 继承时序导致 RunCommand 等命令绑定失败。
        var toolbar = new ScriptEditorToolbar
        {
            DataContext = _viewModel
        };
        ToolbarHost.Content = toolbar;
        Debug.WriteLine($"[ScriptEditor] EnsureBackend CreateToolbar: {sw.ElapsedMilliseconds}ms");

        var refPanel = new ScriptEditorReferencePanel
        {
            Visibility = System.Windows.Visibility.Collapsed,
            DataContext = _viewModel
        };
        // 通过绑定控制引用面板可见性（与 XAML 中原来的 Visibility 绑定一致）
        var visibilityBinding = new System.Windows.Data.Binding(nameof(ScriptEditorViewModel.IsReferencePanelOpen))
        {
            Source = _viewModel,
            Converter = new System.Windows.Controls.BooleanToVisibilityConverter()
        };
        refPanel.SetBinding(System.Windows.UIElement.VisibilityProperty, visibilityBinding);
        ReferencePanelHost.Content = refPanel;
        Debug.WriteLine($"[ScriptEditor] EnsureBackend CreateReferencePanel: {sw.ElapsedMilliseconds}ms");

        // 同步 ViewModel <-> View 的循环状态 + 输出文本
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.IsLooping))
                IsLooping = _viewModel.IsLooping;
            else if (e.PropertyName == nameof(_viewModel.LoopInterval))
                LoopInterval = _viewModel.LoopInterval;
            else if (e.PropertyName == nameof(_viewModel.OutputText))
            {
                // 关键:TextBox 模板绑定的是 UserControl.OutputText DP(不是 VM.OutputText),
                // 必须把 VM 输出镜像到该 DP 才能显示;否则 TextBox 永远是空。
                // 注意用 _suppressOutputChange 防止 OnOutputTextChanged 反向再写 VM 形成回环。
                _suppressOutputChange = true;
                try { OutputText = _viewModel.OutputText; }
                finally { _suppressOutputChange = false; }

                // ScriptManager 标签页场景:同时镜像到 _attachedItem,保证切页后输出不丢。
                // ScriptNode 宿主窗口场景:_attachedItem 为 null,跳过此步即可。
                // 用 ReferenceEquals 守卫避免切页瞬间旧 VM 输出串到新脚本项。
                var item = _attachedItem;
                if (item != null && ReferenceEquals(item, DataContext))
                    item.OutputText = _viewModel.OutputText;

                // 防抖:每 50ms 最多滚动一次,避免大量输出时频繁滚动卡顿;
                // 切页播种期间抑制滚动,防止新脚本被强制滚到底部
                var now = DateTime.Now;
                if (!_suppressAutoScroll && (now - _lastAutoScrollTime).TotalMilliseconds >= 50)
                {
                    _lastAutoScrollTime = now;
                    var scrollItemTitle = item?.Title;
                    OutputTextBox.Dispatcher.InvokeAsync(() =>
                    {
                        // 跨线程排队后 _attachedItem 可能已被切到新脚本,
                        // 此处不再校验 item(其可能为 null),只滚动当前 TextBox 即可
                        Debug.WriteLine($"[ScriptEditor] AutoScrollToEnd fired for '{scrollItemTitle ?? "(no item)"}', current='{(_attachedItem?.Title ?? "null")}'");
                        OutputTextBox.CaretIndex = OutputTextBox.Text.Length;
                        OutputTextBox.ScrollToEnd();
                    });
                }
            }
        };

        Debug.WriteLine($"[ScriptEditor] EnsureBackend TOTAL: {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// 用代码显式绑定快捷键到 ViewModel 命令。
    /// InputBinding 不属于逻辑/视觉树，XAML 中 ElementName/RelativeSource 绑定无法解析，
    /// 会导致 F5 等快捷键静默失效。
    /// </summary>
    private void SetupKeyBindings(ScriptEditorViewModel vm)
    {
        InputBindings.Clear();
        AddKeyBinding(vm.NewDocumentCommand, Key.N, ModifierKeys.Control);
        AddKeyBinding(vm.OpenDocumentCommand, Key.O, ModifierKeys.Control);
        AddKeyBinding(vm.SaveCommand, Key.S, ModifierKeys.Control);
        AddKeyBinding(vm.SaveAsCommand, Key.S, ModifierKeys.Control | ModifierKeys.Shift);
        AddKeyBinding(vm.CompileCommand, Key.B, ModifierKeys.Control);
        AddKeyBinding(vm.RunCommand, Key.F5, ModifierKeys.None);
        AddKeyBinding(vm.TerminateCommand, Key.F5, ModifierKeys.Shift);
        AddKeyBinding(vm.FormatDocumentCommand, Key.D, ModifierKeys.Control);
        AddKeyBinding(vm.CommentSelectionCommand, Key.K, ModifierKeys.Control);
        AddKeyBinding(vm.UncommentSelectionCommand, Key.U, ModifierKeys.Control);
        AddKeyBinding(vm.RenameSymbolCommand, Key.F2, ModifierKeys.None);
    }

    private void AddKeyBinding(ICommand command, Key key, ModifierKeys modifiers)
    {
        InputBindings.Add(new KeyBinding(command, new KeyGesture(key, modifiers)));
    }

    /// <summary>
    /// 动态创建重量级编辑器控件（前后端分离核心）。
    /// XAML 只加载轻量 Shell（工具栏+输出面板），RoslynCodeEditorView 和 BreakpointGutter
    /// 在此方法中按需创建，避免 InitializeComponent() 阻塞 UI 线程。
    /// </summary>
    private void CreateEditorControls()
    {
        if (scriptEditor != null) return; // 已创建

        var createSw = Stopwatch.StartNew();

        // 创建 RoslynPad 代码编辑器（继承 AvalonEdit.TextEditor，重量级 WPF 控件）
        scriptEditor = new RoslynCodeEditorView
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetColumn(scriptEditor, 1);
        editorRoot.Children.Add(scriptEditor);

        // 创建 VS 风格断点边栏
        breakpointGutter = new BreakpointGutter
        {
            Width = 20,
            Background = (Brush)FindResource("Color_Gray5"),
            DotRadius = 7
        };
        Grid.SetColumn(breakpointGutter, 0);
        editorRoot.Children.Add(breakpointGutter);

        Debug.WriteLine($"[ScriptEditor] CreateEditorControls: {createSw.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// 初始化核心逻辑（从 OnLoaded 提取，可独立调用）。
    /// </summary>
    private async Task<bool> InitializeCoreAsync()
    {
        var totalSw = Stopwatch.StartNew();
        Debug.WriteLine("[ScriptEditor] InitializeCoreAsync START");

        // 前后端分离：延迟初始化 Service + ViewModel（首次调用时执行）
        EnsureBackendInitialized();
        if (_roslynPadInitialized)
        {
            Debug.WriteLine("[ScriptEditor] InitializeCoreAsync: RoslynPad already initialized, swapping content");
            SaveEditorScrollState();
            SaveOutputScrollState();
            if (_attachedItem != null)
            {
                _editorService!.SetText(_attachedItem.Code);
                _editorService!.AttachedItem = _attachedItem;
                // 仅同步 ViewModel；依赖属性由模板单向绑定自动跟随脚本项，
                // 直接 SetValue 会破坏绑定导致后续输出不再刷新
                _suppressAutoScroll = true;
                _viewModel!.OutputText = _attachedItem.OutputText;
                _suppressAutoScroll = false;
                RestoreEditorScrollState(_attachedItem);
                RestoreOutputScrollState(_attachedItem);
            }
            _isInitialized = true;
            _initTcs?.TrySetResult(true);
            CloseLoadingWindow();
            Debug.WriteLine($"[ScriptEditor] InitializeCoreAsync (swap) TOTAL: {totalSw.ElapsedMilliseconds}ms");
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
        {
            return false;
        }

        try
        {
            var workingDir = System.IO.Path.GetDirectoryName(
                typeof(ScriptEditorService).Assembly.Location) ?? ".";

            // 优先级：ctor 暂存的初始代码（ScriptNode 宿主窗口场景，回填上次保存的内容）>
            //         _attachedItem.Code（ScriptManager 标签页场景）>
            //         默认模板
            var template = !string.IsNullOrEmpty(_initialDocument?.Code)
                ? _initialDocument.Code
                : !string.IsNullOrEmpty(_attachedItem?.Code)
                    ? _attachedItem.Code
                    : _editorService!.GetDefaultTemplate();
            // 模板已应用,清空暂存,避免后续路径误用
            _initialDocument = null;

            // 同步 _pendingText 为当前 Item 的正确代码，防止共享 Service 中残留的旧
            // _pendingText 在 Initialize 中覆盖 InitializeAsync 刚刚设置的正确文本
            _editorService.SetText(template);

            // 前后端分离：动态创建重量级编辑器控件（不在 XAML 中同步加载）
            CreateEditorControls();

            DocumentId documentId;
            var sw = Stopwatch.StartNew();
            documentId = await scriptEditor!.InitializeAsync(
                roslynHost,
                SharedHighlightColors,
                workingDir,
                template,
                SourceCodeKind.Script);
            Debug.WriteLine($"[ScriptEditor] InitializeAsync: {sw.ElapsedMilliseconds}ms");

            _roslynPadInitialized = true;

            sw.Restart();
            _editorService!.Initialize(scriptEditor!, roslynHost, documentId);
            Debug.WriteLine($"[ScriptEditor] Service.Initialize: {sw.ElapsedMilliseconds}ms");

            sw.Restart();
            // 替换 CompletionProvider 为自定义实现：
            // 1) 修复预处理器指令（#Region 等）的文本替换 Bug；
            // 2) 仅在输入以 # 开头时才提示预处理器指令，避免输入 if 等字母时出现 #if
            if (scriptEditor!.CompletionProvider is RoslynCodeEditorCompletionProvider roslynProvider)
            {
                scriptEditor.CompletionProvider = CreateFixedCompletionProvider(roslynProvider);
            }
            else if (scriptEditor.CompletionProvider == null)
            {
                // 如果 CompletionProvider 为 null，等待初始化完成后再替换
                scriptEditor.Loaded += (_, __) =>
                {
                    if (scriptEditor.CompletionProvider is RoslynCodeEditorCompletionProvider provider)
                        scriptEditor.CompletionProvider = CreateFixedCompletionProvider(provider);
                };
            }

            _viewModel!.OnEditorInitialized();

            // 编辑器文本变化同步到 ScriptCode 依赖属性，并实时写回 ScriptItem
            scriptEditor.Document.TextChanged += (_, _) =>
            {
                _suppressScriptCodeChange = true;
                var code = _editorService!.GetText();
                ScriptCode = code;
                _suppressScriptCodeChange = false;

                // 实时写回当前 ScriptItem，确保 Tab 切换时数据始终最新；
                // 守卫防止切页瞬间旧脚本文本写入新脚本项
                if (_attachedItem != null && ReferenceEquals(_attachedItem, DataContext))
                {
                    _attachedItem.Code = code;
                    _attachedItem.OutputText = _viewModel!.OutputText;
                }
            };

            // 首次初始化后，将模板代码立即写回 ScriptItem（此时 TextChanged 尚未触发）
            if (_attachedItem != null)
            {
                _attachedItem.Code = _editorService!.GetText();
            }

            // 光标位置变更：更新状态栏显示 + 实时保存到当前脚本项（行号按脚本独立记忆）
            scriptEditor.TextArea.Caret.PositionChanged += (_, _) =>
            {
                _viewModel!.OnCaretPositionChanged(
                    scriptEditor.TextArea.Caret.Line,
                    scriptEditor.TextArea.Caret.Column);

                if (_attachedItem != null && ReferenceEquals(_attachedItem, DataContext))
                    _attachedItem.CaretOffset = scriptEditor.TextArea.Caret.Offset;
            };

            // 编辑器滚动位置变更：实时保存到当前脚本项。
            // 必须用"实时记录"而非切页瞬间保存——DataContextChanged 触发时绑定已完成
            // 内容替换，且守卫恒为 false，切页时保存永远拿不到旧值
            scriptEditor.TextArea.TextView.ScrollOffsetChanged += (_, _) =>
            {
                if (_attachedItem != null && ReferenceEquals(_attachedItem, DataContext))
                    _attachedItem.EditorScrollOffset = scriptEditor.TextArea.TextView.VerticalOffset;
            };

            // 首次初始化完成：恢复该脚本项上次的光标位置与代码滚动位置
            if (_attachedItem != null)
            {
                var codeLength = _attachedItem.Code?.Length ?? 0;
                scriptEditor.TextArea.Caret.Offset = Math.Min(_attachedItem.CaretOffset, codeLength);
                RestoreEditorScrollState(_attachedItem);
                // 恢复该脚本项自己的输出滚动位置
                RestoreOutputScrollState(_attachedItem);
            }

            // Ctrl+滚轮缩放字体
            scriptEditor.PreviewMouseWheel += OnEditorMouseWheel;
            Debug.WriteLine($"[ScriptEditor] CompletionProvider + Events: {sw.ElapsedMilliseconds}ms");

            _isInitialized = true;
            _initTcs?.TrySetResult(true);

            CloseLoadingWindow();

            sw.Restart();
            // 绑定 VS 风格断点边栏
            SetupBreakpointGutter();
            Debug.WriteLine($"[ScriptEditor] SetupBreakpointGutter: {sw.ElapsedMilliseconds}ms");

            // 后台预编译加速首次执行
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

    /// <summary>
    /// 创建包装后的补全提供者。
    /// textProvider 提供编辑器实时文本，用于判定光标是否处于 # 指令输入上下文。
    /// </summary>
    private FixedCodeEditorCompletionProvider CreateFixedCompletionProvider(RoslynCodeEditorCompletionProvider original)
    {
        return new FixedCodeEditorCompletionProvider(original, () => scriptEditor?.Document?.Text ?? string.Empty);
    }

    /// <summary>通知当前关联的 ScriptItem 关闭 FileLoading 窗口。</summary>
    private void CloseLoadingWindow()
    {
        if (_attachedItem != null)
        {
            Debug.WriteLine($"[ScriptEditor] Setting IsEditorInitialized=true for '{_attachedItem.Title}'");
            _attachedItem.IsEditorInitialized = true;
        }
        else
        {
            Debug.WriteLine("[ScriptEditor] WARNING: _attachedItem is null, cannot close FileLoading");
        }
    }

    /// <summary>
    /// 控件加载完成后：绑定脚本项数据，并按需延迟初始化 RoslynPad 编辑器。
    /// </summary>
    /// <remarks>
    /// <para>TabControl 会一次性 realized 所有标签内容（实例化全部 ScriptEditorView），
    /// 因此不能在这里立即初始化 RoslynPad——否则打开脚本管理器时仍会一次性构建
    /// 全部重量级编辑器。改为：仅当所属 TabItem 被选中时初始化（真实懒加载）。</para>
    /// <para>独立使用（对话框等，无 TabItem 祖先）时立即初始化，行为与原逻辑一致。</para>
    /// </remarks>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_onLoadedHandled) return;
        _onLoadedHandled = true;

        Debug.WriteLine($"[ScriptEditor] OnLoaded fired, DataContext={DataContext?.GetType().Name}, _isInitialized={_isInitialized}");

        // 绑定到 ScriptItem（VM 层数据）：载入 DLL 引用，供引用面板展示并保持
        // 与脚本项（无头引擎/持久化）一致。
        if (DataContext is ScriptItem item)
        {
            Debug.WriteLine($"[ScriptEditor] OnLoaded: AttachToItem, item.Title={item.Title}");
            AttachToItem(item);
        }
        else
        {
            Debug.WriteLine($"[ScriptEditor] OnLoaded: DataContext is NOT ScriptItem, it's {DataContext?.GetType().FullName ?? "null"}");
        }

        Debug.WriteLine("[ScriptEditor] OnLoaded: about to start deferred initialization");
        EnsureDeferredInitialization();
    }

    /// <summary>
    /// 读取代码编辑器的垂直滚动偏移（像素）。
    /// </summary>
    private static double GetEditorVerticalOffset(RoslynCodeEditorView? editor)
        => editor?.TextArea?.TextView?.VerticalOffset ?? 0;

    /// <summary>
    /// 设置代码编辑器的垂直滚动偏移（像素）。
    /// AvalonEdit 的 TextView 以显式接口实现 IScrollInfo，需转型调用。
    /// </summary>
    private static void SetEditorVerticalOffset(RoslynCodeEditorView? editor, double offset)
    {
        var textView = editor?.TextArea?.TextView;
        if (textView == null) return;
        if (textView is System.Windows.Controls.Primitives.IScrollInfo scrollInfo)
            scrollInfo.SetVerticalOffset(offset);
    }

    /// <summary>
    /// 保存当前脚本项的代码编辑器垂直滚动位置（切页前调用）。
    /// </summary>
    private void SaveEditorScrollState()
    {
        if (_attachedItem == null || scriptEditor == null) return;
        // 守卫：仅当视图当前显示的正是 _attachedItem 时才保存，防止切页竞态串写
        if (!ReferenceEquals(_attachedItem, DataContext)) return;
        _attachedItem.EditorScrollOffset = GetEditorVerticalOffset(scriptEditor);
    }

    /// <summary>
    /// 恢复指定脚本项的代码编辑器滚动位置。立即执行一次 + 布局完成后校正，
    /// 对抗 AvalonEdit 替换文档后的异步重排版对偏移的覆盖。
    /// </summary>
    private void RestoreEditorScrollState(ScriptItem item)
    {
        if (scriptEditor == null) return;
        var targetOffset = item.EditorScrollOffset;
        SetEditorVerticalOffset(scriptEditor, targetOffset);
        Dispatcher.BeginInvoke(new Action(() =>
        {
            // 守卫：延迟回调执行时可能已再次切页
            if (ReferenceEquals(_attachedItem, item))
                SetEditorVerticalOffset(scriptEditor, targetOffset);
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// 保存当前脚本项的输出面板垂直滚动位置（切页前调用）。
    /// </summary>
    private void SaveOutputScrollState()
    {
        if (_attachedItem == null || OutputTextBox == null) return;

        // 关键守卫：仅当视图当前显示的正是 _attachedItem 时才保存。
        // 否则（如 swap 路径中 _attachedItem 已切到新脚本、而视口仍是旧内容）
        // 会把继承自上一脚本的偏移写进新脚本项，导致恢复时"精确复现"错误位置。
        if (!ReferenceEquals(_attachedItem, DataContext)) return;

        _attachedItem.ScrollOffset = OutputTextBox.VerticalOffset;
        Debug.WriteLine($"[ScriptEditor] SaveScroll '{_attachedItem.Title}' -> {_attachedItem.ScrollOffset}");
    }

    /// <summary>
    /// 恢复指定脚本项的输出面板滚动位置。立即执行一次 + 布局完成后再次校正，
    /// 消除文本替换时 WPF 保留旧偏移导致的时序竞争；期间抑制自动滚动。
    /// </summary>
    private void RestoreOutputScrollState(ScriptItem item)
    {
        var targetOffset = item.ScrollOffset;
        _suppressAutoScroll = true;
        Debug.WriteLine($"[ScriptEditor] RestoreScroll '{item.Title}' target={targetOffset}");

        // 立即恢复：切断对上一脚本滚动状态的视觉继承
        OutputTextBox.ScrollToVerticalOffset(targetOffset);

        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                // 守卫：延迟回调执行时可能已再次切页
                if (ReferenceEquals(_attachedItem, item))
                {
                    OutputTextBox.ScrollToVerticalOffset(targetOffset);
                    Debug.WriteLine($"[ScriptEditor] RestoreScroll(delayed) '{item.Title}' applied={targetOffset} actual={OutputTextBox.VerticalOffset}");
                }
            }
            finally
            {
                _suppressAutoScroll = false;
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// 将本编辑器与脚本项关联：引用面板加载脚本项的 DLL 引用，并让服务把
    /// 后续引用增删同步回脚本项（保证"运行全部/保存任务"使用同一份引用）。
    /// 同时刷新输出面板，确保切换脚本时输出栏显示正确的脚本输出。
    /// 断点也在此处按脚本独立保存/恢复（与代码、光标、滚动位置同理）。
    /// </summary>
    private void AttachToItem(ScriptItem item)
    {
        if (_attachedItem == item) return;

        // 切页前：将当前 ViewModel 断点保存到旧脚本项
        if (_attachedItem != null && _viewModel != null)
        {
            _attachedItem.Breakpoints.Clear();
            foreach (var bp in _viewModel.Breakpoints)
                _attachedItem.Breakpoints.Add(bp);
        }

        _attachedItem = item;

        // 后端可能尚未初始化（OnLoaded 在 InitializeCoreAsync 之前触发），
        // 暂存关联关系，待 EnsureBackendInitialized 时再同步 AttachedItem 和输出文本
        if (_editorService != null)
            _editorService.AttachedItem = item;

        SetReferences(item.ReferencePaths);

        if (_viewModel != null)
        {
            // 切页播种：抑制自动滚动，避免新脚本的输出被强制滚到底部
            _suppressAutoScroll = true;
            _viewModel.OutputText = item.OutputText;
            _suppressAutoScroll = false;

            // 恢复新脚本项的断点（抑制同步回调，避免回环写入）
            _suppressBreakpointSync = true;
            _viewModel.Breakpoints.Clear();
            foreach (var bp in item.Breakpoints)
                _viewModel.Breakpoints.Add(bp);
            _suppressBreakpointSync = false;
        }
    }

    /// <summary>
    /// 决定何时初始化：所属 TabItem 未选中时挂接选中事件，选中后才初始化；
    /// 无 TabItem 祖先（对话框等独立场景）时立即初始化。
    /// </summary>
    private void EnsureDeferredInitialization()
    {
        if (_initDeferralHooked) return;
        _initDeferralHooked = true;

        // TabItem 是逻辑容器，不在视觉树中，需通过 TabControl.ItemContainerGenerator 查找
        var tab = FindVisualAncestor<TabControl>(this)?.ItemContainerGenerator.ContainerFromItem(DataContext) as TabItem;
        if (tab == null)
        {
            Debug.WriteLine("[ScriptEditor] EnsureDeferredInit: no TabItem container, initializing immediately");
            InitializeWhenSelected();
            return;
        }

        Debug.WriteLine($"[ScriptEditor] EnsureDeferredInit: found TabItem, IsSelected={tab.IsSelected}");
        if (tab.IsSelected)
        {
            InitializeWhenSelected();
            return;
        }

        Debug.WriteLine("[ScriptEditor] EnsureDeferredInit: tab not selected, hooking IsSelected change");
        DependencyPropertyDescriptor.FromProperty(TabItem.IsSelectedProperty, typeof(TabItem))
            .AddValueChanged(tab, OnTabSelectionChanged);
    }

    #endregion

    private void OnTabSelectionChanged(object? sender, EventArgs e)
    {
        var tab = FindVisualAncestor<TabControl>(this)?.ItemContainerGenerator.ContainerFromItem(DataContext) as TabItem;
        if (tab == null || !tab.IsSelected) return;

        DependencyPropertyDescriptor.FromProperty(TabItem.IsSelectedProperty, typeof(TabItem))
            .RemoveValueChanged(tab, OnTabSelectionChanged);

        InitializeWhenSelected();
    }

    /// <summary>所属标签被选中（或独立场景）时：恢复布局并初始化 RoslynPad。</summary>
    private async void InitializeWhenSelected()
    {
        if (_deferLayout)
        {
            _deferLayout = false;
            InvalidateMeasure();
        }

        if (!_isInitialized)
            await InitializeCoreAsync();

        // 强制完成 WPF 布局渲染管线，测量 AvalonEdit 渲染耗时
        if (_isInitialized)
        {
            var layoutSw = System.Diagnostics.Stopwatch.StartNew();
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            System.Diagnostics.Debug.WriteLine($"[ScriptEditor] Layout/Render flush: {layoutSw.ElapsedMilliseconds}ms");
        }
    }

    private static T? FindVisualAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T found) return found;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    /// <summary>
    /// 延迟布局：TabControl 一次性 realized 大量 ScriptEditorView 时，
    /// 跳过 Measurement（不触发 RoslynPad 控件模板加载），
    /// 直到所属标签页被选中后才恢复正常布局。
    /// </summary>
    protected override Size MeasureOverride(Size constraint)
    {
        if (_deferLayout)
            return Size.Empty;
        return base.MeasureOverride(constraint);
    }

    private void OnEditorMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (scriptEditor == null) return;
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            var fontSize = scriptEditor.FontSize + (e.Delta > 0 ? 1 : -1);
            fontSize = Math.Max(8, Math.Min(72, fontSize));
            scriptEditor.FontSize = fontSize;
            e.Handled = true;
        }
    }

    /// <summary>
    /// 绑定 VS 风格断点边栏：关联断点集合、编辑器 TextView、点击事件。
    /// 同时订阅断点集合变更，实时同步到当前 ScriptItem（供"全部执行"使用）。
    /// </summary>
    private void SetupBreakpointGutter()
    {
        if (breakpointGutter == null || scriptEditor == null) return;
        var textView = scriptEditor.TextArea.TextView;

        breakpointGutter.Attach(_viewModel!.Breakpoints, textView);
        breakpointGutter.LineClicked += (_, line) =>
        {
            _viewModel.ToggleBreakpoint(line);
            textView.InvalidateVisual();
        };

        // 订阅断点集合变更：实时同步到当前 ScriptItem，
        // 使 ScriptItem.Engine（全部执行路径）能读取到正确的断点
        _viewModel.Breakpoints.CollectionChanged += OnBreakpointsChanged;

        // 通过 IsDebugMode 控制边栏可见性
        breakpointGutter.SetBinding(VisibilityProperty,
            new System.Windows.Data.Binding(nameof(ScriptEditorViewModel.IsDebugMode))
            {
                Source = _viewModel,
                Converter = new System.Windows.Controls.BooleanToVisibilityConverter()
            });
    }

    /// <summary>
    /// 断点集合变更回调：将 ViewModel 断点同步到当前 ScriptItem。
    /// 切页期间的批量保存/恢复由 _suppressBreakpointSync 抑制。
    /// </summary>
    private void OnBreakpointsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_suppressBreakpointSync || _attachedItem == null || _viewModel == null) return;
        if (!ReferenceEquals(_attachedItem, DataContext)) return;

        _attachedItem.Breakpoints.Clear();
        foreach (var bp in _viewModel.Breakpoints)
            _attachedItem.Breakpoints.Add(bp);
    }
}