using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlowEditor.Controls.Core;
using FlowEditor.Controls.Discovery;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Nodes;
using FlowEditor.Controls.Services;
using FlowEditor.Controls.Services.Storage;
using FlowEditor.Controls.ViewModels;
using FlowEditor.Controls.Views;
using FlowEditor.Runtime;
using Prism.Ioc;
using Vision.Controls.Services;
using Vision.Controls.Models;
using Vision.FlowNode;


namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionFlowToolboxTests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task LeftToolbox_ReusesGenericView_AndRemovesDuplicateTopAddControls()
    {
        var vm = host.Invoke(Create);
        host.Invoke(() =>
        {
            var page = new VisionFlowView { DataContext = vm };
            var window = new Window { Content = page, Width = 1600, Height = 850, ShowInTaskbar = false, Left = -10000 };
            try
            {
                window.Show(); Idle(page);
                var toolbox = Assert.Single(Children(page).OfType<FlowToolboxView>());
                Assert.True(toolbox.IsVisible);
                Assert.DoesNotContain(Children(page).OfType<Button>(), b => Equals(b.Content, "添加工具节点"));
                Assert.DoesNotContain(Children(page).OfType<ComboBox>(), c => ReferenceEquals(c.ItemsSource, vm.ToolEntries));
                var canvas = (FrameworkElement)page.FindName("FlowCanvas");
                var editor = Assert.IsType<WPFCustomControls.PropertyGrid.Views.PropertyGridHost>(page.FindName("NodeProperties"));
                Assert.Same(vm.Chart.SelectedNode?.Model, ((WPFCustomControls.PropertyGrid.Core.ViewModels.PropertyGridHostViewModel)editor.DataContext).TargetObject);
                Assert.Null(page.FindName("ToolEditor"));
                Assert.True(toolbox.TranslatePoint(new Point(toolbox.ActualWidth, 0), page).X <= canvas.TranslatePoint(new Point(), page).X);
                Assert.True(canvas.TranslatePoint(new Point(canvas.ActualWidth, 0), page).X <= editor.TranslatePoint(new Point(), page).X);
                Assert.Empty(vm.Chart.Nodes);
                window.Width = 900; Idle(page);
                Assert.Single(Children(page).OfType<FlowChartContainerView>());
                Assert.Single(Children(page).OfType<FlowCommandBarView>());
                Assert.Null(page.FindName("ToolboxPanel")); // 复用原容器分栏，不再维护视觉专属收起布局。
            }
            finally { window.Close(); }
        });
        await host.Invoke(() => vm.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task CatalogSearchAndAdd_UseOriginalFactories_AndLockOnlyModification()
    {
        var vm = host.Invoke(Create);
        try
        {
            host.Invoke(() =>
            {
                Assert.Equal(35, vm.ToolboxCategories.Sum(c => c.TotalCount));
                Assert.Equal(new[] { "流程控制", "OpenCV", "HALCON", "AI", "Common" }, vm.ToolboxCategories.Select(c => c.Name));
                Assert.Empty(vm.Chart.Nodes);
                vm.ToolboxSearchText = " opencv/crop ";
                var crop = Assert.Single(Assert.Single(vm.ToolboxCategories).Items);
                Assert.Equal("OpenCV/Crop", crop.DisplayName);
                var toolbox = new FlowToolboxView { AddNodeCommand = vm.AddPaletteCommand, DataContext = vm };
                toolbox.AddNodeCommand.Execute(crop);
                var first = Assert.IsAssignableFrom<VisionToolNodeBase>(Assert.Single(vm.Chart.Nodes).Model);
                var second = vm.AddPaletteAt(crop, new Point(2400, 1700))!;
                Assert.Equal(2, vm.Chart.Nodes.Count); Assert.NotSame(first.Session.Parameters, second.Session.Parameters);
                Assert.Equal(2400, second.X); Assert.Equal(1700, second.Y);
                Assert.True(vm.Chart.Model.CanvasWidth > second.X); Assert.True(vm.Chart.Model.CanvasHeight > second.Y);
                Assert.Same(second, vm.Chart.SelectedNode!.Model);
                Assert.Same(((VisionToolInstanceWorkspace)second.Session).Editor, vm.SelectedEditor);
                vm.Chart.Model.IsRunning = true;
                Assert.False(toolbox.AddNodeCommand.CanExecute(crop));
                toolbox.AddNodeCommand.Execute(crop); Assert.Null(vm.AddPaletteAt(crop)); Assert.Equal(2, vm.Chart.Nodes.Count);
                vm.ToolboxSearchText = "FindCircle"; Assert.Single(Assert.Single(vm.ToolboxCategories).Items);
                vm.Chart.Model.IsRunning = false; Assert.True(toolbox.AddNodeCommand.CanExecute(crop));
                vm.ToolboxSearchText = "no-such-tool"; Assert.Empty(vm.ToolboxCategories);
                vm.ToolboxSearchText = ""; Assert.Equal(35, vm.ToolboxCategories.Sum(c => c.TotalCount));
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
        host.Invoke(() => Assert.False(vm.AddPaletteCommand.CanExecute(vm.ToolboxCategories[0].Items[0])));
    }

    internal static VisionFlowViewModel Create() => Create(null);
    internal static VisionFlowViewModel Create(Action? created, Func<VisionBrowserCatalog, VisionBrowserCatalog>? configure = null)
    {
        var entries = OpenCvToolCatalog.Entries.Concat(HalconToolCatalog.Entries).Concat(AIToolCatalog.Entries).Concat(CommonToolCatalog.Entries).ToArray();
        if (created != null) entries = entries.Select(e => e with { Create = () => { created(); return e.Create(); } }).ToArray();
        var catalog = new VisionBrowserCatalog(entries, [], _ => throw new NotSupportedException(), () => null);
        if (configure != null) catalog = configure(catalog);
        var nodes = new FlowNodeCatalog([typeof(NodeModel).Assembly, typeof(VisionToolNodeBase).Assembly]);
        var factory = new VisionNodeFactory(c => new VisionToolInstanceWorkspace(catalog, catalog.Tools.Single(t => t.Name == c.ToolIdentity), c.Provider), new ReflectionNodeFactory(null, nodes), nodes);
        var dialogs = new Dialogs();
        var service = new FlowChartService(new VisionTaskDocumentStore(new JsonFlowDocumentStore()), new FlowChartExecutionEngine(), dialogs,
            nodeEditorLauncher: ContainerLocator.Container.Resolve<INodeEditorLauncher>(), uiDispatcher: new FlowEditor.Controls.Presentation.Threading.WpfUiDispatcher(), documentMapper: new FlowDocumentMapper(factory, nodes));
        return new(new FlowChartViewModel(service, ContainerLocator.Container.Resolve<IExportService>(), dialogs, new ConnectionService()), catalog, true);
    }
    private static void Idle(FrameworkElement page) { page.UpdateLayout(); page.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); }
    private static IEnumerable<DependencyObject> Children(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Children(child)) yield return nested;
        }
    }
    private sealed class Dialogs : IDialogService
    {
        public string? ShowOpenFileDialog(string filter, string? defaultFileName = null) => null;
        public string? ShowSaveFileDialog(string filter, string defaultFileName, string defaultExt) => null;
        public bool? ShowConfirm(string message, string title) => false; public bool ConfirmYesNo(string message, string title) => false;
        public void ShowInfo(string message, string title = "提示") { } public void ShowError(string message, string title = "错误") => throw new InvalidOperationException(message);
    }
}
