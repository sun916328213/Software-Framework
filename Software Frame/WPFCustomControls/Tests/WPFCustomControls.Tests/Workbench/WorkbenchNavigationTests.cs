// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorkbenchNavigationTests、Shell、RegionProxy。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Reflection、Extension.UI.Theme、Prism.Regions、WPFCustomControls.Workbench。导入不代表所有类型都会在运行时加载。
using System.Reflection;
using Extension.UI.Theme;
using Prism.Regions;
using WPFCustomControls.Workbench;

namespace WPFCustomControls.Tests.Workbench;

/// <summary>
/// WorkbenchNavigationTests 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class WorkbenchNavigationTests
{
    /// <summary>
    /// 测试相关入口 FailedNavigation_RestoresConfirmedSelectionAndReportsError；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Create、SelectFirst、Complete、Same、NotEmpty；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void FailedNavigation_RestoresConfirmedSelectionAndReportsError()
    {
        var (shell, proxy) = Create();
        shell.SelectFirst();
        proxy.Complete(true);
        var confirmed = shell.SelectedNav;
        shell.SelectedNav = shell.NavItems[1];
        proxy.Complete(false);
        Assert.Same(confirmed, shell.SelectedNav);
        Assert.NotEmpty(shell.NavigationError);
        Assert.False(shell.IsNavigating);
    }

    /// <summary>
    /// 测试相关入口 PendingNavigation_PreventsAnotherRequest_AndLateCallbackCannotReopenClosedShell；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Create、SelectFirst、Equal、False、Dispose；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PendingNavigation_PreventsAnotherRequest_AndLateCallbackCannotReopenClosedShell()
    {
        var (shell, proxy) = Create();
        shell.SelectFirst();
        shell.SelectedNav = shell.NavItems[1];
        Assert.Equal(1, proxy.Requests);
        Assert.False(shell.CanSelectNavigation);
        shell.Dispose();
        proxy.Complete(false);
        Assert.Empty(shell.NavigationError);
        Assert.False(shell.CanSelectNavigation);
    }

    /// <summary>
    /// 测试相关入口 Catalog_RejectsUnknownPagesAndDuplicates_AndFallsBackWhenEmpty；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Create、Equal、Single；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Catalog_RejectsUnknownPagesAndDuplicates_AndFallsBackWhenEmpty()
    {
        var (shell, _) = Create([new("unknown", "Unknown"), new("first", "First"), new("duplicate", "First")]);
        Assert.Equal("First", Assert.Single(shell.NavItems).ViewName);
        var (fallback, _) = Create([new("unknown", "Unknown")]);
        Assert.Equal(2, fallback.NavItems.Count);
    }

    /// <summary>
    /// 测试相关入口 SelectFirst_RetriesMissingRegion_AndDoesNotRenavigateConfirmedPage；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Create、SelectFirst、Null、NotEmpty、Add；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void SelectFirst_RetriesMissingRegion_AndDoesNotRenavigateConfirmedPage()
    {
        var (shell, proxy) = Create(regionReady: false);
        shell.SelectFirst();
        Assert.Null(shell.SelectedNav);
        Assert.NotEmpty(shell.NavigationError);
        proxy.Regions.Add(new Region { Name = "TestRegion" });
        shell.SelectFirst();
        proxy.Complete(true);
        shell.SelectFirst();
        Assert.Equal(1, proxy.Requests);
        Assert.Empty(shell.NavigationError);
    }

    /// <summary>
    /// 测试相关入口 Create；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Create、Add；具体条件和先后次序见方法体。
    /// </summary>
    private static (Shell Shell, RegionProxy Proxy) Create(IEnumerable<WorkbenchPage>? pages = null, bool regionReady = true)
    {
        var manager = DispatchProxy.Create<IRegionManager, RegionProxy>();
        var proxy = (RegionProxy)(object)manager;
        if (regionReady) proxy.Regions.Add(new Region { Name = "TestRegion" });
        return (new Shell(manager, pages), proxy);
    }

    /// <summary>
    /// Shell 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：WorkbenchNavigationViewModel&lt;WorkbenchPage&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class Shell(IRegionManager manager, IEnumerable<WorkbenchPage>? pages)
        : WorkbenchNavigationViewModel<WorkbenchPage>(manager, new ThemeService(), "TestRegion",
            pages ?? [new("first", "First"), new("second", "Second")],
            [new("first", "First"), new("second", "Second")]);

    /// <summary>
    /// RegionProxy 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：DispatchProxy。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class RegionProxy : DispatchProxy
    {
        public IRegionCollection Regions { get; } = new RegionManager().Regions;
        public int Requests { get; private set; }
        private Action<NavigationResult>? _callback;
        /// <summary>
        /// 测试相关入口 Complete；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void Complete(bool success) => _callback!(new NavigationResult(null!, success));
        /// <summary>
        /// 测试相关入口 Invoke；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Single、OfType；具体条件和先后次序见方法体。
        /// </summary>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == "get_Regions") return Regions;
            if (targetMethod.Name == "RequestNavigate")
            {
                Requests++;
                _callback = args!.OfType<Action<NavigationResult>>().Single();
                return null;
            }
            throw new NotSupportedException(targetMethod.Name);
        }
    }
}
