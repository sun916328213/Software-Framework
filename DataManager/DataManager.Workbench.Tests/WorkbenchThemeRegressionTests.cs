using DataManager.Controls.Services;
using Extension.UI.Theme;
using MaterialDesignColors;
using Prism.Ioc;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WPFCustomControls.Controls;

namespace DataManager.Tests.Workbench
{
    /// <summary>
    /// 主题回归：
    /// 1) 无持久化配置时，默认值必须来自 App.xaml 的 BundledTheme（Light + Teal + Amber），
    ///    而不是代码里写死的 Indigo/Teal，也不是构造参数 defaultDark；
    /// 2) ConfigManagerView 的卡片底/描边必须跟随黑白切换（原先钉死白色 Color_Main）；
    /// 3) 控件用到的 MD 原生画刷键必须真实存在（防键名写错静默回落）。
    /// App 由集合 Fixture 统一启动（单例），本测只做断言封送。
    /// </summary>
    [Collection("WorkbenchAppBoot")]
    public class WorkbenchThemeRegressionTests(WorkbenchAppFixture host)
    {
        private readonly WorkbenchAppFixture _host = host;

        [Fact]
        public void NoConfig_DefaultsComeFromAppXamlBundledTheme()
        {
            _host.Invoke(() =>
            {
                var bundled = ThemeService.FindBundledTheme();
                Assert.NotNull(bundled);

                var theme = ContainerLocator.Container.Resolve<ThemeService>();
                var originalPath = ThemeService.ConfigPath;
                var tempPath = Path.Combine(Path.GetTempPath(), $"theme-regression-{Guid.NewGuid():N}.json");
                try
                {
                    // 隔离持久化文件，避免读到别的用例写下的选择。
                    ThemeService.ConfigPath = tempPath;
                    Assert.False(File.Exists(tempPath));

                    // 模拟 App.xaml 的设定：Light + Teal + Amber。
                    theme.SetTheme(false, PrimaryColor.Teal, SecondaryColor.Amber);
                    Assert.True(File.Exists(tempPath), "显式切换应落盘");
                    File.Delete(tempPath);

                    // 故意传 defaultDark: true —— App.xaml 的 Light 必须赢。
                    var fresh = new ThemeService(defaultDark: true);
                    Assert.False(fresh.IsDark);
                    Assert.Equal(PrimaryColor.Teal, fresh.Primary);
                    Assert.Equal(SecondaryColor.Amber, fresh.Secondary);
                }
                finally
                {
                    ThemeService.ConfigPath = originalPath;
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
            });
        }

        [Fact]
        public void Initialize_DoesNotBakeDefaultsIntoConfig()
        {
            _host.Invoke(() =>
            {
                var originalPath = ThemeService.ConfigPath;
                var tempPath = Path.Combine(Path.GetTempPath(), $"theme-init-{Guid.NewGuid():N}.json");
                try
                {
                    ThemeService.ConfigPath = tempPath;

                    new ThemeService(defaultDark: false).Initialize();

                    // 初始化只应用不落盘：否则 App.xaml 的默认值会被固化，之后改 App.xaml 不再生效。
                    Assert.False(File.Exists(tempPath), "Initialize 不应写配置文件");
                }
                finally
                {
                    ThemeService.ConfigPath = originalPath;
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
            });
        }

        [Fact]
        public void ConfigManagerPanel_BackgroundFollowsTheme()
        {
            _host.Invoke(() =>
            {
                var theme = ContainerLocator.Container.Resolve<ThemeService>();
                var view = new ConfigManagerView
                {
                    ItemsSource = new ObservableCollection<SimpleRow> { new() { Name = "行1" } },
                };
                var window = new Window
                {
                    Content = view,
                    Width = 900,
                    Height = 600,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                };
                try
                {
                    window.Show();
                    window.UpdateLayout();

                    theme.SetTheme(true, PrimaryColor.Indigo, SecondaryColor.Teal);
                    var darkBg = ReadPanelBackground(view, window);
                    Assert.True(IsDark(darkBg), $"深色下卡片底应为深色，实际 {darkBg}");

                    theme.SetTheme(false, PrimaryColor.Indigo, SecondaryColor.Teal);
                    var lightBg = ReadPanelBackground(view, window);
                    Assert.True(IsLight(lightBg), $"浅色下卡片底应为浅色，实际 {lightBg}");
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void ThemeBrushKeys_UsedByControls_Resolve()
        {
            _host.Invoke(() =>
            {
                // 键名写错时 DynamicResource 会静默回落到无画刷（黑），必须挡住。
                foreach (var key in new[]
                         {
                             "MaterialDesign.Brush.Card.Background",
                             "MaterialDesign.Brush.Card.Border",
                             "MaterialDesign.Brush.DataGrid.ColumnHeaderForeground",
                         })
                {
                    var brush = Application.Current.TryFindResource(key);
                    Assert.True(brush is SolidColorBrush, $"缺少主题画刷键 {key}");
                }
            });
        }

        private static Color ReadPanelBackground(ConfigManagerView view, Window window)
        {
            // 换肤后要过一轮 dispatcher，DynamicResource 才重新求值。
            Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            window.UpdateLayout();

            var border = (view.Content as Border)
                ?? throw new InvalidOperationException("ConfigManagerView 的根内容不是 Border");
            var brush = border.Background as SolidColorBrush;
            Assert.True(brush is not null, $"卡片底画刷不是纯色：{border.Background}");
            return brush!.Color;
        }

        private static bool IsLight(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) > 128;
        private static bool IsDark(Color c) => !IsLight(c);

        /// <summary>给 DataGrid 一个最小行模型（本测不关心列内容）。</summary>
        public sealed class SimpleRow
        {
            public string Name { get; set; } = "";
        }
    }
}
