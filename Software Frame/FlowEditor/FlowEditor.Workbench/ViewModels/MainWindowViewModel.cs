// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：MainWindowViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.UI.Theme、Prism.Regions、WPFCustomControls.Workbench、FlowEditor.Controls.Views。导入不代表所有类型都会在运行时加载。
using Extension.UI.Theme;
using Prism.Regions;
using WPFCustomControls.Workbench;
using FlowEditor.Controls.Views;

namespace FlowEditor.Workbench.ViewModels;


/// <summary>Shell navigation and theme binding; the Controls module owns the page.</summary>
/// <remarks>
/// MainWindowViewModel 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
/// 继承或实现的契约：WorkbenchNavigationViewModel&lt;WorkbenchPage&gt;。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class MainWindowViewModel : WorkbenchNavigationViewModel<WorkbenchPage>
{
    public const string MainRegion = "FlowEditorRegion";
    private readonly IRegionManager _regions;
    /// <summary>
    /// 初始化 MainWindowViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
    /// </summary>
    public MainWindowViewModel(IRegionManager regions, ThemeService theme)
        : base(regions, theme, MainRegion, [new("流程编辑器", nameof(FlowChartContainerView))], [new("流程编辑器", nameof(FlowChartContainerView))])
    {
        _regions = regions;
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnNavigationSucceeded。
    /// 正文中的关键协作调用包括 FirstOrDefault、OfType；具体条件和先后次序见方法体。
    /// </summary>
    protected override void OnNavigationSucceeded(WorkbenchPage page)
    {
        var active = _regions.Regions[MainRegion].ActiveViews.OfType<System.Windows.FrameworkElement>().FirstOrDefault();
        if (active?.DataContext is FlowEditor.Controls.ViewModels.FlowChartContainerViewModel vm)
            vm.ProcessName = "FlowEditorWorkbench";
    }
}
