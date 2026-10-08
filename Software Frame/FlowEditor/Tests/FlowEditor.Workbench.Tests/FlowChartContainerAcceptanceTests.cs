// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FlowChartContainerAcceptanceTests、HeadlessFlowTabFactory、RecordingDialogService、EmptyToolDiscoveryService、SilentPortDialogService、RecordingExportService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、System.Windows.Controls、System.Windows.Automation.Peers、System.Windows.Automation.Provider、System.Windows.Media、System.Windows.Threading。导入不代表所有类型都会在运行时加载。
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;
using System.Windows.Threading;
using FlowEditor.Controls.Behaviors;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.Services;
using FlowEditor.Controls.Services.Storage;
using FlowEditor.Controls.ViewModels;
using MaterialDesignThemes.Wpf;
using FlowEditor.Controls.Views;
using Prism.Mvvm;
using WPFCustomControls.PropertyGrid.Core.Models;
using WPFCustomControls.PropertyGrid.Core.Services;
using WPFCustomControls.PropertyGrid.Services;
using WPFCustomControls.PropertyGrid.Core.ViewModels;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>验证完整流程容器在无宿主窗口环境中的视图与 ViewModel 组合。</summary>
public sealed class FlowChartContainerAcceptanceTests
{
    /// <summary>确保容器、默认标签页、活动画布和三个独立面板能够在 STA 中共同完成布局。</summary>
    [Fact]
    public void Container_WithHeadlessAdapters_ComposesActiveFlowView()
    {
        RunWithContainer((view, viewModel) =>
        {
            var tabHost = Assert.IsAssignableFrom<TabControl>(view.FindName("FlowTabControl"));
            var activeTab = viewModel.ActiveTab;

            Assert.Same(viewModel, view.DataContext);
            Assert.Single(viewModel.Tabs);
            Assert.NotNull(activeTab);
            Assert.Same(activeTab, tabHost.SelectedItem);
            Assert.NotNull(FlowChartContainerView.FindActiveFlowChart(tabHost, activeTab!));
            Assert.NotNull(view.Content);
        });
    }

    /// <summary>确保完整容器中的 PNG 按钮把活动画布、完整边界和 2 倍倍率交给导出服务。</summary>
    [Fact]
    public void ExportPngButton_FromComposedContainer_ExportsActiveCanvas()
    {
        var exportService = new RecordingExportService();

        RunWithContainer((view, viewModel) =>
        {
            var commandBar = Assert.Single(FindVisualDescendants<FlowCommandBarView>(view));
            var button = Assert.IsType<Button>(commandBar.FindName("ExportPngButton"));
            var peer = new ButtonAutomationPeer(button);
            var invokeProvider = Assert.IsAssignableFrom<IInvokeProvider>(
                peer.GetPattern(PatternInterface.Invoke));

            invokeProvider.Invoke();
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            var activeTab = Assert.IsType<FlowChartViewModel>(viewModel.ActiveTab);
            var tabHost = Assert.IsAssignableFrom<TabControl>(view.FindName("FlowTabControl"));
            var flowView = Assert.IsType<FlowChartView>(
                FlowChartContainerView.FindActiveFlowChart(tabHost, activeTab));
            Assert.Equal(1, exportService.PngCalls);
            Assert.Same(flowView.ZoomContainerElement, exportService.Element);
            Assert.Equal(activeTab.GetCanvasExportBounds(), exportService.ContentBounds);
            Assert.Equal(2.0, exportService.Scale);
        }, exportService);
    }

    /// <summary>验证完整容器输出栏的等级过滤、搜索、自动滚动和清除绑定共同生效。</summary>
    [Fact]
    public void OutputPane_FromComposedContainer_FiltersSearchesTogglesAndClears()
    {
        RunWithContainer((view, viewModel) =>
        {
            var pane = Assert.Single(FindVisualDescendants<FlowOutputPaneView>(view));
            var output = Assert.IsType<TextBox>(pane.FindName("OutputTextBox"));
            var search = Assert.IsType<TextBox>(pane.FindName("OutputSearchBox"));
            var level = Assert.IsType<ComboBox>(pane.FindName("OutputLevelFilter"));
            var autoScroll = Assert.IsType<CheckBox>(pane.FindName("AutoScrollToggle"));
            var clear = Assert.IsType<Button>(pane.FindName("ClearOutputButton"));
            var activeTab = Assert.IsType<FlowChartViewModel>(viewModel.ActiveTab);
            activeTab.Model.OutputText =
                "[10:00] 普通信息\n[10:01] [警告] Alpha\n[10:02] [重试] alpha retry\n[10:03] [错误] Beta";

            level.SelectedValue = "Warning";
            search.Text = "ALPHA";
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);

            Assert.Contains("[警告] Alpha", output.Text);
            Assert.Contains("[重试] alpha retry", output.Text);
            Assert.DoesNotContain("普通信息", output.Text);
            Assert.DoesNotContain("[错误] Beta", output.Text);

            autoScroll.IsChecked = false;
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
            Assert.False(TextBoxAutoScrollBehavior.GetIsEnabled(output));

            var peer = new ButtonAutomationPeer(clear);
            var invokeProvider = Assert.IsAssignableFrom<IInvokeProvider>(
                peer.GetPattern(PatternInterface.Invoke));
            invokeProvider.Invoke();
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            Assert.Equal(string.Empty, activeTab.Model.OutputText);
            Assert.Equal(string.Empty, output.Text);
        });
    }

    /// <summary>验证完整容器可添加并连接工具节点，且工具栏保存和打开命令能够恢复画布。</summary>
    [Fact]
    public void Container_ToolNodesAndToolbarCommands_SaveAndReopenFlow()
    {
        const string path = "acceptance.flow";
        var store = new InMemoryFlowDocumentStore();
        var dialogs = new RecordingDialogService(path);

        RunWithContainer((view, viewModel) =>
        {
            var activeTab = Assert.IsType<FlowChartViewModel>(viewModel.ActiveTab);
            activeTab.AddNodeAt(new NodePaletteItem(
                "开始", "流程控制", string.Empty, PackIconKind.Play,
                "测试入口", () => new StartNode { Name = "验收入口" }), new Point(120, 180));
            activeTab.AddNodeAt(new NodePaletteItem(
                "结束", "流程控制", string.Empty, PackIconKind.Stop,
                "测试出口", () => new EndNode { Name = "验收出口" }), new Point(520, 180));
            activeTab.Connections.Add(TestGraph.Flow(activeTab.Nodes[0], activeTab.Nodes[1]));

            var commandBar = Assert.Single(FindVisualDescendants<FlowCommandBarView>(view));
            InvokeButton(commandBar, "保存当前页面流程 (Ctrl+S)");
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            var saved = store.Load<FlowEditor.Core.Models.FlowChartDocumentModel>(path);
            Assert.Equal(2, saved.Nodes.Count);
            Assert.Single(saved.Connections);
            activeTab.Nodes[0].Name = "已修改";
            activeTab.Connections.Clear();

            InvokeButton(commandBar, "打开文件 (Ctrl+O)");
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            Assert.Collection(activeTab.Nodes,
                node =>
                {
                    Assert.IsType<StartNode>(node.Model);
                    Assert.Equal("验收入口_1", node.Name);
                    Assert.Equal(new Point(120, 180), node.Position);
                },
                node =>
                {
                    Assert.IsType<EndNode>(node.Model);
                    Assert.Equal("验收出口_1", node.Name);
                    Assert.Equal(new Point(520, 180), node.Position);
                });
            Assert.Single(activeTab.Connections);
            Assert.Equal(path, activeTab.Model.CurrentFilePath);
            Assert.Equal($"已加载：{path}", activeTab.Model.StatusText);
        }, documentStore: store, dialogService: dialogs);
    }

    /// <summary>
    /// 测试相关入口 NewFlowButton_AndTabSwitch_KeepPropertyPanelOnActiveSelection；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 RunWithContainer、IsType、AddNodeAt、Invoke、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void NewFlowButton_AndTabSwitch_KeepPropertyPanelOnActiveSelection()
    {
        RunWithContainer((view, viewModel) =>
        {
            var first = Assert.IsType<FlowChartViewModel>(viewModel.ActiveTab);
            first.AddNodeAt(new NodePaletteItem(
                "开始", "流程控制", string.Empty, PackIconKind.Play,
                "入口", () => new StartNode { Name = "入口" }), new Point(120, 180));
            first.SelectedNode = first.Nodes[0];
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Same(first.Nodes[0].Model, viewModel.PropertyGridVM.TargetObject);

            var commandBar = Assert.Single(FindVisualDescendants<FlowCommandBarView>(view));
            InvokeButton(commandBar, "新建流程 (Ctrl+N)");
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Equal(2, viewModel.Tabs.Count);
            Assert.NotSame(first, viewModel.ActiveTab);
            Assert.Null(viewModel.PropertyGridVM.TargetObject);

            var tabHost = Assert.IsAssignableFrom<TabControl>(view.FindName("FlowTabControl"));
            tabHost.SelectedItem = first;
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Same(first, viewModel.ActiveTab);
            Assert.Same(first.Nodes[0].Model, viewModel.PropertyGridVM.TargetObject);

            viewModel.ActiveTab = null;
            Assert.Null(viewModel.PropertyGridVM.TargetObject);
        });
    }

    /// <summary>
    /// 测试相关入口 OutputPane_AtNarrowWidth_WrapsControlsWithinAvailableSpace；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、Measure、Arrange、UpdateLayout、IsType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void OutputPane_AtNarrowWidth_WrapsControlsWithinAvailableSpace()
    {
        WpfTestHost.Run(() =>
        {
            var pane = new FlowOutputPaneView();
            pane.Measure(new Size(400, 150));
            pane.Arrange(new Rect(0, 0, 400, 150));
            pane.UpdateLayout();
            var search = Assert.IsType<TextBox>(pane.FindName("OutputSearchBox"));
            var clear = Assert.IsType<Button>(pane.FindName("ClearOutputButton"));
            var searchPosition = search.TranslatePoint(new Point(), pane);
            var clearPosition = clear.TranslatePoint(new Point(), pane);
            Assert.True(searchPosition.Y > clearPosition.Y);
            Assert.InRange(searchPosition.X + search.ActualWidth, 0, pane.ActualWidth);
        });
    }

    /// <summary>
    /// 测试相关入口 ToolbarExecutionButtons_FollowActiveAndBackgroundFlowStates；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 RunWithContainer、Single、FindVisualDescendants、Invoke、ButtonFor；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ToolbarExecutionButtons_FollowActiveAndBackgroundFlowStates()
    {
        RunWithContainer((view, vm) =>
        {
            var bar = Assert.Single(FindVisualDescendants<FlowCommandBarView>(view));
            Button ButtonFor(string tooltip) => Assert.Single(FindVisualDescendants<Button>(bar),
                button => button.ToolTip?.ToString() == tooltip);
            void Flush() => view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var run = ButtonFor("当前流程执行");
            var runAll = ButtonFor("全部运行 (F5)");
            var stop = ButtonFor("全部停止 (Shift+F5)");
            var loop = ButtonFor("循环执行");
            var loopStop = ButtonFor("停止循环");
            Assert.False(run.IsEnabled);
            Assert.False(runAll.IsEnabled);
            Assert.False(stop.IsEnabled);
            Assert.False(loop.IsEnabled);
            Assert.False(loopStop.IsEnabled);

            var first = vm.ActiveTab!;
            first.AddNodeAt(new NodePaletteItem("开始", "流程控制", string.Empty, PackIconKind.Play,
                "入口", () => new StartNode()), new Point(120, 180));
            Flush();
            Assert.True(run.IsEnabled);
            Assert.True(runAll.IsEnabled);
            Assert.True(loop.IsEnabled);

            first.Model.IsRunning = true;
            Flush();
            Assert.False(run.IsEnabled);
            Assert.False(runAll.IsEnabled);
            Assert.True(stop.IsEnabled);
            first.Model.IsLoopRunning = true;
            first.Model.IsRunning = false; // 循环轮次之间的等待间隔。
            Flush();
            Assert.False(run.IsEnabled);
            Assert.False(runAll.IsEnabled);
            Assert.False(loop.IsEnabled);
            Assert.True(loopStop.IsEnabled);
            first.Model.IsLoopRunning = false;
            Flush();
            Assert.True(run.IsEnabled);
            Assert.False(loopStop.IsEnabled);

            InvokeButton(bar, "新建流程 (Ctrl+N)");
            Flush();
            Assert.False(run.IsEnabled);
            Assert.True(runAll.IsEnabled); // 后台标签仍有可执行流程。
            vm.Tabs.Clear();
            Flush();
            Assert.Null(vm.ActiveTab);
            Assert.False(runAll.IsEnabled);
            Assert.False(vm.SaveAllCommand.CanExecute(null));
        });
    }

    /// <summary>
    /// 测试相关入口 ReplacedAndResetTabs_DetachOldSubscriptionsAndDoNotDuplicateNewOnes；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 RunWithContainer、AddNewTab、ReplaceWithFlow、NotifyCanExecuteChanged、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ReplacedAndResetTabs_DetachOldSubscriptionsAndDoNotDuplicateNewOnes()
    {
        RunWithContainer((view, vm) =>
        {
            var retired = vm.ActiveTab!;
            vm.AddNewTab();
            var retained = vm.ActiveTab!;
            vm.ReplaceWithFlow(retained);
            vm.ReplaceWithFlow(retained);
            var changes = 0;
            vm.RunAllCommand.CanExecuteChanged += (_, _) => changes++;
            retained.RunCommand.NotifyCanExecuteChanged();
            Assert.Equal(1, changes);
            changes = 0;
            retired.RunCommand.NotifyCanExecuteChanged();
            retired.Model.IsLoopRunning = true;
            Assert.Equal(0, changes);

            vm.Tabs.Clear();
            changes = 0;
            retained.RunCommand.NotifyCanExecuteChanged();
            Assert.Equal(0, changes);
            Assert.Null(vm.PropertyGridVM.TargetObject);
        });
    }

    /// <summary>
    /// 测试相关入口 PendingConnectionRender_WhenContextCleared_DoesNotAccessRetiredFlow；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、CreateContainerViewModel、Invoke、IsType、FindName；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PendingConnectionRender_WhenContextCleared_DoesNotAccessRetiredFlow()
    {
        WpfTestHost.Run(() =>
        {
            var vm = CreateContainerViewModel();
            var canvas = new FlowChartView { DataContext = vm.ActiveTab };
            canvas.DataContext = null;
            canvas.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var connections = Assert.IsType<Canvas>(canvas.FindName("ConnectionCanvas"));
            Assert.Empty(connections.Children.Cast<UIElement>());
        });
    }

    /// <summary>
    /// 测试相关入口 Canvas_UnloadAndReload_DetachesVisualSubscriptionsAndRestoresCurrentFlow；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、CreateContainerViewModel、AddNewTab、Invoke、AddNodeAt；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Canvas_UnloadAndReload_DetachesVisualSubscriptionsAndRestoresCurrentFlow()
    {
        WpfTestHost.Run(() =>
        {
            var container = CreateContainerViewModel();
            var first = container.ActiveTab!;
            container.AddNewTab();
            var second = container.ActiveTab!;
            var canvas = new FlowChartView { DataContext = first };
            var shutdownMode = Application.Current.ShutdownMode;
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var host = new Window { Width = 800, Height = 600, ShowInTaskbar = false, Content = canvas };
            void Flush() => canvas.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            void AddStart(FlowChartViewModel tab) => tab.AddNodeAt(new NodePaletteItem(
                "开始", "流程控制", string.Empty, PackIconKind.Play, "入口", () => new StartNode()), new Point(120, 180));
            var nodes = Assert.IsType<Canvas>(canvas.FindName("FlowChartCanvas"));
            try
            {
                host.Show();
                Flush();
                Assert.True(canvas.IsLoaded);
                AddStart(first);
                Flush();
                Assert.Single(nodes.Children.OfType<NodeView>());

                host.Content = null;
                Flush();
                Assert.False(canvas.IsLoaded);
                Assert.Empty(nodes.Children.OfType<NodeView>());
                AddStart(first);
                first.Model.IsRunning = true;
                canvas.DataContext = second;
                AddStart(second);
                Flush();
                Assert.Empty(nodes.Children.OfType<NodeView>());
                Assert.True(first.Model.IsRunning); // 页面卸载不停止共享会话。
                first.Model.IsRunning = false;

                host.Content = canvas;
                Flush();
                Assert.True(canvas.IsLoaded);
                Assert.Single(nodes.Children.OfType<NodeView>());
                AddStart(first);
                Flush();
                Assert.Single(nodes.Children.OfType<NodeView>());
                AddStart(second);
                Flush();
                Assert.Equal(2, nodes.Children.OfType<NodeView>().Count());
            }
            finally
            {
                host.Close();
                Flush();
                Application.Current.ShutdownMode = shutdownMode;
            }
        });
    }

    /// <summary>在共享 STA 宿主内组合容器并执行断言，随后恢复 Prism 全局 factory。</summary>
    private static void RunWithContainer(
        Action<FlowChartContainerView, FlowChartContainerViewModel> assertion,
        IExportService? exportService = null,
        IFlowDocumentStore? documentStore = null,
        IDialogService? dialogService = null)
    {
        WpfTestHost.Run(() =>
        {
            var viewModel = CreateContainerViewModel(exportService, documentStore, dialogService);
            ViewModelLocationProvider.SetDefaultViewModelFactory(type =>
                type == typeof(FlowChartContainerViewModel)
                    ? viewModel
                    : Activator.CreateInstance(type)!);
            try
            {
                var view = new FlowChartContainerView();
                view.Measure(new Size(1600, 900));
                view.Arrange(new Rect(0, 0, 1600, 900));
                view.UpdateLayout();
                view.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                assertion(view, viewModel);
            }
            finally
            {
                RestoreDefaultViewModelFactory();
            }
        });
    }

    /// <summary>恢复 Prism 的默认无参 ViewModel 创建行为，避免测试间共享全局 factory。</summary>
    private static void RestoreDefaultViewModelFactory() =>
        ViewModelLocationProvider.SetDefaultViewModelFactory(type => Activator.CreateInstance(type)!);

    /// <summary>使用内存存储、空工具发现和静默属性对话框创建完整容器 ViewModel。</summary>
    private static FlowChartContainerViewModel CreateContainerViewModel(
        IExportService? exportService = null,
        IFlowDocumentStore? documentStore = null,
        IDialogService? dialogService = null)
    {
        var store = documentStore ?? new InMemoryFlowDocumentStore();
        var dialogs = dialogService ?? TestFlowEditorFactory.Dialogs;
        var tabFactory = new HeadlessFlowTabFactory(exportService, store, dialogs);
        var nodeFactory = new ReflectionNodeFactory();
        var documentService = new FlowChartContainerDocumentService(
            new FlowDocumentMapper(nodeFactory),
            store,
            tabFactory);
        var propertyGrid = new PropertyGridHostViewModel(
            new PropertyGridBuilderService(),
            new SilentPortDialogService());

        return new FlowChartContainerViewModel(
            dialogs,
            tabFactory,
            propertyGrid,
            documentService,
            new ToolboxService(new EmptyToolDiscoveryService(), nodeFactory));
    }

    /// <summary>为容器创建带完整无头依赖的流程标签页。</summary>
    private sealed class HeadlessFlowTabFactory : IFlowTabFactory
    {
        private readonly IExportService? _exportService;
        private readonly IFlowDocumentStore _documentStore;
        private readonly IDialogService _dialogService;

        /// <summary>创建可选择记录导出调用的无头标签页 factory。</summary>
        public HeadlessFlowTabFactory(
            IExportService? exportService,
            IFlowDocumentStore documentStore,
            IDialogService dialogService)
        {
            _exportService = exportService;
            _documentStore = documentStore;
            _dialogService = dialogService;
        }

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 Create；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public FlowChartViewModel Create() => new(
            new FlowChartService(
                _documentStore,
                new FlowChartExecutionEngine(),
                _dialogService),
            _exportService ?? TestFlowEditorFactory.Exports,
            _dialogService,
            new ConnectionService());
    }

    /// <summary>调用具有指定工具提示的真实按钮。</summary>
    private static void InvokeButton(DependencyObject parent, string toolTip)
    {
        var button = Assert.Single(FindVisualDescendants<Button>(parent),
            candidate => string.Equals(candidate.ToolTip?.ToString(), toolTip, StringComparison.Ordinal));
        var peer = new ButtonAutomationPeer(button);
        var invokeProvider = Assert.IsAssignableFrom<IInvokeProvider>(
            peer.GetPattern(PatternInterface.Invoke));
        invokeProvider.Invoke();
    }

    /// <summary>为保存和打开命令返回固定路径的无窗口对话框 adapter。</summary>
    private sealed class RecordingDialogService : IDialogService
    {
        private readonly string _path;

        /// <summary>创建始终选择指定路径的对话框 adapter。</summary>
        public RecordingDialogService(string path) => _path = path;

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 ShowOpenFileDialog；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public string? ShowOpenFileDialog(string filter, string? defaultFileName = null) => _path;

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 ShowSaveFileDialog；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public string? ShowSaveFileDialog(string filter, string defaultFileName, string defaultExt) => _path;

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 ShowConfirm；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public bool? ShowConfirm(string message, string title) => false;

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 ConfirmYesNo；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public bool ConfirmYesNo(string message, string title) => false;

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 ShowInfo；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public void ShowInfo(string message, string title = "提示")
        {
        }

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 ShowError；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public void ShowError(string message, string title = "错误") =>
            throw new InvalidOperationException($"{title}: {message}");
    }

    /// <summary>为组合冒烟测试提供确定性的空工具集合。</summary>
    private sealed class EmptyToolDiscoveryService : IFlowToolDiscoveryService
    {
        /// <inheritdoc />
        public IReadOnlyList<ToolMetadata> DiscoveredTools { get; } = Array.Empty<ToolMetadata>();

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 Discover；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public int Discover() => 0;

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 GetGroupedTools；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Empty；具体条件和先后次序见方法体。
        /// </remarks>
        public IEnumerable<IGrouping<string, ToolMetadata>> GetGroupedTools() =>
            Array.Empty<IGrouping<string, ToolMetadata>>();

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 Search；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public List<ToolMetadata> Search(string keyword) => [];
    }

    /// <summary>在验收测试中抑制属性面板端口编辑窗口。</summary>
    private sealed class SilentPortDialogService : IPortDialogService
    {
        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 ShowAddPortDialog；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public PortDialogResult? ShowAddPortDialog(PortDialogRequest request) => null;

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 ShowModifyBindingDialog；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public PortDialogResult? ShowModifyBindingDialog(PortDialogRequest request) => null;
    }

    /// <summary>记录 PNG 导出参数，避免验收测试打开真实文件对话框。</summary>
    private sealed class RecordingExportService : IExportService
    {
        /// <summary>PNG 导出调用次数。</summary>
        public int PngCalls { get; private set; }

        /// <summary>最后一次导出的视觉元素。</summary>
        public FrameworkElement? Element { get; private set; }

        /// <summary>最后一次导出的内容边界。</summary>
        public Rect ContentBounds { get; private set; }

        /// <summary>最后一次导出的缩放倍率。</summary>
        public double Scale { get; private set; }

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 ExportToPngWithDialog；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public bool ExportToPngWithDialog(
            FrameworkElement element,
            Rect contentBounds,
            double scale = 2.0)
        {
            PngCalls++;
            Element = element;
            ContentBounds = contentBounds;
            Scale = scale;
            return true;
        }

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 ExportToSvgWithDialog；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public bool ExportToSvgWithDialog(FrameworkElement element) => false;
    }

    /// <summary>递归枚举视觉树中的指定类型元素。</summary>
    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
                yield return match;

            foreach (var descendant in FindVisualDescendants<T>(child))
                yield return descendant;
        }
    }
}
