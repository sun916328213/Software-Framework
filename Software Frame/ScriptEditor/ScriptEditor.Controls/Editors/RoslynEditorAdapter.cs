// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：RoslynEditorAdapter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Controls、System.Collections.ObjectModel、System.Diagnostics、System.Windows、System.Windows.Controls、System.Windows.Input。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Controls;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.CodeAnalysis;
using RoslynPad.Editor;
using RoslynPad.Roslyn;
using ScriptEditor.Controls.Services;
using ScriptEditor.Runtime.Services;
using ScriptEditor.Controls.Views;

namespace ScriptEditor.Controls.Editors;

/// <summary>
/// RoslynPad 编辑器适配器：封装重量级编辑器控件的创建、初始化与文档操作。
/// </summary>
/// <remarks>
/// P4 产物：原来散落在 <c>ScriptEditorView.xaml.cs</c> 中的 RoslynPad 操作
///（动态创建控件、<c>InitializeAsync</c>、补全提供者修复、文本/光标/滚动/缩放）集中到此。
/// 不持有运行状态（IsRunning/Output 等仍由 <c>IScriptSession</c> 独占），
/// 不做文件对话框与持久化。
/// </remarks>
public sealed class RoslynEditorAdapter
{
    private readonly IClassificationHighlightColors _highlightColors;

    /// <summary>
    /// 初始化 RoslynEditorAdapter 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public RoslynEditorAdapter()
    {
        _highlightColors = new ThemeClassificationHighlightColors(() =>
            _editor?.IsDarkTheme ?? false);
    }

    private RoslynCodeEditorView? _editor;
    private BreakpointGutter? _gutter;

    public RoslynCodeEditorView? Editor => _editor;
    public BreakpointGutter? Gutter => _gutter;
    public bool IsCreated => _editor != null;
    public bool IsInitialized { get; private set; }

    /// <summary>编辑器全文（UI 线程访问）。</summary>
    public string Text
    {
        get => _editor?.Text ?? string.Empty;
        set
        {
            if (_editor != null)
                _editor.Text = value;
        }
    }

    public string DocumentText => _editor?.Document?.Text ?? string.Empty;

    public int CaretOffset
    {
        get => _editor?.TextArea.Caret.Offset ?? 0;
        set
        {
            if (_editor == null) return;
            var length = _editor.Document?.TextLength ?? 0;
            _editor.TextArea.Caret.Offset = Math.Min(Math.Max(0, value), length);
        }
    }

    public (int Line, int Column) CaretPosition
    {
        get
        {
            if (_editor == null) return (1, 1);
            var caret = _editor.TextArea.Caret;
            return (caret.Line, caret.Column);
        }
    }

    public double VerticalOffset
    {
        get => _editor?.TextArea?.TextView?.VerticalOffset ?? 0;
        set => SetVerticalOffset(value);
    }

    public double FontSize
    {
        get => _editor?.FontSize ?? 12;
        set
        {
            if (_editor != null)
                _editor.FontSize = value;
        }
    }

    public event Action<string>? TextChanged;
    public event Action<int, int, int>? CaretChanged;
    public event Action<double>? ScrollChanged;

    /// <summary>
    /// 动态创建重量级编辑器控件（替代 XAML 同步加载，避免阻塞 UI 线程）。
    /// 幂等：已创建时直接返回。
    /// </summary>
    public void EnsureCreated(Grid editorRoot, Func<Brush> gutterBackground)
    {
        if (_editor != null) return;

        var sw = Stopwatch.StartNew();

        _editor = new RoslynCodeEditorView
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetColumn(_editor, 1);
        editorRoot.Children.Add(_editor);

        _gutter = new BreakpointGutter
        {
            Width = 20,
            Background = gutterBackground(),
            DotRadius = 7
        };
        _gutter.SetResourceReference(Panel.BackgroundProperty, "MaterialDesign.Brush.Card.Background");
        Grid.SetColumn(_gutter, 0);
        editorRoot.Children.Add(_gutter);

        _editor.PreviewMouseWheel += OnEditorMouseWheel;

        Debug.WriteLine($"[RoslynEditorAdapter] CreateControls: {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>初始化 RoslynPad 文档（只需成功一次，后续切页仅替换文本）。</summary>
    public async Task<DocumentId> InitializeAsync(
        RoslynHost host, string workingDir, string template)
    {
        if (_editor == null)
            throw new InvalidOperationException("EnsureCreated must be called before InitializeAsync.");

        var sw = Stopwatch.StartNew();
        var documentId = await _editor.InitializeAsync(
            host,
            _highlightColors,
            workingDir,
            template,
            SourceCodeKind.Script).ConfigureAwait(true);
        Debug.WriteLine($"[RoslynEditorAdapter] InitializeAsync: {sw.ElapsedMilliseconds}ms");

        FixCompletionProvider();
        HookDocumentEvents();
        IsInitialized = true;
        return documentId;
    }

    /// <summary>
    /// 替换补全提供者：修复预处理器指令文本替换 Bug，
    /// 非 # 上下文过滤 # 指令项。
    /// </summary>
    public void FixCompletionProvider()
    {
        if (_editor == null) return;

        if (_editor.CompletionProvider is RoslynCodeEditorCompletionProvider roslynProvider)
        {
            _editor.CompletionProvider = new FixedCodeEditorCompletionProvider(
                roslynProvider, () => _editor.Document?.Text ?? string.Empty);
        }
        else if (_editor.CompletionProvider == null)
        {
            _editor.Loaded += (_, __) =>
            {
                if (_editor.CompletionProvider is RoslynCodeEditorCompletionProvider provider)
                    _editor.CompletionProvider = new FixedCodeEditorCompletionProvider(
                        provider, () => _editor.Document?.Text ?? string.Empty);
            };
        }
    }

    /// <summary>绑定 VS 风格断点边栏到断点集合。</summary>
    public void AttachBreakpoints(
        ObservableCollection<int> breakpoints,
        Action<int> onLineClicked,
        object bindingSource,
        string isDebugModePropertyName)
    {
        if (_gutter == null || _editor == null) return;

        _gutter.Attach(breakpoints, _editor.TextArea.TextView);
        _gutter.LineClicked += (_, line) =>
        {
            onLineClicked(line);
            _editor.TextArea.TextView.InvalidateVisual();
        };

        _gutter.SetBinding(UIElement.VisibilityProperty,
            new System.Windows.Data.Binding(isDebugModePropertyName)
            {
                Source = bindingSource,
                Converter = new BooleanToVisibilityConverter()
            });
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 AdjustFontSize。
    /// 正文中的关键协作调用包括 Max、Min；具体条件和先后次序见方法体。
    /// </summary>
    public void AdjustFontSize(int delta)
    {
        if (_editor == null) return;
        var size = Math.Max(8, Math.Min(72, _editor.FontSize + delta));
        _editor.FontSize = size;
    }

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetVerticalOffset。
    /// 正文中的关键协作调用包括 SetVerticalOffset；具体条件和先后次序见方法体。
    /// </summary>
    private void SetVerticalOffset(double offset)
    {
        var textView = _editor?.TextArea?.TextView;
        if (textView is System.Windows.Controls.Primitives.IScrollInfo scrollInfo)
            scrollInfo.SetVerticalOffset(offset);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 HookDocumentEvents。
    /// </summary>
    private void HookDocumentEvents()
    {
        if (_editor == null) return;

        _editor.Document.TextChanged += (_, _) => TextChanged?.Invoke(DocumentText);
        _editor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            var caret = _editor.TextArea.Caret;
            CaretChanged?.Invoke(caret.Line, caret.Column, caret.Offset);
        };
        _editor.TextArea.TextView.ScrollOffsetChanged += (_, _) =>
            ScrollChanged?.Invoke(_editor.TextArea.TextView.VerticalOffset);
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnEditorMouseWheel。
    /// 正文中的关键协作调用包括 AdjustFontSize；具体条件和先后次序见方法体。
    /// </summary>
    private void OnEditorMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_editor == null) return;
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            AdjustFontSize(e.Delta > 0 ? 1 : -1);
            e.Handled = true;
        }
    }
}
