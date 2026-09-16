using DataManager.SQL;
using DataManager.Workbench.Models;
using DataManager.Controls.Services;
using Extension.Core.ThemeEx;
using DataManager.Controls.ViewModels;
using DataManager.Controls.Views;
using MaterialDesignColors;
using Prism.Ioc;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DataManager.Tests.Workbench
{
    /// <summary>
    /// 回归：深色下输入框文字必须跟随主题变浅，不得回落 WPF 默认黑色。
    /// 根因：MD 的 Outlined 输入样式不把主题前景赋到 Foreground 属性，
    /// Dm* 样式显式钉 MD 原生键（仍是官方体系，无自有画刷）。
    /// 视图必须进真实窗口再断言（游离视图绑定不求值、容器不生成）。
    /// App 由集合 Fixture 统一启动（单例），本测只做断言封送。
    /// </summary>
    [Collection("WorkbenchAppBoot")]
    public class WorkbenchDarkTextTests(WorkbenchAppFixture host)
    {
        private readonly WorkbenchAppFixture _host = host;

        [Fact]
        public void DarkTheme_InputTextFollowsTheme_NotBlack()
        {
            _host.Invoke(() =>
            {
                var theme = ContainerLocator.Container.Resolve<ThemeService>();

                theme.SetTheme(true, PrimaryColor.Indigo, SecondaryColor.Teal);
                var darkFg = ReadFirstInputForeground();
                Assert.True(IsLight(darkFg), $"深色下输入文字应为浅色，实际 {darkFg}");

                theme.SetTheme(false, PrimaryColor.Indigo, SecondaryColor.Teal);
                var lightFg = ReadFirstInputForeground();
                Assert.True(IsDark(lightFg), $"浅色下输入文字应为深色，实际 {lightFg}");
            });
        }

        private static Color ReadFirstInputForeground()
        {
            // 走生产路径：视图进窗口、配真 VM，否则绑定不求值。
            var view = new SqlView
            {
                DataContext = new SqlViewModel<TestEntity>(new SqlWorkspace<TestEntity>(ContainerLocator.Container.Resolve<SQLManager>()))
            };
            var window = new Window
            {
                Content = view,
                Width = 1000,
                Height = 800,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false,
            };
            try
            {
                window.Show();
                window.UpdateLayout();
                // 连接串框收在折叠面板里，须展开才进可视树（生产同理）。
                var expander = CollectAll(view).OfType<Expander>().FirstOrDefault();
                if (expander is not null)
                {
                    expander.IsExpanded = true;
                    // 展开动画/模板应用要过一轮 dispatcher，泵一次再量。
                    Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                    window.UpdateLayout();
                }
                // ⚠️ 必须从**视图自己**的资源链取 DmInput，不能用 Application.Current：
                // 页面样式已归 DataManager.Controls 所有（Themes/PageStyles.xaml），
                // 由各页面在自己的 UserControl.Resources 里合并；宿主那份只是引用。
                // Application.Current 拿到的是宿主链上的另一个实例，ReferenceEquals 必然为 false。
                var dmInput = view.TryFindResource("DmInput") as Style;
                Assert.NotNull(dmInput);
                var boxes = CollectAll(view).OfType<TextBox>().ToList();
                var expanders = CollectAll(view).OfType<Expander>().ToList();
                var box = boxes.FirstOrDefault(b => ReferenceEquals(b.Style, dmInput));
                Assert.True(box is not null,
                    $"expanders={expanders.Count} expanded={string.Join(",", expanders.Select(e => e.IsExpanded))} " +
                    $"textboxes={boxes.Count} styles=[{string.Join("|", boxes.Select(b => (b.Style == dmInput ? "DmInput" : b.Style?.ToString() ?? "null") + ":'" + (b.Text?.Length > 10 ? b.Text[..10] : b.Text) + "'"))}]");
                var brush = box!.Foreground as SolidColorBrush;
                Assert.NotNull(brush);
                return brush!.Color;
            }
            finally
            {
                window.Close();
            }
        }

        private static IEnumerable<DependencyObject> CollectAll(DependencyObject root)
        {
            yield return root;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                foreach (var found in CollectAll(VisualTreeHelper.GetChild(root, i)))
                    yield return found;
            }
        }

        private static TextBox? FindFirstTextBox(DependencyObject root, Style? dmInput)
        {
            if (root is TextBox box && ReferenceEquals(box.Style, dmInput))
                return box;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var found = FindFirstTextBox(VisualTreeHelper.GetChild(root, i), dmInput);
                if (found is not null)
                    return found;
            }
            return null;
        }

        private static bool IsLight(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) > 128;
        private static bool IsDark(Color c) => !IsLight(c);
    }
}
