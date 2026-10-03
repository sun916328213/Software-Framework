// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowCommandBarView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Windows.Controls;

namespace FlowEditor.Controls.Views;

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
