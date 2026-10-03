// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：BreakpointGutter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Controls、System、System.Collections.Generic、System.Collections.ObjectModel、System.Collections.Specialized、System.Windows。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Rendering;

namespace ScriptEditor.Controls.Views;

/// <summary>
/// VS 风格断点边栏：浅灰底色 + 断点行居中红色实心圆。
/// 作为独立控件放在编辑器左侧，与行号栏并列。
/// </summary>
/// <remarks>
/// 性能：断点集合缓存为 <see cref="HashSet{T}"/>，渲染时 O(1) 判定（原实现对每条
/// 可视线做 ObservableCollection 线性查找，为 O(可视行数 × 断点数)）。
/// </remarks>
public class BreakpointGutter : Canvas
{
    private ObservableCollection<int> _breakpoints = null!;
    private TextView? _textView;

    /// <summary>断点行号集合的 O(1) 查找缓存（集合变更时重建）。</summary>
    private HashSet<int> _breakpointSet = new();

    // 订阅保存为字段：防止重复 Attach 时订阅叠加
    private NotifyCollectionChangedEventHandler? _collectionChangedHandler;
    private EventHandler? _visualLinesChangedHandler;

    Brush vsBreakpointBrush;
    public Brush DotFill { get; set; }
    public Pen DotStroke { get; set; }
    public double DotRadius { get; set; } = 6;

    /// <summary>
    /// 初始化 BreakpointGutter 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 FromRgb；具体条件和先后次序见方法体。
    /// </summary>
    public BreakpointGutter()
    {
        vsBreakpointBrush = new SolidColorBrush(Color.FromRgb(200, 30, 39));
        DotFill = vsBreakpointBrush;
        DotStroke = new Pen(vsBreakpointBrush, 0);

        Width = 20;
        MinWidth = 20;
        Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
        Cursor = Cursors.Hand;
    }

    /// <summary>绑定断点集合与编辑器 TextView。</summary>
    public void Attach(ObservableCollection<int> breakpoints, TextView textView)
    {
        // 防止重复 Attach（View 多次 Loaded）时订阅叠加
        if (_breakpoints != null && _collectionChangedHandler != null)
            _breakpoints.CollectionChanged -= _collectionChangedHandler;
        if (_textView != null && _visualLinesChangedHandler != null)
            _textView.VisualLinesChanged -= _visualLinesChangedHandler;

        _breakpoints = breakpoints;
        _textView = textView;

        _collectionChangedHandler = (_, _) => { RebuildBreakpointSet(); InvalidateVisual(); };
        _visualLinesChangedHandler = (_, _) => InvalidateVisual();

        _breakpoints.CollectionChanged += _collectionChangedHandler;
        _textView.VisualLinesChanged += _visualLinesChangedHandler;

        RebuildBreakpointSet();
        InvalidateVisual();
    }

    /// <summary>重建断点行号查找缓存。</summary>
    private void RebuildBreakpointSet()
    {
        var set = new HashSet<int>(_breakpoints.Count);
        foreach (var line in _breakpoints)
            set.Add(line);
        _breakpointSet = set;
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnMouseLeftButtonDown。
    /// 正文中的关键协作调用包括 OnMouseLeftButtonDown、GetPosition、GetVisualLineFromVisualTop；具体条件和先后次序见方法体。
    /// </summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_textView == null) return;

        var visualY = e.GetPosition(this).Y + _textView.ScrollOffset.Y;
        var vl = _textView.GetVisualLineFromVisualTop(visualY);
        if (vl == null) return;

        LineClicked?.Invoke(this, vl.FirstDocumentLine.LineNumber);
        e.Handled = true;
    }

    public event EventHandler<int>? LineClicked;

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnRender。
    /// 正文中的关键协作调用包括 OnRender、Contains、DrawEllipse；具体条件和先后次序见方法体。
    /// </summary>
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (_textView == null || _breakpointSet.Count == 0) return;

        var offsetY = _textView.ScrollOffset.Y;
        var cx = Width / 2;

        foreach (var vl in _textView.VisualLines)
        {
            if (!_breakpointSet.Contains(vl.FirstDocumentLine.LineNumber)) continue;

            var top = vl.VisualTop - offsetY;
            dc.DrawEllipse(DotFill, DotStroke,
                new Point(cx, top + vl.Height / 2), DotRadius, DotRadius);
        }
    }
}
