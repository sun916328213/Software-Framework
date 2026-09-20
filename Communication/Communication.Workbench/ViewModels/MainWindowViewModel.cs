using Communication.Workbench.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using Extension.UI.Theme;
using Prism.Regions;
using System.Collections.ObjectModel;

namespace Communication.Workbench.ViewModels;

/// <summary>侧栏导航项。</summary>
public class NavItem
{
    public NavItem(string title, string badge, string viewName)
    {
        Title = title;
        Badge = badge;
        ViewName = viewName;
    }

    public string Title { get; }

    public string Badge { get; }

    public string ViewName { get; }
}

/// <summary>
/// 主窗口视图模型：侧栏导航 + 区域跳转 + 把 DI 里的 ThemeService 单例透给复用的 ThemePickerView。
/// 主题 UI 不在页面里（页面只管通讯测试），与 DataManager.Workbench 同一分工。
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IRegionManager _regionManager;

    [ObservableProperty]
    private NavItem? selectedNav;

    public ObservableCollection<NavItem> NavItems { get; }

    public ThemeService Theme { get; }

    public MainWindowViewModel(IRegionManager regionManager, ThemeService theme)
    {
        _regionManager = regionManager;
        Theme = theme;

        NavItems = new ObservableCollection<NavItem>(LoadPages());

        // 配置坏了（空页）就回退默认，保证壳永远有页可显。
        if (NavItems.Count == 0)
        {
            foreach (var p in NavPageConfig.DefaultPages)
                NavItems.Add(new NavItem(p.Title, p.Badge, p.ViewName));
        }
    }

    partial void OnSelectedNavChanged(NavItem? value)
    {
        if (value != null)
            NavigateTo(value.ViewName);
    }

    /// <summary>
    /// 窗口加载后定位到第一页。必须经 <see cref="SelectedNav"/> 赋值触发导航，
    /// 否则内容区显示了页面而侧栏没有任何高亮项。
    /// </summary>
    public void SelectFirst()
    {
        if (SelectedNav is not null)
            NavigateTo(SelectedNav.ViewName);
        else if (NavItems.Count > 0)
            SelectedNav = NavItems[0];
    }

    private void NavigateTo(string viewName)
    {
        if (_regionManager.Regions.ContainsRegionWithName(RegionNames.MainRegion))
            _regionManager.RequestNavigate(RegionNames.MainRegion, viewName);
    }

    /// <summary>导航页来自 Config/Pages.json，视图名须与注册名一致；缺文件、坏值回退默认。</summary>
    private static IEnumerable<NavItem> LoadPages()
    {
        var saved = NavPageConfig.Read();
        var source = saved is { Count: > 0 } ? saved : NavPageConfig.DefaultPages;
        foreach (var p in source)
        {
            if (string.IsNullOrWhiteSpace(p.Title) || string.IsNullOrWhiteSpace(p.ViewName))
                continue;
            yield return new NavItem(p.Title, p.Badge, p.ViewName);
        }
    }
}
