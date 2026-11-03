// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowToolboxView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Windows、System.Windows.Controls、System.Windows.Input、FlowEditor.Controls.ViewModels。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlowEditor.Controls.ViewModels;

namespace FlowEditor.Controls.Views;

/// <summary>显示可搜索的节点分类，并封装节点双击添加和标准 WPF 拖放的发起过程。</summary>
public partial class FlowToolboxView : UserControl
{
    /// <summary>组合宿主可提供添加命令；未设置时保持原活动标签页行为。命令参数为工具箱项。</summary>
    public static readonly DependencyProperty AddNodeCommandProperty = DependencyProperty.Register(
        nameof(AddNodeCommand), typeof(ICommand), typeof(FlowToolboxView), new PropertyMetadata(null));
    /// <summary>获取或设置组合宿主的节点添加命令。</summary>
    public ICommand? AddNodeCommand { get => (ICommand?)GetValue(AddNodeCommandProperty); set => SetValue(AddNodeCommandProperty, value); }

    /// <summary>优先检查组合宿主命令，未配置命令时沿用活动标签页判定。</summary>
    private bool CanAdd(NodePaletteItem item) => AddNodeCommand?.CanExecute(item) ?? (ViewModel?.ActiveTab != null);
    private NodePaletteItem? _draggingPaletteItem;
    private Point _paletteDragStart;

    /// <summary>获取当前工具箱所属的容器视图模型。</summary>
    private FlowChartContainerViewModel? ViewModel => DataContext as FlowChartContainerViewModel;

    /// <summary>初始化流程工具箱。</summary>
    public FlowToolboxView()
    {
        InitializeComponent();
    }

    /// <summary>切换工具箱分类或子分组的展开状态。</summary>
    private void OnCategoryHeaderClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ToolboxCategory category })
            category.IsExpanded = !category.IsExpanded;
    }

    /// <summary>记录工具项按下位置，并在双击时向活动流程添加节点。</summary>
    private void OnPaletteItemMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: NodePaletteItem item }) return;
        _draggingPaletteItem = null;
        if (!CanAdd(item) || e.LeftButton != MouseButtonState.Pressed) return;

        _draggingPaletteItem = item;
        _paletteDragStart = e.GetPosition(this);

        if (e.ClickCount == 2)
        {
            if (AddNodeCommand is { } command) command.Execute(item);
            else ViewModel!.ActiveTab!.AddNode(item);
            _draggingPaletteItem = null;
        }
    }

    /// <summary>在移动超过系统阈值后，以复制语义启动节点工具项拖放。</summary>
    private void OnPaletteItemMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingPaletteItem == null || e.LeftButton != MouseButtonState.Pressed) return;

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _paletteDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _paletteDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var item = _draggingPaletteItem;
        _draggingPaletteItem = null;
        if (!CanAdd(item)) return;
        DragDrop.DoDragDrop((DependencyObject)sender, item, DragDropEffects.Copy);
    }
}
