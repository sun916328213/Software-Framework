// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：MainWindowViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.UI.Theme、Prism.Regions、WPFCustomControls.Workbench、Vision.Workbench。导入不代表所有类型都会在运行时加载。
using Extension.UI.Theme;
using Prism.Regions;
using WPFCustomControls.Workbench;
using Vision.Workbench;

namespace Vision.Workbench.ViewModels;


/// <summary>Shell navigation and theme binding; the Controls module owns the page.</summary>
/// <remarks>
/// MainWindowViewModel 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
/// 继承或实现的契约：WorkbenchNavigationViewModel&lt;WorkbenchPage&gt;。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class MainWindowViewModel : WorkbenchNavigationViewModel<WorkbenchPage>
{
    public const string MainRegion = "VisionRegion";
    /// <summary>
    /// 初始化 MainWindowViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
    /// </summary>
    public MainWindowViewModel(IRegionManager regions, ThemeService theme)
        : base(regions, theme, MainRegion, [new("视觉工具", nameof(ToolBrowserView))], [new("视觉工具", nameof(ToolBrowserView))])
    {
    }
}
