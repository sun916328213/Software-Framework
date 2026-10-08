// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorkbenchDarkTextTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Workbench、DataManager.Core、DataManager.Core.SQL、DataManager.Runtime.SQL、DataManager.Workbench.Models、DataManager.Controls.Services。导入不代表所有类型都会在运行时加载。
using DataManager.Workbench;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Workbench.Models;
using DataManager.Controls.Services;
using Extension.UI.Theme;
using DataManager.Controls.ViewModels;
using DataManager.Controls.Views;
using MaterialDesignColors;
using Prism.Ioc;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DataManager.Workbench.Tests
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

        /// <summary>
        /// 测试相关入口 DarkTheme_InputTextFollowsTheme_NotBlack；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Invoke、Resolve、SetTheme、ReadFirstInputForeground、True；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 测试相关入口 ReadFirstInputForeground；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Resolve、Show、UpdateLayout、FirstOrDefault、OfType；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 测试相关入口 CollectAll；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 GetChildrenCount、CollectAll、GetChild；具体条件和先后次序见方法体。
        /// </summary>
        private static IEnumerable<DependencyObject> CollectAll(DependencyObject root)
        {
            yield return root;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                foreach (var found in CollectAll(VisualTreeHelper.GetChild(root, i)))
                    yield return found;
            }
        }

        /// <summary>
        /// 测试相关入口 FindFirstTextBox；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ReferenceEquals、GetChildrenCount、FindFirstTextBox、GetChild；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 测试相关入口 IsLight；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        private static bool IsLight(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) > 128;
        /// <summary>
        /// 测试相关入口 IsDark；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 IsLight；具体条件和先后次序见方法体。
        /// </summary>
        private static bool IsDark(Color c) => !IsLight(c);
    }
}
