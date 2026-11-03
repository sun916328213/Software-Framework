using System.Windows;
using System.Windows.Controls;
using Vision.Controls.ViewModels;

#nullable enable
namespace Vision.Controls.View;

/// <summary>图像预设与操作栏；借用显示面板，不创建图像、工具或会话。</summary>
public partial class VisionImageToolbarView : UserControl
{
    public static readonly DependencyProperty ImagePanelProperty = DependencyProperty.Register(nameof(ImagePanel),
        typeof(VisionImagePanel), typeof(VisionImageToolbarView), new PropertyMetadata(null));
    public VisionImagePanel? ImagePanel { get => (VisionImagePanel?)GetValue(ImagePanelProperty); set => SetValue(ImagePanelProperty, value); }

    public VisionImageToolbarView() => InitializeComponent();

    private void SelectCanvas(object sender, RoutedEventArgs e)
    {
        if (DataContext is VisionToolEditorSession { IsSessionActive: true })
            ImagePanel?.SelectInteractionContext(focus: true);
    }
    private void FitImage(object sender, RoutedEventArgs e) => ImagePanel?.ImageViewport.Fit();
    private void ActualPixels(object sender, RoutedEventArgs e) => ImagePanel?.ImageViewport.ActualPixels();
}
