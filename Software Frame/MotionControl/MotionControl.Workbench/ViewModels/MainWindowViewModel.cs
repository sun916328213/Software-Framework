// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：NavItem、MainWindowViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.UI.Theme、Prism.Regions、WPFCustomControls.Workbench、MotionControl.Controls.Views。导入不代表所有类型都会在运行时加载。
using Extension.UI.Theme;
using Prism.Regions;
using WPFCustomControls.Workbench;
using MotionControl.Controls.Views;

namespace MotionControl.Workbench.ViewModels;

/// <summary>
/// NavItem 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
/// 继承或实现的契约：IWorkbenchPage。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed record NavItem(string Title, string ViewName) : IWorkbenchPage;

/// <summary>Shell navigation and theme binding; the Controls module owns the page.</summary>
/// <remarks>
/// MainWindowViewModel 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
/// 继承或实现的契约：WorkbenchNavigationViewModel&lt;NavItem&gt;。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class MainWindowViewModel : WorkbenchNavigationViewModel<NavItem>
{
    public const string MainRegion = "MotionRegion";
    /// <summary>
    /// 初始化 MainWindowViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
    /// </summary>
    public MainWindowViewModel(IRegionManager regions, ThemeService theme)
        : base(regions, theme, MainRegion, [new("运动控制", nameof(MotionView))], [new("运动控制", nameof(MotionView))])
    {
    }
}
