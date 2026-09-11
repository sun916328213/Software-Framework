using DataManager.Workbench.Infrastructure;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;

namespace DataManager.Workbench.Services
{
    /// <summary>
    /// 黑白（深浅）主题切换服务，MaterialDesign 官方教程做法：
    /// 全应用只有 App.xaml 里那个 <see cref="BundledTheme"/> 实例是主题真相源，
    /// 切主题只改它的 BaseTheme / PrimaryColor / SecondaryColor，
    /// MD 原生画刷（MaterialDesign.Brush.*）随之全局刷新，不维护自有画刷字典。
    /// 当前选择持久化到 workbench-config.json 的 Theme 节。
    /// </summary>
    public class ThemeService
    {
        private static readonly JsonConfigStore.ThemeSection DefaultTheme = new();

        public ThemeService()
        {
            var saved = JsonConfigStore.Load<JsonConfigStore.ThemeSection>("Theme") ?? DefaultTheme;
            IsDark = saved.IsDark;
            Primary = ParsePrimary(saved.Primary, PrimaryColor.Indigo);
            Secondary = ParseSecondary(saved.Secondary, SecondaryColor.Teal);
        }

        /// <summary>当前是否深色主题。</summary>
        public bool IsDark { get; private set; }

        /// <summary>当前主色。</summary>
        public PrimaryColor Primary { get; private set; }

        /// <summary>当前辅色。</summary>
        public SecondaryColor Secondary { get; private set; }

        /// <summary>主题应用完成后触发。</summary>
        public event EventHandler? ThemeChanged;

        /// <summary>启动时调用：把已持久化的主题应用到 BundledTheme 实例。</summary>
        public void Initialize() => SetTheme(IsDark, Primary, Secondary);

        /// <summary>深浅切换（主色/辅色保持不变），等价官方教程里改 BaseTheme 的那一步。</summary>
        public void Toggle() => SetTheme(!IsDark, Primary, Secondary);

        /// <summary>自由切换：深浅 + 主色 + 辅色，一次改完 BundledTheme 三个属性。</summary>
        public void SetTheme(bool dark, PrimaryColor primary, SecondaryColor secondary)
        {
            IsDark = dark;
            Primary = primary;
            Secondary = secondary;

            var bundled = WPFCustomControls.Services.AppThemeService.FindBundledTheme();
            if (bundled is not null)
            {
                bundled.BaseTheme = dark ? BaseTheme.Dark : BaseTheme.Light;
                bundled.PrimaryColor = primary;
                bundled.SecondaryColor = secondary;
            }

            JsonConfigStore.Save("Theme", new JsonConfigStore.ThemeSection(dark, primary.ToString(), secondary.ToString()));
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        private static PrimaryColor ParsePrimary(string? name, PrimaryColor fallback) =>
            Enum.TryParse<PrimaryColor>(name, out var v) ? v : fallback;

        private static SecondaryColor ParseSecondary(string? name, SecondaryColor fallback) =>
            Enum.TryParse<SecondaryColor>(name, out var v) ? v : fallback;
    }
}
