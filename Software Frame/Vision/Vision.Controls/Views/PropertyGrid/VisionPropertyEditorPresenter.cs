using System.Windows.Controls;
using Vision.Controls.Presentation;

namespace Vision.Controls.View;

/// <summary>只生成依赖运行时 PropertyInfo 的编辑器；行布局与视觉样式由 XAML 决定。</summary>
public sealed class VisionPropertyEditorPresenter : ContentControl
{
    public VisionPropertyEditorPresenter() =>
        DataContextChanged += (_, e) => Content = e.NewValue is VisionPropertyItem item ? item.CreateEditor() : null;
}
