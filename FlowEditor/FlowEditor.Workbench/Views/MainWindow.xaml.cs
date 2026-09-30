using System.Windows;

namespace FlowEditor.Workbench.Views;

/// <summary>工作台壳窗口：仅承载标题栏与流程图编辑区域，业务内容由区域导航装配。</summary>
public partial class MainWindow : Window
{
    /// <summary>初始化壳窗口组件。</summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}
