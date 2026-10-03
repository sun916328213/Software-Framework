// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：NavItem、MainWindowViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.ObjectModel、DataManager.Workbench.Infrastructure、Extension.UI.Theme、Prism.Regions、WPFCustomControls.Workbench。导入不代表所有类型都会在运行时加载。
using System.Collections.ObjectModel;
using DataManager.Workbench.Infrastructure;
using Extension.UI.Theme;
using Prism.Regions;
using WPFCustomControls.Workbench;

namespace DataManager.Workbench.ViewModels;

/// <summary>
/// NavItem 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
/// 继承或实现的契约：IWorkbenchPage。具体约束按这些契约及各成员实现解释。
/// </summary>
public class NavItem : IWorkbenchPage
{
    public string Title { get; }
    public string Badge { get; }
    public string ViewName { get; }

    /// <summary>
    /// 初始化 NavItem 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public NavItem(string title, string badge, string viewName)
    {
        Title = title;
        Badge = badge;
        ViewName = viewName;
    }
}

/// <summary>Shell navigation; page state and device resources belong to Controls.</summary>
/// <remarks>
/// MainWindowViewModel 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
/// 继承或实现的契约：WorkbenchNavigationViewModel&lt;NavItem&gt;。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed class MainWindowViewModel : WorkbenchNavigationViewModel<NavItem>
{
    /// <summary>
    /// 初始化 MainWindowViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 LoadPages、DefaultPages；具体条件和先后次序见方法体。
    /// </summary>
    public MainWindowViewModel(IRegionManager regions, ThemeService theme)
        : base(regions, theme, RegionNames.MainRegion, LoadPages(), DefaultPages()) { }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 DefaultPages。
    /// 正文中的关键协作调用包括 Select；具体条件和先后次序见方法体。
    /// </summary>
    private static IEnumerable<NavItem> DefaultPages() => NavPageConfig.DefaultPages
        .Select(p => new NavItem(p.Title, p.Badge, p.ViewName));

    /// <summary>
    /// 执行加载操作；对应入口为 LoadPages。
    /// 正文中的关键协作调用包括 Select、Where、Read、ToList；具体条件和先后次序见方法体。
    /// </summary>
    private static IEnumerable<NavItem> LoadPages() => (NavPageConfig.Read() ?? NavPageConfig.DefaultPages.ToList())
        .Where(p => p is not null)
        .Select(p => new NavItem(p.Title?.Trim() ?? "", p.Badge ?? "", p.ViewName?.Trim() ?? ""));
}
