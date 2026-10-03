// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowOutputPaneView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Windows.Controls;

namespace FlowEditor.Controls.Views;

/// <summary>显示当前流程标签页的执行输出，并提供清空、过滤、搜索和滚动控制。</summary>
public partial class FlowOutputPaneView : UserControl
{
    /// <summary>初始化流程输出栏。</summary>
    public FlowOutputPaneView()
    {
        InitializeComponent();
    }
}
