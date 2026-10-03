// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：IWorkbenchPage、WorkbenchNavigationViewModel、WorkbenchPage。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.ObjectModel、CommunityToolkit.Mvvm.ComponentModel、Extension.UI.Theme、Prism.Regions。导入不代表所有类型都会在运行时加载。
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Extension.UI.Theme;
using Prism.Regions;

namespace WPFCustomControls.Workbench;

/// <summary>A registered page in a workbench navigation catalog.</summary>
/// <remarks>
/// IWorkbenchPage 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
/// </remarks>
public interface IWorkbenchPage
{
    string Title { get; }
    string ViewName { get; }
}

/// <summary>Coordinates shell selection and navigation without owning page resources.</summary>
/// <remarks>
/// WorkbenchNavigationViewModel 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
/// 继承或实现的契约：ObservableObject、IDisposable。具体约束按这些契约及各成员实现解释。
/// </remarks>
public abstract class WorkbenchNavigationViewModel<TPage> : ObservableObject, IDisposable
    where TPage : class, IWorkbenchPage
{
    private readonly IRegionManager _regions;
    private readonly string _regionName;
    private TPage? _selectedNav;
    private TPage? _confirmedNav;
    private bool _restoring;
    private bool _closed;
    private bool _isNavigating;
    private string _navigationError = string.Empty;

    /// <summary>
    /// 初始化 WorkbenchNavigationViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 ToList、ToHashSet、Select、DistinctBy、Where；具体条件和先后次序见方法体。
    /// </summary>
    protected WorkbenchNavigationViewModel(IRegionManager regions, ThemeService theme, string regionName,
        IEnumerable<TPage> pages, IEnumerable<TPage> defaults)
    {
        _regions = regions;
        Theme = theme;
        _regionName = regionName;
        var catalog = defaults.ToList();
        var allowed = catalog.Select(page => page.ViewName).ToHashSet(StringComparer.Ordinal);
        var configured = pages.ToList();
        var valid = configured.Where(page => page is not null && !string.IsNullOrWhiteSpace(page.Title)
            && allowed.Contains(page.ViewName)).DistinctBy(page => page.ViewName).ToList();
        if (valid.Count != configured.Count)
            System.Diagnostics.Trace.TraceWarning("Ignored invalid or duplicate workbench pages: {0}", configured.Count - valid.Count);
        NavItems = new(valid.Count > 0 ? valid : catalog);
    }

    public ThemeService Theme { get; }
    public ObservableCollection<TPage> NavItems { get; }
    public bool IsNavigating { get => _isNavigating; private set { if (SetProperty(ref _isNavigating, value)) OnPropertyChanged(nameof(CanSelectNavigation)); } }
    public bool CanSelectNavigation => !_closed && !IsNavigating;
    public string NavigationError { get => _navigationError; private set => SetProperty(ref _navigationError, value); }

    public TPage? SelectedNav
    {
        get => _selectedNav;
        set
        {
            if (_closed) return;
            if (IsNavigating && !_restoring) { OnPropertyChanged(); return; }
            if (SetProperty(ref _selectedNav, value) && !_restoring && value is not null)
                Navigate(value);
        }
    }

    /// <summary>Called after modules and shell regions have initialized. Repeated calls are harmless.</summary>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 SelectFirst。
    /// 正文中的关键协作调用包括 FirstOrDefault、ReferenceEquals、SetSelection、Navigate；具体条件和先后次序见方法体。
    /// </remarks>
    public void SelectFirst()
    {
        if (_closed || IsNavigating) return;
        var page = SelectedNav ?? NavItems.FirstOrDefault();
        if (page is null) return;
        if (ReferenceEquals(page, _confirmedNav)) return;
        SetSelection(page);
        Navigate(page);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Navigate。
    /// 正文中的关键协作调用包括 Contains、Fail、ContainsRegionWithName、RequestNavigate、SetSelection；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private void Navigate(TPage page)
    {
        if (!NavItems.Contains(page)) { Fail("该页面不在导航目录中。"); return; }
        if (!_regions.Regions.ContainsRegionWithName(_regionName)) { Fail("页面区域尚未就绪，请重试。"); return; }
        NavigationError = string.Empty;
        IsNavigating = true;
        try
        {
            _regions.RequestNavigate(_regionName, page.ViewName, result =>
            {
                if (_closed) return;
                IsNavigating = false;
                if (result.Result != true) { Fail($"无法打开{page.Title}：{result.Error?.Message ?? "导航被取消"}"); return; }
                _confirmedNav = page;
                SetSelection(page);
                OnNavigationSucceeded(page);
            });
        }
        catch (Exception error) { IsNavigating = false; Fail($"无法打开{page.Title}：{error.Message}"); }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Fail。
    /// 正文中的关键协作调用包括 SetSelection；具体条件和先后次序见方法体。
    /// </summary>
    private void Fail(string message)
    {
        NavigationError = message;
        SetSelection(_confirmedNav);
    }

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetSelection。
    /// </summary>
    private void SetSelection(TPage? page)
    {
        _restoring = true;
        try { SelectedNav = page; }
        finally { _restoring = false; }
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnNavigationSucceeded。
    /// </summary>
    protected virtual void OnNavigationSucceeded(TPage page) { }
    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 OnPropertyChanged、nameof；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose() { _closed = true; IsNavigating = false; OnPropertyChanged(nameof(CanSelectNavigation)); }
}

/// <summary>A shell page for workbenches with a fixed catalog.</summary>
/// <remarks>
/// WorkbenchPage 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
/// 继承或实现的契约：IWorkbenchPage。具体约束按这些契约及各成员实现解释。
/// </remarks>
public sealed record WorkbenchPage(string Title, string ViewName) : IWorkbenchPage;
