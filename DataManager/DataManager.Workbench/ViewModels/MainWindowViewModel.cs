using CommunityToolkit.Mvvm.ComponentModel;
using DataManager.Workbench.Infrastructure;
using Extension.Core.ThemeEx;
using Prism.Regions;
using System.Collections.ObjectModel;

namespace DataManager.Workbench.ViewModels
{
    /// <summary>
    /// 导航项
    /// </summary>
    public class NavItem
    {
        public string Title { get; }
        public string Badge { get; }
        public string ViewName { get; }

        public NavItem(string title, string badge, string viewName)
        {
            Title = title;
            Badge = badge;
            ViewName = viewName;
        }
    }

    /// <summary>
    /// 主窗口视图模型：侧栏导航 + 区域跳转。
    /// 主题切换 UI 已抽成 <c>WPFCustomControls.ThemePickerView</c> 复用控件，
    /// 本视图模型只负责把 DI 里的 <see cref="ThemeService"/> 单例透出去给它绑定。
    /// 主题持久化到 Config/Theme.json（见 <see cref="ThemeService.ConfigPath"/>），
    /// 导航页持久化到 Config/Pages.json（见 <see cref="NavPageConfig"/>）。
    /// MVVM 基于 CommunityToolkit（ObservableObject + 源生成器），
    /// 模块化 / IOC / 区域导航仍走 Prism（IRegionManager）。
    /// </summary>
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly IRegionManager _regionManager;

        [ObservableProperty]
        private NavItem? selectedNav;

        public ObservableCollection<NavItem> NavItems { get; }

        /// <summary>
        /// 主题服务（宿主 DI 单例）。暴露给 XAML 传给复用的 ThemePickerView，
        /// 保证控件改的主题就是全应用在用的那一份（同一个 BundledTheme 实例）。
        /// </summary>
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
        /// 窗口加载后定位到默认页面（配置第一页）。
        /// 必须**通过 SelectedNav 赋值**去导航：那会走 OnSelectedNavChanged 触发跳转，
        /// 同时让侧栏把该项显示成选中态。曾经是直接 NavigateTo，不设 SelectedNav ——
        /// 结果是「内容区显示着某页，侧栏里却没有任何一项高亮」，看着像没选中。
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

        /// <summary>
        /// Prism 导航页来自 Config/Pages.json（视图名须与
        /// RegisterForNavigation 注册名一致）；缺文件、坏值回退默认。
        /// 空标题/空视图名的脏项直接丢弃。
        /// </summary>
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
}
