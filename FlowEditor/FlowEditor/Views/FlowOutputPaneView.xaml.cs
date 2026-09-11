using System.Windows.Controls;

namespace FlowEditor.Views;

/// <summary>显示当前流程标签页的执行输出，并提供一键清空入口。</summary>
public partial class FlowOutputPaneView : UserControl
{
    /// <summary>初始化流程输出栏。</summary>
    public FlowOutputPaneView()
    {
        InitializeComponent();
    }
}
