using Extension.Core.ThemeEx;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Prism.Ioc;
using System.Windows;
using System.Windows.Media;

namespace DataManager.Tests.Workbench
{
    /// <summary>
    /// 主题自由切换验证：官方做法（只改 BundledTheme 一个实例）下，
    /// 深浅 + 主色切换后 MD 原生画刷同步变化，不残留旧颜色。
    /// App 由集合 Fixture 统一启动（单例），本测只做断言封送。
    /// </summary>
    [Collection("WorkbenchAppBoot")]
    public class WorkbenchThemeSwitchTests(WorkbenchAppFixture host)
    {
        private readonly WorkbenchAppFixture _host = host;

        [Fact]
        public void SetTheme_UpdatesBundledThemeAndNativeBrushes()
        {
            _host.Invoke(() =>
            {
                var theme = ContainerLocator.Container.Resolve<ThemeService>();
                var bundled = ThemeService.FindBundledTheme();
                Assert.NotNull(bundled);

                theme.SetTheme(true, PrimaryColor.Red, SecondaryColor.Amber);
                Assert.Equal(BaseTheme.Dark, bundled!.BaseTheme);
                Assert.Equal(PrimaryColor.Red, bundled.PrimaryColor);
                AssertBrush(0xF4, 0x43, 0x36); // Red 500

                theme.SetTheme(false, PrimaryColor.Teal, SecondaryColor.Teal);
                Assert.Equal(BaseTheme.Light, bundled.BaseTheme);
                Assert.Equal(PrimaryColor.Teal, bundled.PrimaryColor);
                AssertBrush(0x00, 0x96, 0x88); // Teal 500
            });
        }

        private static void AssertBrush(byte r, byte g, byte b)
        {
            var brush = Application.Current.Resources["MaterialDesign.Brush.Primary"] as SolidColorBrush;
            Assert.NotNull(brush);
            Assert.Equal(Color.FromRgb(r, g, b), brush!.Color);
        }
    }
}
