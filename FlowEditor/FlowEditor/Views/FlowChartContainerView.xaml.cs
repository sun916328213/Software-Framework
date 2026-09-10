using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FlowEditor.ViewModels;
using WPFCustomControls.Controls;

namespace FlowEditor.Views;

/// <summary>组合流程命令栏、工具箱、编辑画布、属性面板和输出区域。</summary>
public partial class FlowChartContainerView : UserControl
{
    /// <summary>获取当前视图的数据上下文。</summary>
    private FlowChartContainerViewModel VM => (FlowChartContainerViewModel)DataContext;
    /// <summary>初始化流程容器。</summary>
    public FlowChartContainerView()
    {
        InitializeComponent();
        Loaded += (_, _) => Focusable = true;
    }

    #region 标签页重命名

    /// <summary>双击标签头编辑完成后，将新名称写入 FlowChartModel.TabTitle。</summary>
    private void OnHeaderRenamed(object sender, RoutedEventArgs e)
    {
        if (e is HeaderRenameEventArgs args && args.RenameInfo.DataItem is FlowChartViewModel vm)
        {
            vm.RenameTab(args.RenameInfo.NewText);
        }
    }

    #endregion

    #region 导出 PNG

    /// <summary>根据是否存在活动标签页更新 PNG 导出命令状态。</summary>
    private void OnExportPngCanExecute(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = e.Parameter is FlowChartViewModel;
        e.Handled = true;
    }

    /// <summary>执行活动标签页的 PNG 导出。</summary>
    private void OnExportPngExecuted(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is not FlowChartViewModel activeTab) return;

        var flowChartView = FindActiveFlowChart(FlowTabControl, activeTab);
        if (flowChartView == null)
        {
            FlowTabControl.UpdateLayout();
            flowChartView = FindActiveFlowChart(FlowTabControl, activeTab);
        }

        if (flowChartView == null)
        {
            activeTab.Model.StatusText = "导出失败：活动画布尚未加载";
            e.Handled = true;
            return;
        }

        activeTab.ExportPng(
            flowChartView.ZoomContainerElement,
            activeTab.GetCanvasExportBounds());
        e.Handled = true;
    }

    #endregion

    #region 查找控件

    /// <summary>从标签宿主的内容视觉树中查找绑定到活动标签页的流程画布。</summary>
    /// <param name="tabHost">承载选中内容的标签控件。</param>
    /// <param name="activeTab">活动标签页的数据上下文。</param>
    /// <returns>活动流程画布；内容尚未实现时返回 <see langword="null"/>。</returns>
    internal static FlowChartView? FindActiveFlowChart(DependencyObject tabHost, object activeTab) =>
        FindVisualChild<FlowChartView>(
            tabHost,
            candidate => ReferenceEquals(candidate.DataContext, activeTab));

    /// <summary>在指定视觉树下递归查找第一个满足条件的目标类型子元素。</summary>
    /// <typeparam name="T">要查找的视觉元素类型。</typeparam>
    /// <param name="parent">搜索起点。</param>
    /// <param name="predicate">目标元素必须满足的条件。</param>
    /// <returns>找到的元素；不存在时返回 <see langword="null"/>。</returns>
    private static T? FindVisualChild<T>(DependencyObject parent, Predicate<T> predicate)
        where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found && predicate(found))
                return found;
            var result = FindVisualChild(child, predicate);
            if (result != null)
                return result;
        }
        return null;
    }

    #endregion
}
