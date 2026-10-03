// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorkbenchThemeSwitchTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Workbench、DataManager.Core、Extension.UI.Theme、MaterialDesignColors、MaterialDesignThemes.Wpf、Prism.Ioc。导入不代表所有类型都会在运行时加载。
using DataManager.Workbench;
using DataManager.Core;
using Extension.UI.Theme;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Prism.Ioc;
using System.Windows;
using System.Windows.Media;

namespace DataManager.Workbench.Tests
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

        /// <summary>
        /// 验证 SetTheme_UpdatesBundledThemeAndNativeBrushes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Invoke、Resolve、FindBundledTheme、NotNull、SetTheme；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 AssertBrush 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 NotNull、Equal、FromRgb；具体条件和先后次序见方法体。
        /// </summary>
        private static void AssertBrush(byte r, byte g, byte b)
        {
            var brush = Application.Current.Resources["MaterialDesign.Brush.Primary"] as SolidColorBrush;
            Assert.NotNull(brush);
            Assert.Equal(Color.FromRgb(r, g, b), brush!.Color);
        }
    }
}
