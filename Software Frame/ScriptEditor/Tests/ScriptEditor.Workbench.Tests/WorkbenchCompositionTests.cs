// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorkbenchCompositionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Controls、ScriptEditor.Workbench、System.Linq、System.Windows、Prism.Ioc、ScriptEditor.Controls.ViewModels。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Controls;
using ScriptEditor.Workbench;
using System.Linq;
using System.Windows;
using Prism.Ioc;
using ScriptEditor.Controls.ViewModels;
using ScriptEditor.Controls.Views;

namespace ScriptEditor.Workbench.Tests;

/// <summary>
/// 宿主装配冒烟测试：生产入口拉起主窗、ScriptEditorModule 资源合并跨程序集可解析、
/// 页面 ViewModel 与 UserControl 能在真实 DI 容器下组装并绑定。
/// 这些装配问题编译期不报错，只有把宿主真正拉起来才暴露；不涉及真实脚本执行。
/// </summary>
[Collection("WorkbenchAppBoot")]
public sealed class WorkbenchCompositionTests(WorkbenchAppFixture host)
{
    private readonly WorkbenchAppFixture _host = host;

    /// <summary>
    /// 测试相关入口 AppBoot_ShowsShell_WithAutoWiredViewModel；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、NotNull、Resolve、IsType、Single；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AppBoot_ShowsShell_WithAutoWiredViewModel()
    {
        _host.Invoke(() =>
        {
            Assert.NotNull(Application.Current);

            var window = Application.Current!.MainWindow;
            Assert.NotNull(window);

            var region = ContainerLocator.Container.Resolve<Prism.Regions.IRegionManager>().Regions["ScriptRegion"];
            var shell = Assert.IsType<ScriptManagerView>(Assert.Single(region.ActiveViews));
            Assert.Equal(WindowStartupLocation.CenterScreen, window!.WindowStartupLocation);

            // DataContext 由 XAML 的 prism:ViewModelLocator.AutoWireViewModel 经宿主容器工厂解析，
            // 代码里没有显式 new —— 这条断言守住该约定一旦被破坏。
            Assert.IsType<ScriptManagerViewModel>(shell.DataContext);
        });
    }

    /// <summary>
    /// 测试相关入口 AppBoot_MergesModuleResources；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、ToArray、Where、Select、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AppBoot_MergesModuleResources()
    {
        _host.Invoke(() =>
        {
            var sources = Application.Current!.Resources.MergedDictionaries
                .Select(dictionary => dictionary.Source?.AbsoluteUri)
                .Where(uri => uri is not null)
                .ToArray();

            // ScriptEditorModule.OnInitialized 合并两条：WPFCustomControls 公共资源 + 查找替换面板样式。
            Assert.True(sources.Length >= 2, $"ScriptEditorModule 的资源合并只解析出 {sources.Length} 条。");
            Assert.Contains(sources, uri => uri!.Contains(
                "/WPFCustomControls;component/Resources/Resources.xaml", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(sources, uri => uri!.Contains(
                "/ScriptEditor;component/Resources/SearchReplacePanel.xaml", StringComparison.OrdinalIgnoreCase));
        });
    }

    /// <summary>
    /// 测试相关入口 PackUri_ResolvesBaml_FromForeignEntryAssembly；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、GetResourceStream、NotNull；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData("pack://application:,,,/ScriptEditor;component/UI/Views/ScriptManagerView.xaml")]
    [InlineData("pack://application:,,,/ScriptEditor;component/UI/Views/ScriptEditorView.xaml")]
    [InlineData("pack://application:,,,/ScriptEditor;component/Resources/SearchReplacePanel.xaml")]
    [InlineData("pack://application:,,,/WPFCustomControls;component/Resources/Resources.xaml")]
    public void PackUri_ResolvesBaml_FromForeignEntryAssembly(string uri)
    {
        _host.Invoke(() =>
        {
            // ⛔ 只解析资源流、不实例化：pack URI 可解析性单独钉住，
            //    与页面实例化（Shell_ResolvesComposedViewModelFromHostContainer）解耦。
            var resource = Application.GetResourceStream(new Uri(uri, UriKind.Absolute));
            Assert.NotNull(resource);
        });
    }

    /// <summary>
    /// 测试相关入口 Shell_ResolvesComposedViewModelFromHostContainer；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Same、NotNull；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Shell_ResolvesComposedViewModelFromHostContainer()
    {
        _host.Invoke(() =>
        {
            // 宿主容器注册项少一个（ScriptManagerService / ScriptEditorService / IScriptRuntime ...），
            // 这里就构造失败 —— 这正是「DI 装配」的验收点。
            var container = ContainerLocator.Container;
            var viewModel = container.Resolve<ScriptManagerViewModel>();

            var view = new ScriptManagerView { DataContext = viewModel };

            Assert.Same(viewModel, view.DataContext);
            Assert.NotNull(viewModel.Model);
            Assert.NotNull(viewModel.Items);
        });
    }
}
