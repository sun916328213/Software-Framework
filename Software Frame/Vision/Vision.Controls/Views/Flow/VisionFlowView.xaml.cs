#nullable enable
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Data;
using Vision.FlowNode;
using Vision.Controls.View;
using Vision.Controls.Services;
using Prism.Regions;
using Prism.Ioc;
using FlowEditor.Controls.ViewModels;
using Vision.Controls.ViewModels;

namespace Vision.Controls.View;

/// <summary>宿主组合视图；导航和卸载都不销毁流程实例，由宿主关闭链归还DataContext。</summary>
public partial class VisionFlowView : UserControl, INavigationAware
{
    public static readonly DependencyProperty ToolbarAccessoryProperty = DependencyProperty.Register(
        nameof(ToolbarAccessory), typeof(object), typeof(VisionFlowView), new PropertyMetadata(null));
    public object? ToolbarAccessory { get => GetValue(ToolbarAccessoryProperty); set => SetValue(ToolbarAccessoryProperty, value); }
    private readonly Dictionary<VisionToolNodeBase, Window> _editors = [];
    private VisionFlowViewModel? _observed;
    private VisionFlowViewModel? _hosted;
    public VisionFlowView()
    {
        InitializeComponent();
        Loaded += (_, _) => Observe(DataContext as VisionFlowViewModel);
        Unloaded += (_, _) => Observe(null);
        DataContextChanged += (_, _) => { if (IsLoaded) { Observe(DataContext as VisionFlowViewModel); OnEditorLoaded(FlowHost, new RoutedEventArgs()); } };
    }
    private void Observe(VisionFlowViewModel? model)
    {
        if (ReferenceEquals(model, _observed)) return;
        if (_observed != null) { _observed.EditorRequested -= ShowEditor; _observed.EditorClosing -= CloseEditor; }
        foreach (var window in _editors.Values.ToArray()) window.Close();
        _observed = model;
        if (model != null) { model.EditorRequested += ShowEditor; model.EditorClosing += CloseEditor; }
    }
    private void ShowEditor(VisionToolNodeBase node) => OpenToolEditor(node);
    private void CloseEditor(VisionToolNodeBase node) { if (_editors.TryGetValue(node, out var window)) window.Close(); }

    /// <summary>只创建借用视图；每个节点最多一个非模态Owned窗口，切换选中节点不重绑。</summary>
    public Window? OpenToolEditor(VisionToolNodeBase node)
    {
        if (_observed?.Owns(node) != true ||
            node.Session is not VisionToolInstanceWorkspace workspace || !workspace.Editor.IsSessionActive) return null;
        if (_editors.TryGetValue(node, out var existing))
        {
            if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
            existing.Activate(); return existing;
        }
        var owner = Window.GetWindow(this);
        if (owner?.IsVisible != true) return null;
        var editor = new VisionToolEditorView { Session = workspace.Editor };
        var window = new Window
        {
            Owner = owner, DataContext = node, Content = editor, Width = 1280, Height = 800,
            MinWidth = 800, MinHeight = 560, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        window.SetBinding(Window.TitleProperty, new Binding(nameof(node.Name)) { Source = node, StringFormat = "{0} — 完整工具编辑器" });
        window.SetResourceReference(BackgroundProperty, "MaterialDesign.Brush.Background");
        window.Closed += (_, _) =>
        {
            _editors.Remove(node);
            editor.Session = null; window.Content = null; window.DataContext = null;
        };
        _editors.Add(node, window);
        try { window.Show(); return window; }
        catch { _editors.Remove(node); editor.Session = null; window.Content = null; throw; }
    }
    private void OnNodeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ClickCount != 2) return;
        var current = e.OriginalSource as DependencyObject;
        while (current != null && current is not FlowEditor.Controls.Views.NodeView)
        {
            if (current is ButtonBase || current is TextBox || current is FlowEditor.Controls.Views.PortConnectorView) return;
            current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current);
        }
        if (current is FrameworkElement { DataContext: NodeViewModel { Model: VisionToolNodeBase node } })
        { node.OpenToolEditor(); e.Handled = true; }
    }
    private async void OnEditorLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not VisionFlowViewModel vm) return;
        if (_hosted != null && !ReferenceEquals(_hosted, vm))
            FlowHost.DataContext = vm.Editor ?? ContainerLocator.Container.Resolve<FlowChartContainerViewModel>();
        if (FlowHost.DataContext is not FlowChartContainerViewModel editor) return;
        if (vm.Editor != null && !ReferenceEquals(vm.Editor, editor))
        {
            var unused = editor;
            FlowHost.DataContext = editor = vm.Editor;
            // 重新放置同一任务时，AutoWire产生的空容器也须释放日志路由；不碰借用任务。
            try { await unused.DisposeAsync(); }
            catch (Exception error) { vm.Chart.Model.StatusText = $"空编辑容器清理失败：{error.Message}"; }
        }
        _hosted = vm;
        vm.AttachEditor(editor);
        var grid = (WPFCustomControls.PropertyGrid.Views.PropertyGridHost)FlowHost.FindName("PropertyGrid");
        grid.SetBinding(WPFCustomControls.PropertyGrid.Views.PropertyGridHost.IsEditingEnabledProperty,
            new Binding(nameof(vm.IsEditingEnabled)) { Source = vm });
        var toolbox = FindChild<FlowEditor.Controls.Views.FlowToolboxView>(FlowHost)!;
        toolbox.AddNodeCommand = vm.AddPaletteCommand;
        // 保留旧宿主查询名称作为同一控件的别名，不创建第二个画布/属性/工具箱。
        RegisterAlias("NodeProperties", grid); RegisterAlias("Toolbox", toolbox);
        FlowHost.UpdateLayout();
        if (ActiveCanvas is { } canvas) RegisterAlias("FlowCanvas", canvas);
    }
    private void RegisterAlias(string name, object value)
    {
        if (FindName(name) != null) UnregisterName(name);
        RegisterName(name, value);
    }
    private FlowEditor.Controls.Views.FlowChartView? ActiveCanvas => FindChild<FlowEditor.Controls.Views.FlowChartView>(
        FlowHost, candidate => ReferenceEquals(candidate.DataContext, (FlowHost.DataContext as FlowChartContainerViewModel)?.ActiveTab));
    private static T? FindChild<T>(DependencyObject root, Predicate<T>? predicate = null) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T found && (predicate == null || predicate(found))) return found;
            if (FindChild<T>(child, predicate) is { } nested) return nested;
        }
        return null;
    }
    private void OnPaletteDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(NodePaletteItem)) is not NodePaletteItem item) return;
        e.Effects = DataContext is VisionFlowViewModel vm && vm.AddPaletteCommand.CanExecute(item)
            && e.AllowedEffects.HasFlag(DragDropEffects.Copy) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
    private void OnPaletteDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(NodePaletteItem)) is not NodePaletteItem item) return;
        // 复用标准工具箱载荷和原添加入口；避免通用画布再次创建同一个实例。
        e.Effects = e.AllowedEffects.HasFlag(DragDropEffects.Copy) && DataContext is VisionFlowViewModel vm
            && ActiveCanvas is { } canvas && vm.AddNodePaletteAt(item, e.GetPosition(canvas.ZoomContainerElement)) != null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
    public bool IsNavigationTarget(NavigationContext navigationContext) => true;
    public void OnNavigatedTo(NavigationContext navigationContext) { }
    public void OnNavigatedFrom(NavigationContext navigationContext) { }
}
