using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Rendering;

namespace ScriptEditor.Views;

/// <summary>
/// VS 风格断点边栏：浅灰底色 + 断点行居中红色实心圆。
/// 作为独立控件放在编辑器左侧，与行号栏并列。
/// </summary>
public class BreakpointGutter : Canvas
{
    private ObservableCollection<int> _breakpoints = null!;
    private TextView? _textView;

    Brush vsBreakpointBrush;
    public Brush DotFill { get; set; }  
    public Pen DotStroke { get; set; }  
    public double DotRadius { get; set; } = 6;

    public BreakpointGutter()
    {
        vsBreakpointBrush = new SolidColorBrush(Color.FromRgb(200, 30, 39));
        DotFill=vsBreakpointBrush;
        DotStroke = new Pen(vsBreakpointBrush, 0);

        Width = 20;
        MinWidth= 20;
        Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
        Cursor = Cursors.Hand;
    }

    /// <summary>绑定断点集合与编辑器 TextView。</summary>
    public void Attach(ObservableCollection<int> breakpoints, TextView textView)
    {
        _breakpoints = breakpoints;
        _textView = textView;

        _breakpoints.CollectionChanged += (_, _) => InvalidateVisual();
        _textView.VisualLinesChanged += (_, _) => InvalidateVisual();
    }

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

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (_textView == null || _breakpoints.Count == 0) return;

        var offsetY = _textView.ScrollOffset.Y;
        var cx = Width / 2;

        foreach (var vl in _textView.VisualLines)
        {
            if (!_breakpoints.Contains(vl.FirstDocumentLine.LineNumber)) continue;

            var top = vl.VisualTop - offsetY;
            dc.DrawEllipse(DotFill, DotStroke,
                new Point(cx, top + vl.Height / 2), DotRadius, DotRadius);
        }
    }
}