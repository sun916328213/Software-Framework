using System.Windows;

namespace FlowNodes.Workbench.Views;

/// <summary>
/// 测试工作台主窗口：仅提供 FlowRegion 区域占位，
/// 完整编辑器由 <see cref="App"/> 在模块加载完成后经区域导航装配。
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
