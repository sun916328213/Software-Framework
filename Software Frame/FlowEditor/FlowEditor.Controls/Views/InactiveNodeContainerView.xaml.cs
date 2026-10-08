// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：InactiveNodeContainerView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Windows、System.Windows.Controls、System.Windows.Controls.Primitives、System.Windows.Input、System.Windows.Media。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.ViewModels;

namespace FlowEditor.Controls.Views;

/// <summary>呈现并操作画布上的不执行节点容器。</summary>
public partial class InactiveNodeContainerView : UserControl
{
    /// <summary>初始化容器标题栏的拖拽交互。</summary>
    public InactiveNodeContainerView()
    {
        InitializeComponent();
        new ContainerTitleDrag(this, TitleBar).Attach();
    }

    /// <summary>把缩放手柄的位移应用到容器尺寸。</summary>
    private void OnResizeDelta(object sender, DragDeltaEventArgs e)
    {
        if (DataContext is not NodeViewModel { Model: InactiveNodeContainerNode container }) return;
        container.ContainerWidth += e.HorizontalChange;
        container.ContainerHeight += e.VerticalChange;
    }

    /// <summary>缩放结束后重新计算节点与不执行容器的归属关系。</summary>
    private void OnResizeCompleted(object sender, DragCompletedEventArgs e)
        => ContainerTitleDrag.RefreshMembership(this);
}
