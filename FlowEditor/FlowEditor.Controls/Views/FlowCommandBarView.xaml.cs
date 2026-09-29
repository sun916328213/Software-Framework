using System.Windows.Controls;

namespace FlowEditor.Views;

/// <summary>
/// 显示流程容器级命令、运行状态和视图控制。
/// </summary>
public partial class FlowCommandBarView : UserControl
{
    /// <summary>用户请求导出当前活动画布时发生。</summary>
    public event EventHandler? ExportPngRequested;

    /// <summary>初始化流程命令栏。</summary>
    public FlowCommandBarView()
    {
        InitializeComponent();
    }

    /// <summary>把导出按钮点击转换为命令栏与宿主视图之间的显式事件 seam。</summary>
    private void OnExportPngClick(object sender, System.Windows.RoutedEventArgs e)
        => ExportPngRequested?.Invoke(this, EventArgs.Empty);
}
