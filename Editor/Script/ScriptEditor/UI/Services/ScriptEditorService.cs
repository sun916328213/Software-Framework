using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text;
using System.Windows.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using ICSharpCode.AvalonEdit.Document;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Scripting;
using RoslynPad.Editor;
using RoslynPad.Roslyn;
using ScriptEditor.Models;
using Extension.Core.ConvertEx;

namespace ScriptEditor.Services;

/// <summary>
/// 脚本编辑器核心服务，负责 Roslyn 引擎生命周期、编辑器操作、脚本执行/编译、
/// 文件操作、引用管理等全部业务逻辑。
/// </summary>
/// <remarks>
/// <para>核心职责：</para>
/// <list type="bullet">
///   <item>管理共享的 CustomRoslynHost 实例</item>
///   <item>DLL 引用的增删改查</item>
///   <item>编辑器文本操作、格式化、注释、重命名</item>
///   <item>脚本编译、执行、循环执行、终止（委托给无头 <see cref="ScriptEngine"/>）</item>
///   <item>输出缓冲与实时刷新</item>
///   <item>文件新建/打开/保存/另存为</item>
/// </list>
/// <para>运行时状态委托给 <see cref="ScriptEditorModel"/>。</para>
/// <para>架构：按职责拆分为 partial 模块 ——</para>
/// <list type="bullet">
///   <item><c>ScriptEditorService.cs</c>：核心状态、事件中继、编辑器生命周期、执行引擎</item>
///   <item><c>ScriptEditorService.References.cs</c>：引用管理与 Roslyn 工作区同步</item>
///   <item><c>ScriptEditorService.Files.cs</c>：文件操作与脚本模板</item>
/// </list>
/// </remarks>
public partial class ScriptEditorService : ObservableObject
{
    #region 事件

    /// <summary>综合状态变更通知（IsRunning / IsLooping / StatusText 等任意状态变化时触发）。</summary>
    public event Action? StateChanged;

    /// <summary>输出文本变更通知。</summary>
    public event Action? OutputTextChanged;

    #endregion

    // ════════════════════════════════════════════════════════════════
    //  原有：RoslynHost / 编辑器 / 引用管理
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 无头执行引擎（前端/后端分离）：编译、运行、循环、终止逻辑全部委托给它。
    /// 本服务仅负责编辑器（RoslynPad）关联、引用管理、文件操作与 UI 线程封送。
    /// </summary>
    private ScriptEngine _engine = null!;

    private readonly HashSet<string> _localInjectedReferencePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly FileService _fileService;
    private readonly ScriptEditorModel _model;
    private static readonly SymbolRenameOptions RenameOptions = new();

    /// <summary>
    /// 当前关联的脚本项（ScriptManager 标签页场景）。非 null 时，引用增删改会
    /// 同步回 ScriptItem（其无头引擎/持久化读取同一份引用），保证编辑器面板
    /// 修改的 DLL 引用在"运行全部/重启加载"时依然生效。
    /// </summary>
    private Models.ScriptItem? _attachedItem;
    internal ScriptItem? AttachedItem
    {
        get => _attachedItem;
        set
        {
            _attachedItem = value;
            OnPropertyChanged(nameof(AttachedItem));
        }
    }

    private void SyncAttachedItemReferences()
    {
        if (AttachedItem == null) return;
        AttachedItem.SetReferences(_model.AdditionalReferencePaths);
    }

    private RoslynCodeEditor? _editor;
    private RoslynHost? _host;
    private DocumentId? _documentId;
    private string? _pendingText;

    /// <summary>编辑器文本变更处理程序（保存为字段以便重复 Initialize 时先退订，防止订阅叠加）。</summary>
    private EventHandler? _editorTextChanged;

    /// <summary>
    /// 创建脚本编辑器服务。
    /// </summary>
    /// <param name="fileService">文件服务，缺省时新建默认实例。传入 null 则不依赖 Prism 容器。</param>
    public ScriptEditorService(FileService? fileService = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _model = new ScriptEditorModel();
        _fileService = fileService ?? new FileService();
        System.Diagnostics.Debug.WriteLine($"[ScriptEditorService] Ctor model+fileService: {sw.ElapsedMilliseconds}ms");

        // 中继 Model 事件（跨线程时封送到 UI 线程，已在 UI 线程则同步通知避免绑定延迟）
        // 注：Model 与本 Service 同生命周期（构造函数内创建），订阅无泄漏风险
        _model.PropertyChanged += OnModelPropertyChanged;
        _model.StateChanged += OnModelStateChanged;

        System.Diagnostics.Debug.WriteLine($"[ScriptEditorService] Ctor events: {sw.ElapsedMilliseconds}ms");

        try
        {
            // 后端：共享 CustomRoslynHost + 无头执行引擎（状态事件封送到 UI 线程）
            // ScriptHostProvider.Shared 应在 App 预热阶段已由后台线程完成初始化，
            // 此处访问 Lazy.Value 应瞬时返回（若未预热则 UI 线程阻塞 ~500ms）。
            var hostSw = System.Diagnostics.Stopwatch.StartNew();
            _model.RoslynHost = ScriptHostProvider.Shared;
            System.Diagnostics.Debug.WriteLine($"[ScriptEditorService] Ctor ScriptHostProvider.Shared: {hostSw.ElapsedMilliseconds}ms");

            _engine = new ScriptEngine(GetText, () => _model.AdditionalReferences, () => _model.Breakpoints);
            _engine.PropertyChanged += OnEnginePropertyChanged;
            System.Diagnostics.Debug.WriteLine($"[ScriptEditorService] Ctor ScriptEngine: {hostSw.ElapsedMilliseconds}ms");

            // 初始化执行相关状态（输出冲刷定时器，冲刷 ScriptEngine 的输出队列到 UI）
            // 必须在 _engine 创建之后且在 try 块内初始化，避免 _engine 为 null 时访问
            _outputFlushTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(100),
                DispatcherPriority.Background,
                (_, _) => _engine.FlushOutput(),
                System.Windows.Application.Current.Dispatcher)
            { IsEnabled = false };
        }
        catch (Exception ex)
        {
            _model.InitError = ex;
        }

        System.Diagnostics.Debug.WriteLine($"[ScriptEditorService] Ctor TOTAL: {sw.ElapsedMilliseconds}ms");
    }

    // ════════════════════════════════════════════════════════════════
    //  Model 暴露（供 ViewModel 直接绑定，消除代理层）
    // ════════════════════════════════════════════════════════════════

    /// <summary>获取运行时状态模型（供 ViewModel 直接绑定）。</summary>
    public ScriptEditorModel Model => _model;

    /// <summary>用户添加的 DLL 引用路径列表（只读）。</summary>
    public IReadOnlyList<string> AdditionalReferencePaths => _model.AdditionalReferencePaths;

    /// <summary>当前 Roslyn 宿主实例（CustomRoslynHost）。</summary>
    public CustomRoslynHost? RoslynHost => _model.RoslynHost as CustomRoslynHost;

    /// <summary>初始化阶段的异常（null 表示成功）。</summary>
    public Exception? InitError => _model.InitError;

    /// <summary>编辑器是否已完成初始化（View 已关联）。</summary>
    public bool IsInitialized => _editor != null && _host != null && _documentId != null;

    /// <summary>最近一次脚本执行的原始返回值。</summary>
    public object? LastScriptResult => _engine.LastScriptResult;

    /// <summary>
    /// 最近一次脚本执行的输出文本（引擎直读，无 UI 封送延迟）。
    /// RunAsync 返回后引擎已在 finally 中同步 FlushOutput，此值即时完整；
    /// Model.OutputText 的镜像经 Dispatcher.InvokeAsync 封送，可能尚未执行，
    /// 无 UI 参与的调用方（如 ScriptNode 流程执行）必须读此属性。
    /// </summary>
    public string EngineOutputText => _engine.OutputText;

    // 内部使用的便利属性（LoopInterval 需要同步到 Engine）
    internal int LoopInterval
    {
        get => _model.LoopInterval;
        set { _model.LoopInterval = value; _engine.LoopInterval = value; }
    }

    // ════════════════════════════════════════════════════════════════
    //  原有：编辑器生命周期
    // ════════════════════════════════════════════════════════════════

    /// <summary>Model 属性变更 → 跨线程封送后中继为 Service 属性变更。</summary>
    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.InvokeAsync(() =>
            {
                OnPropertyChanged(e.PropertyName);
                if (e.PropertyName == nameof(ScriptEditorModel.OutputText))
                    OutputTextChanged?.Invoke();
            }, DispatcherPriority.Background);
        }
        else
        {
            OnPropertyChanged(e.PropertyName);
            if (e.PropertyName == nameof(ScriptEditorModel.OutputText))
                OutputTextChanged?.Invoke();
        }
    }

    /// <summary>Model 综合状态变更 → 跨线程封送后转发。</summary>
    private void OnModelStateChanged()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
            dispatcher.InvokeAsync(() => StateChanged?.Invoke(), DispatcherPriority.Background);
        else
            StateChanged?.Invoke();
    }

    /// <summary>
    /// 关联 RoslynCodeEditor 实例。由 View 层在 OnLoaded 中调用。
    /// </summary>
    public void Initialize(RoslynCodeEditor editor, RoslynHost host, DocumentId documentId)
    {
        // 防止重复 Initialize（View 多次 Loaded）时订阅叠加：先退订旧编辑器上的处理程序
        if (_editor != null && _editorTextChanged != null)
            _editor.Document.TextChanged -= _editorTextChanged;

        _editor = editor;
        _host = host;
        _documentId = documentId;

        _editorTextChanged = OnEditorTextChanged;
        editor.Document.TextChanged += _editorTextChanged;

        if (_pendingText != null)
            editor.Text = _pendingText;
        else
            _pendingText = editor.Text;
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_editor == null) return;
        _pendingText = _editor.Text;
        _model.HasModified = true;
    }

    /// <summary>获取编辑器当前完整文本（线程安全，始终返回缓存文本）。</summary>
    public string GetText() => _pendingText ?? string.Empty;

    /// <summary>设置编辑器文本内容（未初始化时暂存，初始化后自动应用）。</summary>
    public void SetText(string text)
    {
        _pendingText = text;
        if (_editor != null)
            _editor.Text = text;
    }

    /// <summary>获取编辑器当前选中文本。</summary>
    public string GetSelectedText()
    {
        if (_editor == null) return string.Empty;
        var textArea = _editor.TextArea;
        if (textArea == null || textArea.Selection.IsEmpty) return string.Empty;
        return textArea.Selection.GetText();
    }

    /// <summary>获取光标行列位置（1-based）。</summary>
    public (int Line, int Column) GetCaretPosition()
    {
        if (_editor == null) return (1, 1);
        var caret = _editor.TextArea.Caret;
        return (caret.Line, caret.Column);
    }

    // ════════════════════════════════════════════════════════════════
    //  原有：编辑器操作
    // ════════════════════════════════════════════════════════════════

    public void RefreshDiagnostics()
    {
        if (!IsInitialized || _editor == null) return;
        try
        {
            var text = _editor.Text;
            // 文本未变化时跳过整篇 Replace：避免产生冗余撤销记录与不必要的重分析
            if (string.Equals(text, _editor.Document.Text, StringComparison.Ordinal)) return;
            _editor.Document.Replace(0, _editor.Document.TextLength, text);
        }
        catch { }
    }

    public async Task FormatDocumentAsync()
    {
        if (!IsInitialized || _host == null || _documentId == null || _editor == null) return;
        try
        {
            var document = _host.GetDocument(_documentId);
            if (document == null) return;
            var formattedDoc = await Formatter.FormatAsync(document) ;
            var formattedText = await formattedDoc.GetTextAsync() ;
            var newText = formattedText.ToString();
            // 内容未变化时跳过 Replace，避免破坏撤销栈
            if (!string.Equals(newText, _editor.Document.Text, StringComparison.Ordinal))
                _editor.Document.Replace(0, _editor.Document.TextLength, newText);
            _host.UpdateDocument(formattedDoc);
        }
        catch { }
    }

    public void CommentSelection()
    {
        if (_editor == null) return;
        ToggleLineComment(addComment: true);
    }

    public void UncommentSelection()
    {
        if (_editor == null) return;
        ToggleLineComment(addComment: false);
    }

    private void ToggleLineComment(bool addComment)
    {
        if (_editor == null) return;
        var document = _editor.Document;
        var textArea = _editor.TextArea;
        if (textArea == null) return;
        var range = GetLineRange(document, textArea);
        if (range.StartLine == null || range.EndLine == null) return;

        _editor.BeginChange();
        try
        {
            var line = range.StartLine;
            while (line != null)
            {
                var lineText = document.GetText(line.Offset, line.Length);
                if (addComment)
                {
                    if (!string.IsNullOrWhiteSpace(lineText))
                        document.Insert(line.Offset, "//");
                }
                else
                {
                    var trimmed = lineText.TrimStart();
                    if (trimmed.StartsWith("//"))
                    {
                        var commentIndex = lineText.IndexOf("//", StringComparison.Ordinal);
                        document.Remove(line.Offset + commentIndex, 2);
                    }
                }
                if (line.LineNumber >= range.EndLine.LineNumber) break;
                line = line.NextLine;
                if (addComment)
                    range = range with { EndLine = document.GetLineByOffset(range.EndLine.Offset) };
            }
        }
        finally { _editor.EndChange(); }
    }

    private static (IDocumentLine StartLine, IDocumentLine EndLine) GetLineRange(
        IDocument document, ICSharpCode.AvalonEdit.Editing.TextArea textArea)
    {
        int startOffset, endOffset;
        if (textArea.Selection.IsEmpty)
        {
            var caretLine = document.GetLineByOffset(textArea.Caret.Offset);
            startOffset = caretLine.Offset;
            endOffset = caretLine.EndOffset;
        }
        else
        {
            startOffset = document.GetOffset(textArea.Selection.StartPosition.Line, textArea.Selection.StartPosition.Column);
            endOffset = document.GetOffset(textArea.Selection.EndPosition.Line, textArea.Selection.EndPosition.Column);
        }
        var startLine = document.GetLineByOffset(startOffset);
        var endLine = document.GetLineByOffset(endOffset);
        return (startLine, endLine);
    }

    public async Task<string?> GetCurrentSymbolNameAsync()
    {
        var symbol = await ResolveSymbolAtCaretAsync();
        return symbol?.Name;
    }

    public async Task RenameSymbolAsync(string newName)
    {
        if (!IsInitialized || _host == null || _documentId == null || _editor == null) return;
        if (string.IsNullOrWhiteSpace(newName)) return;

        try
        {
            var symbol = await ResolveSymbolAtCaretAsync();
            if (symbol == null) return;
            var document = _host.GetDocument(_documentId);
            if (document == null) return;
            var newSolution = await Renamer.RenameSymbolAsync(
                document.Project.Solution, symbol, RenameOptions, newName) ;
            var newDocument = newSolution.GetDocument(_documentId);
            if (newDocument == null) return;
            var newText = await newDocument.GetTextAsync();
            _editor.Document.Replace(0, _editor.Document.TextLength, newText.ToString());
            _host.UpdateDocument(newDocument);
        }
        catch (InvalidOperationException) { }
    }

    private async Task<ISymbol?> ResolveSymbolAtCaretAsync()
    {
        if (!IsInitialized || _host == null || _documentId == null || _editor == null) return null;
        var document = _host.GetDocument(_documentId);
        if (document == null) return null;
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return null;
        var syntaxTree = await document.GetSyntaxTreeAsync();
        if (syntaxTree == null) return null;
        var root = await syntaxTree.GetRootAsync();
        var token = root.FindToken(_editor.CaretOffset);
        var node = token.Parent;
        if (node == null) return null;
        return semanticModel.GetSymbolInfo(node).Symbol ?? semanticModel.GetDeclaredSymbol(node);
    }

    // ════════════════════════════════════════════════════════════════
    //  引用管理与文件操作：见 partial 模块
    //  ScriptEditorService.References.cs / ScriptEditorService.Files.cs
    // ════════════════════════════════════════════════════════════════

    // ════════════════════════════════════════════════════════════════
    //  执行引擎（委托给无头 ScriptEngine）
    // ════════════════════════════════════════════════════════════════

    #region 执行引擎

    /// <summary>设置脚本执行时的输入数据（委托给无头引擎）。</summary>
    public void SetInputData(object? data) => _engine.SetInputData(data);

    /// <summary>
    /// 引擎状态变化 → 镜像到 Model（驱动 WPF 绑定与命令 CanExecute）。
    /// 事件可能在后台线程触发，统一封送到 UI 线程。
    /// </summary>
    private void OnEnginePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.InvokeAsync(() => OnEnginePropertyChanged(sender, e));
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(ScriptEngine.OutputText):
                _model.OutputText = _engine.OutputText;
                if (AttachedItem != null) AttachedItem.OutputText = _engine.OutputText;
                break;
            case nameof(ScriptEngine.StatusText):
                _model.StatusText = _engine.StatusText;
                if (AttachedItem != null) AttachedItem.StatusText = _engine.StatusText;
                break;
            case nameof(ScriptEngine.IsRunning):
                _model.IsRunning = _engine.IsRunning;
                if (AttachedItem != null) AttachedItem.IsRunning = _engine.IsRunning;
                break;
            case nameof(ScriptEngine.IsLooping):
                _model.IsLooping = _engine.IsLooping;
                if (AttachedItem != null) AttachedItem.IsLooping = _engine.IsLooping;
                break;
            case nameof(ScriptEngine.ExecutionTime):
                _model.ExecutionTime = _engine.ExecutionTime;
                if (AttachedItem != null) AttachedItem.ExecutionTime = _engine.ExecutionTime;
                break;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  输出缓冲（冲刷 ScriptEngine 输出队列到 UI）
    // ════════════════════════════════════════════════════════════════

    #region 输出缓冲

    private readonly DispatcherTimer _outputFlushTimer;

    private void StartOutputFlushTimer()
    {
        _outputFlushTimer.IsEnabled = true;
    }

    private void StopOutputFlushTimer()
    {
        _outputFlushTimer.IsEnabled = false;
    }

    #endregion

    /// <summary>
    /// 异步执行当前脚本。返回 true 表示执行成功，false 表示出错。
    /// 执行逻辑在无头 <see cref="ScriptEngine"/> 中完成（编译/运行不依赖 RoslynPad 编辑器）。
    /// </summary>
    public async Task<bool> RunAsync()
    {
        var isLoop = _model.IsLooping;
        if (!isLoop)
            StartOutputFlushTimer();

        try
        {
            return await _engine.RunAsync();
        }
        finally
        {
            if (!isLoop)
                StopOutputFlushTimer();
        }
    }

    /// <summary>终止当前执行。</summary>
    public void Terminate() => _engine.Terminate();

    /// <summary>异步循环执行当前脚本。</summary>
    public async Task LoopRunAsync()
    {
        StartOutputFlushTimer();
        try
        {
            await _engine.LoopRunAsync();
        }
        finally
        {
            StopOutputFlushTimer();
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>停止循环执行。</summary>
    public void StopLoop() => _engine.StopLoop();

    /// <summary>仅编译当前脚本，不执行。</summary>
    public async Task CompileAsync()
    {
        var wasModified = _model.HasModified;
        await _engine.CompileAsync();
        // 编辑器已初始化时刷新诊断（RoslynPad 工作区文档）
        var wasLooping = _model.IsLooping;
        if (IsInitialized && !wasLooping)
            RefreshDiagnostics();
        _model.HasModified = wasModified;
    }

    /// <summary>预编译脚本（后台静默编译，加速首次执行）。</summary>
    public async Task PreCompileScriptAsync(string code)
    {
        await _engine.PreCompileAsync(code);
    }

    #endregion

    // 引用管理操作 → 见 ScriptEditorService.References.cs

    #region 编辑器操作委托

    /// <summary>格式化当前文档。</summary>
    public async Task FormatDocumentWrapperAsync()
    {
        await FormatDocumentAsync();
        _model.StatusText = "已格式化";
    }

    /// <summary>加载默认脚本模板。</summary>
    public void LoadTemplate()
    {
        var currentCode = GetText();
        var template = GetDefaultTemplate();

        // 当前代码为空或与模板相同，直接加载
        if (string.IsNullOrWhiteSpace(currentCode) || currentCode == template)
        {
            SetText(template);
            _model.StatusText = "已加载模板";
            return;
        }

        // 当前有非模板代码，提示用户确认替换
        var prompt = new WPFCustomControls.Window.Views.MessagePrompt(
            "确认替换",
            "当前编辑器中有代码，加载模板将替换现有内容，是否继续？",
            System.Windows.Visibility.Visible);
        if (prompt.ShowDialog() != true)
        {
            _model.StatusText = "已取消加载模板";
            return;
        }

        SetText(template);
        // 显式更新 AttachedItem.Code，确保 ScriptItem 数据同步（共享 Service 场景下 _editor 可能指向其他视图）
        if (AttachedItem != null)
            AttachedItem.Code = template;
        _model.StatusText = "已加载模板";
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    //  新增：资源释放
    // ════════════════════════════════════════════════════════════════

    #region 资源释放

    /// <summary>
    /// 预热 RoslynPad 编辑器：创建隐藏的 RoslynCodeEditor 并调用 InitializeAsync，
    /// 预初始化 RoslynWorkspace + CompletionService + 语法高亮。
    /// 首次调用 ~1.5s，后续 Tab 切换时 InitializeAsync 仅需 &lt; 100ms。
    /// 应在 loading 窗口阶段调用，不阻塞主窗口显示。
    /// 注意：内部涉及 UI 操作（Window / RoslynCodeEditor 均为 DispatcherObject），
    /// 若从后台线程调用会先投递到 UI 线程再执行，任何线程 await 均安全。
    /// </summary>
    public async Task WarmupRoslynPadAsync()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null)
            return;

        if (dispatcher.CheckAccess())
        {
            await WarmupRoslynPadCoreAsync();
        }
        else
        {
            // 后台线程调用：投递到 UI 线程执行核心逻辑（含 UI 创建与 async 初始化）。
            // InvokeAsync(Func<Task>) 返回 DispatcherOperation<Task>，双重 await 展开内层任务。
            await await dispatcher.InvokeAsync(WarmupRoslynPadCoreAsync);
        }
    }

    /// <summary>
    /// 预热核心逻辑，必须在 UI 线程执行（创建隐藏窗口 + RoslynCodeEditor）。
    /// </summary>
    private async Task WarmupRoslynPadCoreAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var roslynHost = RoslynHost;
        if (roslynHost == null)
        {
            System.Diagnostics.Debug.WriteLine("[ScriptEditorService] Warmup skipped: RoslynHost is null");
            return;
        }

        var highlightColors = new RoslynPad.Editor.ClassificationHighlightColors();
        var workingDir = System.IO.Path.GetDirectoryName(typeof(ScriptEditorService).Assembly.Location) ?? ".";
        var template = GetDefaultTemplate();

        var window = new System.Windows.Window
        {
            Width = 0, Height = 0,
            ShowInTaskbar = false,
            WindowStyle = System.Windows.WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Left = -10000, Top = -10000
        };
        var editor = new RoslynCodeEditor();
        window.Content = editor;
        window.Show();

        try
        {
            await editor.InitializeAsync(roslynHost, highlightColors, workingDir, template, SourceCodeKind.Script);
            System.Diagnostics.Debug.WriteLine($"[ScriptEditorService] RoslynPad warmup completed: {sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScriptEditorService] RoslynPad warmup failed: {ex.Message}");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// 预热 UI 控件：预创建工具栏和引用面板，让 BAML 和 MaterialDesign 模板在 loading 阶段解析。
    /// 后续 EnsureBackendInitialized 中 new 时命中 CLR 缓存，从 ~170ms 降至 < 10ms。
    /// </summary>
    public static void WarmupUI()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        new Views.ScriptEditorToolbar();
        System.Diagnostics.Debug.WriteLine($"[ScriptEditorService] UI warmup Toolbar: {sw.ElapsedMilliseconds}ms");
        sw.Restart();
        new Views.ScriptEditorReferencePanel();
        System.Diagnostics.Debug.WriteLine($"[ScriptEditorService] UI warmup ReferencePanel: {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// 统一预热入口：loading 阶段调用，一次性完成所有 ScriptEditor 后端+UI 预热。
    /// 包含：RoslynHost 初始化 → ScriptEditorService 预解析 → RoslynPad 预热 → UI 控件预热。
    /// </summary>
    /// <param name="container">Prism DI 容器，用于预解析 ScriptEditorService 单例。</param>
    public   async Task WarmupAsync(Prism.Ioc.IContainerProvider container)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        System.Diagnostics.Debug.WriteLine("[ScriptEditorService] ====== WarmupAsync START ======");

        // 1. 等待 RoslynHost 后台线程完成初始化（ScriptEditorModule.WarmupTask）
        if (ScriptEditorModule.WarmupTask != null)
        {
            await ScriptEditorModule.WarmupTask;
            System.Diagnostics.Debug.WriteLine($"[ScriptEditorService] Warmup Step1 RoslynHost: {sw.ElapsedMilliseconds}ms");
        }
 
        // 3. 预热 RoslynPad 编辑器（隐藏窗口 + InitializeAsync，预初始化 CompletionService）
        sw.Restart();
        await WarmupRoslynPadAsync();
        System.Diagnostics.Debug.WriteLine($"[ScriptEditorService] Warmup Step3 RoslynPad: {sw.ElapsedMilliseconds}ms");

        // 4. 预热工具栏和引用面板 BAML + MaterialDesign 模板
        sw.Restart();
        WarmupUI();
        System.Diagnostics.Debug.WriteLine($"[ScriptEditorService] Warmup Step4 UI: {sw.ElapsedMilliseconds}ms");

        System.Diagnostics.Debug.WriteLine("[ScriptEditorService] ====== WarmupAsync COMPLETE ======");
    }

    /// <summary>释放执行相关资源。</summary>
    public void DisposeExecution()
    {
        StopOutputFlushTimer();
        _engine.StopLoop();
        _engine.Terminate();
    }

    #endregion
}