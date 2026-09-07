using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlowEditor.ViewModels;
using WPFCustomControls.Controls;

namespace FlowEditor.Views;

public partial class FlowChartContainerView : UserControl
{
    private FlowChartContainerViewModel VM => (FlowChartContainerViewModel)DataContext;
    private NodePaletteItem? _draggingPaletteItem;

    public FlowChartContainerView()
    {
        InitializeComponent();
        Loaded += (_, _) => Focusable = true;

        // 容器级 PreviewMouseLeftButtonUp：处理从工具箱拖放到画布
        PreviewMouseLeftButtonUp += OnContainerMouseLeftButtonUp;
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

    #region 工具箱交互

    private void OnCategoryHeaderClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ToolboxCategory category)
            category.IsExpanded = !category.IsExpanded;
    }

    private void OnPaletteItemMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not NodePaletteItem item) return;
        if (VM.ActiveTab == null) return;

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _draggingPaletteItem = item;

            if (e.ClickCount == 2)
            {
                VM.ActiveTab.AddNode(item);
                _draggingPaletteItem = null;
            }
        }
    }

    /// <summary>拖放释放：如果鼠标在激活标签的画布内，则放置节点。</summary>
    private void OnContainerMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingPaletteItem == null || VM.ActiveTab == null) return;

        var tabItem = FlowTabControl.ItemContainerGenerator.ContainerFromItem(VM.ActiveTab) as TabItem;
        if (tabItem == null) { _draggingPaletteItem = null; return; }

        var fcv = FindVisualChild<FlowChartView>(tabItem);
        if (fcv == null) { _draggingPaletteItem = null; return; }

        try
        {
            var canvasPos = e.GetPosition(fcv.ZoomContainerElement);
            var w = fcv.ZoomContainerElement.ActualWidth;
            var h = fcv.ZoomContainerElement.ActualHeight;
            if (canvasPos.X >= 0 && canvasPos.Y >= 0 && canvasPos.X <= w && canvasPos.Y <= h)
            {
                VM.ActiveTab.AddNodeAt(_draggingPaletteItem, canvasPos);
            }
        }
        catch { }
        _draggingPaletteItem = null;
    }

    #endregion

    #region 导出 PNG

    private void OnExportPngClick(object sender, RoutedEventArgs e)
    {
        if (VM.ActiveTab == null) return;

        var tabItem = FlowTabControl.ItemContainerGenerator.ContainerFromItem(VM.ActiveTab) as TabItem;
        if (tabItem == null) return;

        var flowChartView = FindVisualChild<FlowChartView>(tabItem);
        if (flowChartView == null) return;

        VM.ActiveTab.ExportPngCommand.Execute(flowChartView.ZoomContainerElement);
    }

    #endregion

    #region 查找控件

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found)
                return found;
            var result = FindVisualChild<T>(child);
            if (result != null)
                return result;
        }
        return null;
    }

    #endregion
}
