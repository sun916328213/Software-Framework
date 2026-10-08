// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorkbenchCollection、WorkbenchFixture、WorkbenchCompositionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、System.Windows.Media、System.Windows.Media.Imaging、System.Windows.Threading、FlowEditor.Controls.Discovery、FlowEditor.Controls.Core。导入不代表所有类型都会在运行时加载。
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FlowEditor.Controls.Discovery;
using FlowEditor.Controls.Core;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Services;
using FlowEditor.Controls.ViewModels;
using FlowNodes.Integration;
using FlowNodes.Presentation.NodeEditing;
using FlowNodes.Script;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using ScriptEditor.Core.Contracts;
using Xunit;

namespace FlowNodes.Workbench.Tests;

/// <summary>
/// WorkbenchCollection 的类型定义。FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// 继承或实现的契约：ICollectionFixture&lt;WorkbenchFixture&gt;。具体约束按这些契约及各成员实现解释。
/// </summary>
[CollectionDefinition("ActualWorkbench", DisableParallelization = true)]
public sealed class WorkbenchCollection : ICollectionFixture<WorkbenchFixture> { }

/// <summary>
/// WorkbenchFixture 的类型定义。FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// 继承或实现的契约：IDisposable。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class WorkbenchFixture : IDisposable
{
    private readonly ManualResetEventSlim _ready = new();
    private readonly Thread _thread;
    private Exception? _fault;
    private App? _app;

    /// <summary>
    /// 初始化 WorkbenchFixture 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent、BeginInvoke、Run、Set、SetApartmentState；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public WorkbenchFixture()
    {
        _thread = new Thread(() =>
        {
            try
            {
                _app = new App();
                _app.InitializeComponent();
                _app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                _app.Startup += (_, _) => _app.Dispatcher.BeginInvoke(
                    new Action(_ready.Set), DispatcherPriority.ApplicationIdle);
                _app.Run();
            }
            catch (Exception error) { _fault = error; _ready.Set(); }
        }) { IsBackground = true };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        if (!_ready.Wait(TimeSpan.FromSeconds(60))) throw new TimeoutException("FlowNodes Workbench startup timed out.");
        if (_fault != null) throw new InvalidOperationException("Actual FlowNodes Workbench failed to start.", _fault);
    }

    /// <summary>
    /// 测试相关入口 Invoke；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、action；具体条件和先后次序见方法体。
    /// </summary>
    public void Invoke(Action<IContainerProvider> action) => _app!.Dispatcher.Invoke(() => action(_app.Container));

    /// <summary>
    /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Shutdown、Join、FromSeconds、Dispose；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose()
    {
        _app?.Dispatcher.Invoke(() => _app.Shutdown());
        _thread.Join(TimeSpan.FromSeconds(15));
        _ready.Dispose();
    }
}

/// <summary>
/// WorkbenchCompositionTests 的类型定义。FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("ActualWorkbench")]
public sealed class WorkbenchCompositionTests(WorkbenchFixture host)
{
    /// <summary>
    /// 测试相关入口 ActualAppLoadsModulesAndRendersTheNavigatedEditor；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、ToArray、Resolve、Equal、All；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ActualAppLoadsModulesAndRendersTheNavigatedEditor()
    {
        host.Invoke(container =>
        {
            var modules = container.Resolve<IModuleCatalog>().Modules.ToArray();
            Assert.Equal(6, modules.Length);
            Assert.All(modules, module => Assert.Equal(ModuleState.Initialized, module.State));
            Assert.IsType<Views.MainWindow>(Application.Current.MainWindow);
            var region = container.Resolve<IRegionManager>().Regions["FlowRegion"];
            var view = Assert.IsAssignableFrom<FrameworkElement>(Assert.Single(region.ActiveViews));
            var model = Assert.IsType<FlowChartContainerViewModel>(view.DataContext);
            Assert.Equal("FlowNodesWorkbench", model.ProcessName);
            view.Measure(new Size(1000, 700));
            view.Arrange(new Rect(0, 0, 1000, 700));
            view.UpdateLayout();
            var bitmap = new RenderTargetBitmap(1000, 700, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(view);
            Assert.Equal(1000, bitmap.PixelWidth);
        });
    }

    /// <summary>
    /// 测试相关入口 ActualCatalogToolboxFactoryAndEditorUseTheSharedComposition；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Equal、CreateNodeCatalog、Empty；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ActualCatalogToolboxFactoryAndEditorUseTheSharedComposition()
    {
        host.Invoke(container =>
        {
            var catalog = container.Resolve<IFlowNodeCatalog>();
            Assert.Equal(FlowNodesComposition.CreateNodeCatalog().Nodes, catalog.Nodes);
            Assert.Empty(catalog.Diagnostics);
            var tools = container.Resolve<IFlowToolDiscoveryService>().DiscoveredTools;
            Assert.Equal(44, tools.Count);
            Assert.Contains(container.Resolve<IEnumerable<INodeEditorHandler>>(), h => h is FlowNodesEditorAdapter);
            var factory = container.Resolve<INodeFactory>();
            Assert.All(tools, tool => Assert.Equal(tool.ToolType, factory.Create(tool.ToolType).GetType()));
            var node = Assert.IsType<ScriptNode>(factory.Create(typeof(ScriptNode)));
            var window = new FlowNodesEditorAdapter().CreateWindow(node);
            try { Assert.Equal("脚本编辑器 - " + node.Name, window.Title); }
            finally { window.Close(); }
        });
    }

    /// <summary>
    /// 测试相关入口 ActualScriptRuntimeExecutesThroughTheWorkbenchNodeFactory；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、IsType、Create、NotNull；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ActualScriptRuntimeExecutesThroughTheWorkbenchNodeFactory()
    {
        ScriptNode? node = null;
        IScriptRuntime? runtime = null;
        host.Invoke(container =>
        {
            runtime = container.Resolve<IScriptRuntime>();
            node = Assert.IsType<ScriptNode>(container.Resolve<INodeFactory>().Create(typeof(ScriptNode)));
            node.EditorState.Code = "return 6 * 7;";
        });
        Assert.NotNull(runtime);
        await node!.ExecuteAsync(CancellationToken.None);
        Assert.Equal(42, node.ScriptResult);
    }
}
