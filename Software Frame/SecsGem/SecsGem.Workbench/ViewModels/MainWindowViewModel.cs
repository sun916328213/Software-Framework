// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：MainWindowViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.UI.Theme、Prism.Regions、WPFCustomControls.Workbench、SecsGem.Workbench。导入不代表所有类型都会在运行时加载。
using Extension.UI.Theme;
using Prism.Regions;
using WPFCustomControls.Workbench;
using SecsGem.Controls.Views;
using SecsGem.Workbench.Views;

namespace SecsGem.Workbench.ViewModels;


/// <summary>通信统一使用 Controls，Workbench 提供配方管理和手动切换。</summary>
/// <remarks>
/// MainWindowViewModel 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
/// 继承或实现的契约：WorkbenchNavigationViewModel&lt;WorkbenchPage&gt;。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class MainWindowViewModel : WorkbenchNavigationViewModel<WorkbenchPage>
{
    public const string MainRegion = "SecsRegion";
    /// <summary>
    /// 初始化 MainWindowViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
    /// </summary>
    public MainWindowViewModel(IRegionManager regions, ThemeService theme)
        : base(regions, theme, MainRegion,
            [new("SECS/GEM", nameof(SecsView)), new("配方管理", nameof(RecipeTestView))],
            [new("SECS/GEM", nameof(SecsView)), new("配方管理", nameof(RecipeTestView))])
    {
    }
}
