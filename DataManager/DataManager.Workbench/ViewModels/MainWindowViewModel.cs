using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataManager.Workbench.Infrastructure;
using DataManager.Workbench.Services;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
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
    /// 主窗口视图模型：侧栏导航 + 区域跳转 + 主题自由切换（深浅 + 主色 + 辅色）。
    /// 主题持久化到 workbench-config.json 的 Theme 节。
    /// MVVM 基于 CommunityToolkit（ObservableObject + 源生成器），
    /// 模块化 / IOC / 区域导航仍走 Prism（IRegionManager）。
    /// </summary>
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly IRegionManager _regionManager;
        private readonly ThemeService _theme;

        [ObservableProperty]
        private NavItem? selectedNav;

        [ObservableProperty]
        private PrimaryColor selectedPrimary;

        [ObservableProperty]
        private SecondaryColor selectedSecondary;

        public ObservableCollection<NavItem> NavItems { get; }

        /// <summary>切换按钮图标：亮色显太阳、深色显月亮。</summary>
        public PackIconKind ThemeIconKind => _theme.IsDark ? PackIconKind.WeatherNight : PackIconKind.WeatherSunny;

        /// <summary>切换按钮提示。</summary>
        public string ThemeTip => _theme.IsDark ? "切换到浅色主题" : "切换到深色主题";

        /// <summary>可选主色（MaterialDesign 枚举自由切换）。</summary>
        public IReadOnlyList<PrimaryColor> PrimaryColors { get; } =
            Enum.GetValues<PrimaryColor>();

        /// <summary>可选辅色。</summary>
        public IReadOnlyList<SecondaryColor> SecondaryColors { get; } =
            Enum.GetValues<SecondaryColor>();

        public MainWindowViewModel(IRegionManager regionManager, ThemeService theme)
        {
            _regionManager = regionManager;
            _theme = theme;

            // 构造期直接写字段：避免源生成属性的 OnChanged 回调把初始值又写回一次配置。
            selectedPrimary = theme.Primary;
            selectedSecondary = theme.Secondary;

            NavItems = new ObservableCollection<NavItem>(LoadPages());

            // 配置坏了（空页）就回退默认，保证壳永远有页可显。
            if (NavItems.Count == 0)
            {
                foreach (var p in JsonConfigStore.DefaultPages)
                    NavItems.Add(new NavItem(p.Title, p.Badge, p.ViewName));
            }

            // 主题在别处被改（如测试直调 ThemeService）时刷新图标/提示。
            _theme.ThemeChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(ThemeIconKind));
                OnPropertyChanged(nameof(ThemeTip));
            };
        }

        partial void OnSelectedNavChanged(NavItem? value)
        {
            if (value != null)
                NavigateTo(value.ViewName);
        }

        partial void OnSelectedPrimaryChanged(PrimaryColor value) =>
            _theme.SetTheme(_theme.IsDark, value, SelectedSecondary);

        partial void OnSelectedSecondaryChanged(SecondaryColor value) =>
            _theme.SetTheme(_theme.IsDark, SelectedPrimary, value);

        /// <summary>黑白主题切换（官方做法：只改 BundledTheme 的 BaseTheme，主色/辅色不动）。</summary>
        [RelayCommand]
        private void ToggleTheme() => _theme.Toggle();

        /// <summary>窗口加载后定位到默认页面（配置第一页）。</summary>
        public void SelectFirst()
        {
            if (SelectedNav is not null)
                NavigateTo(SelectedNav.ViewName);
            else if (NavItems.Count > 0)
                NavigateTo(NavItems[0].ViewName);
        }

        private void NavigateTo(string viewName)
        {
            if (_regionManager.Regions.ContainsRegionWithName(RegionNames.MainRegion))
                _regionManager.RequestNavigate(RegionNames.MainRegion, viewName);
        }

        /// <summary>
        /// Prism 导航页来自 workbench-config.json 的 Pages 节（视图名须与
        /// RegisterForNavigation 注册名一致）；缺文件、缺节、坏值回退默认。
        /// 空标题/空视图名的脏项直接丢弃。
        /// </summary>
        private static IEnumerable<NavItem> LoadPages()
        {
            List<JsonConfigStore.PageItem>? saved;
            try
            {
                saved = JsonConfigStore.Load<List<JsonConfigStore.PageItem>>("Pages");
            }
            catch
            {
                saved = null;
            }

            var source = saved is { Count: > 0 } ? saved : JsonConfigStore.DefaultPages;
            foreach (var p in source)
            {
                if (string.IsNullOrWhiteSpace(p.Title) || string.IsNullOrWhiteSpace(p.ViewName))
                    continue;
                yield return new NavItem(p.Title, p.Badge, p.ViewName);
            }
        }
    }
}
