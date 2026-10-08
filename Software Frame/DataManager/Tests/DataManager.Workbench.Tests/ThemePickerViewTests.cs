// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ThemePickerViewTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Workbench、DataManager.Core、Extension.UI.Theme、MaterialDesignColors、MaterialDesignThemes.Wpf、Prism.Ioc。导入不代表所有类型都会在运行时加载。
using DataManager.Workbench;
using DataManager.Core;
using Extension.UI.Theme;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Prism.Ioc;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WPFCustomControls.Controls;
using WPFCustomControls.ValueConvert;

namespace DataManager.Workbench.Tests
{
    /// <summary>
    /// ThemePickerView 复用控件回归：
    /// 1) 不传 Theme 时自建兜底 ThemeService，控件单独放进任意窗口也能工作；
    /// 2) 传了宿主单例时，控件与宿主共用同一真相源（选中项即服务的当前值）；
    /// 3) 浅色 / 深色两个分段按钮各自把主题「设成」目标态，而不是翻转 —— 连点两次不来回跳；
    /// 4) 两个分段按钮各自绑自己的命令，不会互相串；
    /// 5) 别处改了主题，控件自动跟上（选中项 + 高亮态）；
    /// 6) ShowColorPickers=false 时配色整块收起，但浅深分段按钮必须留着 —— 否则控件就废了；
    /// 7) 配色列表不含 Inherit 这类没有颜色的占位值，且每个选项都能取到预览色；
    /// 8) 预览色块的颜色 == BundledTheme 实际应用的主色（预览不能骗人）。
    /// 9) 侧栏与分段控件外皮都**跟随主题** —— 浅色主题下侧栏必须是浅底（原先写死 #0F172A，浅色下仍是深色）。
    /// 10) 侧栏徽标 chip 的文字在自己底色上的对比度 ≥ 4.5:1（四种组合全过）。
    /// 11) 药丸圆角与凹槽同心（r=4），且**不画描边** —— 层级全靠填充分层
    ///     （用户 2026-09-15 实测后拍板「把边框去除效果更好」，连下拉框的 Outlined 边也去掉）。
    /// 12) 分段标签未选中态、侧栏次要文字的对比度都 ≥ 4.5:1（各有一条用例 + 变异验证）。
    /// App 由集合 Fixture 统一启动（单例），本测只做断言封送。
    /// </summary>
    [Collection("WorkbenchAppBoot")]
    public class ThemePickerViewTests(WorkbenchAppFixture host)
    {
        private readonly WorkbenchAppFixture _host = host;

        /// <summary>
        /// 测试相关入口 NoThemeInjected_FallsBackToSelfOwnedService；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Invoke、NotNull、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void NoThemeInjected_FallsBackToSelfOwnedService()
        {
            _host.Invoke(() =>
            {
                var view = new ThemePickerView();

                Assert.NotNull(view.ViewModel.Theme);
                Assert.Equal(view.ViewModel.Theme!.Primary, view.ViewModel.SelectedPrimary);
                Assert.Equal(view.ViewModel.Theme.Secondary, view.ViewModel.SelectedSecondary);
            });
        }

        /// <summary>
        /// 测试相关入口 InjectedTheme_IsTheSharedSourceOfTruth；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Invoke、Resolve、Same、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void InjectedTheme_IsTheSharedSourceOfTruth()
        {
            _host.Invoke(() =>
            {
                var theme = ContainerLocator.Container.Resolve<ThemeService>();
                var view = new ThemePickerView { Theme = theme };

                // 同一个实例 —— 控件改的就是全应用在用的那一份。
                Assert.Same(theme, view.ViewModel.Theme);
                Assert.Equal(theme.Primary, view.ViewModel.SelectedPrimary);
                Assert.Equal(theme.Secondary, view.ViewModel.SelectedSecondary);
                Assert.Equal(theme.IsDark, view.ViewModel.IsDark);
            });
        }

        /// <summary>
        /// 测试相关入口 SegmentButtons_SetExplicitState；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Invoke、Resolve、RedirectThemeConfig、ShowInWindow、SetTheme；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SegmentButtons_SetExplicitState()
        {
            _host.Invoke(() =>
            {
                var theme = ContainerLocator.Container.Resolve<ThemeService>();
                var restore = RedirectThemeConfig(theme);
                var view = new ThemePickerView { Theme = theme };
                // 命令是绑定来的，必须在真实窗口里求值（游离视图上 Command 为 null）。
                var window = ShowInWindow(view);
                try
                {
                    theme.SetTheme(true, theme.Primary, theme.Secondary);
                    Pump(window);

                    FindLightButton(view).Command.Execute(null);
                    Assert.False(theme.IsDark);
                    Assert.False(view.ViewModel.IsDark);

                    FindDarkButton(view).Command.Execute(null);
                    Assert.True(theme.IsDark);
                    Assert.True(view.ViewModel.IsDark);
                }
                finally
                {
                    window.Close();
                    restore();
                }
            });
        }

        /// <summary>
        /// 测试相关入口 SegmentButtons_AreNotToggles；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Invoke、Resolve、RedirectThemeConfig、ShowInWindow、Execute；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SegmentButtons_AreNotToggles()
        {
            _host.Invoke(() =>
            {
                var theme = ContainerLocator.Container.Resolve<ThemeService>();
                var restore = RedirectThemeConfig(theme);
                var view = new ThemePickerView { Theme = theme };
                var window = ShowInWindow(view);
                try
                {
                    // 「浅色」是设为浅色，不是翻转：连点两次仍应是浅色。
                    // 若哪天有人把它接回 ToggleThemeCommand，这条会立刻变红。
                    FindLightButton(view).Command.Execute(null);
                    FindLightButton(view).Command.Execute(null);
                    Assert.False(theme.IsDark);

                    FindDarkButton(view).Command.Execute(null);
                    FindDarkButton(view).Command.Execute(null);
                    Assert.True(theme.IsDark);
                }
                finally
                {
                    window.Close();
                    restore();
                }
            });
        }

        /// <summary>
        /// 测试相关入口 SegmentButtons_EachBoundToItsOwnCommand；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Invoke、ShowInWindow、Same、FindLightButton、FindDarkButton；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SegmentButtons_EachBoundToItsOwnCommand()
        {
            _host.Invoke(() =>
            {
                var view = new ThemePickerView();
                var window = ShowInWindow(view);
                try
                {
                    Assert.Same(view.ViewModel.SetLightThemeCommand, FindLightButton(view).Command);
                    Assert.Same(view.ViewModel.SetDarkThemeCommand, FindDarkButton(view).Command);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        /// <summary>
        /// 测试相关入口 ExternalThemeChange_PullsSelectionIntoControl；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Invoke、Resolve、RedirectThemeConfig、ShowInWindow、SetTheme；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ExternalThemeChange_PullsSelectionIntoControl()
        {
            _host.Invoke(() =>
            {
                var theme = ContainerLocator.Container.Resolve<ThemeService>();
                var restore = RedirectThemeConfig(theme);
                var view = new ThemePickerView { Theme = theme };
                // 必须塞进真实 Window：两个下拉是 TwoWay 绑定，控件没被实例化时
                // 下拉根本不参与绑定，「控件自动跟上」就验不到（游离视图上断言会假通过）。
                var window = ShowInWindow(view);
                try
                {
                    // 别处改了主题（宿主代码 / 另一个窗口），控件应自动跟上而不是各说各话。
                    theme.SetTheme(!theme.IsDark, PrimaryColor.Purple, SecondaryColor.Pink);
                    Pump(window);

                    Assert.Equal(PrimaryColor.Purple, view.ViewModel.SelectedPrimary);
                    Assert.Equal(SecondaryColor.Pink, view.ViewModel.SelectedSecondary);
                    Assert.Equal(theme.IsDark, view.ViewModel.IsDark);
                }
                finally
                {
                    window.Close();
                    restore();
                }
            });
        }

        /// <summary>
        /// 测试相关入口 SegmentHighlight_FollowsThemeState；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Invoke、Resolve、RedirectThemeConfig、ShowInWindow、SetTheme；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SegmentHighlight_FollowsThemeState()
        {
            _host.Invoke(() =>
            {
                var theme = ContainerLocator.Container.Resolve<ThemeService>();
                var restore = RedirectThemeConfig(theme);
                var view = new ThemePickerView { Theme = theme };
                var window = ShowInWindow(view);
                try
                {
                    theme.SetTheme(false, theme.Primary, theme.Secondary);
                    Pump(window);

                    // 当前态抬到不透明，另一侧压到 0.65 —— 用户一眼能看出现在是浅色还是深色。
                    // 0.65 而不是 0.6：0.6 时浅色下未选中标签只有 4.38:1，低于 AA 正文下限 4.5:1
                    // （见 SegmentLabels_MeetContrastInBothThemes）。
                    Assert.Equal(1d, FindLightButton(view).Opacity);
                    Assert.Equal(0.65d, FindDarkButton(view).Opacity);

                    theme.SetTheme(true, theme.Primary, theme.Secondary);
                    Pump(window);

                    Assert.Equal(0.65d, FindLightButton(view).Opacity);
                    Assert.Equal(1d, FindDarkButton(view).Opacity);
                }
                finally
                {
                    window.Close();
                    restore();
                }
            });
        }

        /// <summary>
        /// 测试相关入口 SegmentChrome_FollowsThemeAndStaysDistinctFromItsContainer；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Invoke、Resolve、RedirectThemeConfig、ShowInWindow、SetTheme；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SegmentChrome_FollowsThemeAndStaysDistinctFromItsContainer()
        {
            _host.Invoke(() =>
            {
                var theme = ContainerLocator.Container.Resolve<ThemeService>();
                var restore = RedirectThemeConfig(theme);
                // 侧栏已从「固定深色品牌区」改成跟随主题，分段控件的外皮也跟着走 MD 原生键
                //（原先是写死的白色半透明 —— 压到浅底上等于看不见）。
                // 这里守两件事：① 选中态底色 == 主题的 Card.Background；
                // ② 它与凹槽底（Background）必须不同，否则「选中态看不见」，正是白半透明搬到浅底会踩的坑。
                var view = new ThemePickerView
                {
                    Theme = theme,
                    FieldForeground = new SolidColorBrush(Colors.Lime),
                };
                var window = ShowInWindow(view);
                try
                {
                    foreach (var dark in new[] { false, true })
                    {
                        theme.SetTheme(dark, theme.Primary, theme.Secondary);
                        Pump(window);

                        // 注意：这两个键的**值**本身随主题变，必须每轮重新取，不能循环外取一次。
                        var cardBg = ((SolidColorBrush)Application.Current.FindResource("MaterialDesign.Brush.Card.Background")).Color;
                        var troughBg = ((SolidColorBrush)Application.Current.FindResource("MaterialDesign.Brush.Background")).Color;
                        Assert.NotEqual(troughBg, cardBg);

                        var selected = dark ? FindDarkButton(view) : FindLightButton(view);
                        var unselected = dark ? FindLightButton(view) : FindDarkButton(view);

                        // 选中侧铺主题亮底；未选中侧保持透明底，只靠 Opacity 0.65 表示「没选我」。
                        Assert.Equal(cardBg, ((SolidColorBrush)selected.Background).Color);
                        Assert.Equal(Colors.Transparent, ((SolidColorBrush)unselected.Background).Color);

                        // 前景两侧统一跟随字段前景（宿主传什么就是什么）
                        Assert.Equal(Colors.Lime, ((SolidColorBrush)selected.Foreground).Color);
                        Assert.Equal(Colors.Lime, ((SolidColorBrush)unselected.Foreground).Color);
                    }
                }
                finally
                {
                    window.Close();
                    restore();
                }
            });
        }

        /// <summary>
        /// 药丸的两条不变式。**口径 2026-09-15 被用户改过一次，别照抄旧结论。**
        /// ① 药丸圆角必须与凹槽**同心**（凹槽 r=8 + Padding=4 → 内弧 4），且必须**真的来自
        ///    `ButtonAssist.CornerRadius`**：模板原先硬编码 `CornerRadius="5"`，样式里那条 assist
        ///    从来没生效过。改成绑**附着属性**后，绑定失败是**静默**的（会掉成默认 0，药丸变方），
        ///    所以断言必须建立在「模板实际用的值 == 按钮上的 assist 值」上，而不是断言某个常量 ——
        ///    常量断言在「两处一起改错」时照样绿。
        /// ② **药丸不画描边**（两态都 `BorderThickness == 0`）。曾给选中态药丸加过 1px 边，
        ///    理由是「白药丸压 `#F1F1F1` 只差 14 级灰，不给边圆角看不见」—— 用户实测后判定
        ///    **去掉更好**：整块改成「填充分层」表达层级（药丸走 `Card.Background`、
        ///    凹槽走 `Background`，靠明度差抬起来）。所以这里钉 0 是**有意的**：
        ///    别再把「少了的边」当漏项加回来。
        /// </summary>
        [Fact]
        public void SegmentPill_CornerRadiusIsConcentricAndNoPillIsOutlined()
        {
            _host.Invoke(() =>
            {
                var theme = ContainerLocator.Container.Resolve<ThemeService>();
                var restore = RedirectThemeConfig(theme);
                var view = new ThemePickerView { Theme = theme };
                var window = ShowInWindow(view);
                try
                {
                    foreach (var dark in new[] { false, true })
                    {
                        theme.SetTheme(dark, theme.Primary, theme.Secondary);
                        Pump(window);

                        var selected = dark ? FindDarkButton(view) : FindLightButton(view);
                        var unselected = dark ? FindLightButton(view) : FindDarkButton(view);

                        var selectedPill = FindPill(selected);
                        var unselectedPill = FindPill(unselected);

                        // ① 同心圆角：模板实际生效的圆角 == 按钮上的 assist 值
                        var assist = (CornerRadius)selected.GetValue(ButtonAssist.CornerRadiusProperty);
                        Assert.Equal(assist, selectedPill.CornerRadius);

                        // 凹槽 r=8、Padding=4 → 同心内弧必须是 4（写死这个数，防「两处一起改错」）
                        Assert.Equal(new CornerRadius(4), assist);

                        // ② 两态药丸都不画描边（层级改由填充表达）
                        Assert.Equal(new Thickness(0), selectedPill.BorderThickness);
                        Assert.Equal(new Thickness(0), unselectedPill.BorderThickness);
                    }
                }
                finally
                {
                    window.Close();
                    restore();
                }
            });
        }

        /// <summary>
        /// 容器**不画描边**，层级全靠填充 —— 2026-09-15 用户实测后拍板（原话「把边框去除效果更好」）。
        /// <para>
        /// 演进史（**别照抄中间某一版**）：描边走 `Card.Border`（软档 1.96 / 2.63:1）→ 一度调到
        /// `ForegroundLight`（3.8:1，被否「线框感」）→ 回退 `Card.Border` → **最终整条去掉**：
        /// 面板框、凹槽框、下拉框的 Outlined 边、药丸的 1px 边全去，整块改用「填充分层」。
        /// </para>
        /// <para>
        /// 本用例钉三件事：
        /// ① 面板 / 凹槽 `BorderThickness == 0` —— 钉 0 是**有意的**，别把「少了的框」当漏项加回来；
        /// ② 面板填充 ≠ 侧栏填充（否则面板整块消失）；
        /// ③ 选中药丸填充 ≠ 凹槽填充（否则选中态消失）。
        /// ②③ 必须**每轮按当前主题重新取色**，循环外取一次会拿到上一态的值。
        /// </para>
        /// <para>
        /// ⚠️ 已知且**刻意保留**的一处：凹槽填充与面板填充是同一个画刷
        /// （`MaterialDesign.Brush.Background`），所以凹槽本身 1.00:1、完全看不见。
        /// 渲染实测观感没问题（分段控件读起来就是「两个标签 + 选中那个有药丸」），
        /// 选中态由药丸独立承载，故不额外补填充。有框/无框对照图见
        /// `.workbuddy-ai/preview/bd-grid.png`。
        /// </para>
        /// </summary>
        [Fact]
        public void ContainerLayering_IsCarriedByFillNotByOutline()
        {
            _host.Invoke(() =>
            {
                var theme = ContainerLocator.Container.Resolve<ThemeService>();
                var restore = RedirectThemeConfig(theme);
                var view = new ThemePickerView { Theme = theme };
                var window = ShowInWindow(view);
                try
                {
                    foreach (var dark in new[] { false, true })
                    {
                        theme.SetTheme(dark, theme.Primary, theme.Secondary);
                        Pump(window);

                        var panel = (Border)view.FindName("ColorPickerPanel")!;
                        var trough = FindSegmentContainer(view);

                        // 宿主侧栏底色（MainWindow 用的就是这个键），每轮重新取。
                        var sidebar = ((SolidColorBrush)Application.Current.FindResource(
                            "MaterialDesign.Brush.Card.Background")).Color;

                        // ① 框已按用户决定去掉
                        Assert.Equal(new Thickness(0), panel.BorderThickness);
                        Assert.Equal(new Thickness(0), trough.BorderThickness);

                        // ② 面板靠填充与侧栏分层
                        var panelFill = ((SolidColorBrush)panel.Background).Color;
                        Assert.NotEqual(sidebar, panelFill);

                        // ③ 选中药丸靠填充与凹槽分层
                        var selected = dark ? FindDarkButton(view) : FindLightButton(view);
                        var troughFill = ((SolidColorBrush)trough.Background).Color;
                        var pillFill = ((SolidColorBrush)selected.Background).Color;
                        Assert.NotEqual(troughFill, pillFill);
                    }
                }
                finally
                {
                    window.Close();
                    restore();
                }
            });
        }

        /// <summary>
        /// 分段标签在**未选中**态会被 Opacity 压暗，这一步把文字推到底色附近，必须守住 4.5:1。
        /// 背景：Opacity=0.6 时浅色下实测只有 **4.38:1**（字形实心像素 `#707070` 压容器底 `#F1F1F1`），
        /// 深色下 4.90:1 本来就过线 —— **只有浅色这一侧不达标**，肉眼几乎看不出来，只能靠算。
        /// 抬到 0.65 后（本用例实测）：浅色 **5.24:1** / 深色 **5.52:1**，两侧都过线。
        /// 口径与 MainWindow 一致（显式传 MD 原生键）：不传的话会用到「固定深色宿主」的兜底默认值
        /// （`FieldForeground` 默认 `#E2E8F0`），那个组合下的数字没有参考意义。
        /// </summary>
        [Fact]
        public void SegmentLabels_MeetContrastInBothThemes()
        {
            _host.Invoke(() =>
            {
                var theme = ContainerLocator.Container.Resolve<ThemeService>();
                var restore = RedirectThemeConfig(theme);

                var view = new ThemePickerView
                {
                    Theme = theme,
                    FieldBackground = (Brush)Application.Current.FindResource("MaterialDesign.Brush.Card.Background"),
                    FieldForeground = (Brush)Application.Current.FindResource("MaterialDesign.Brush.Foreground"),
                };

                var window = ShowInWindow(view);
                try
                {
                    foreach (var dark in new[] { false, true })
                    {
                        theme.SetTheme(dark, theme.Primary, theme.Secondary);

                        // ⚠️ 两个字段画刷必须**每轮按当前主题重新解析**：真实宿主（MainWindow.xaml:59-60）
                        // 传的是 {DynamicResource}，换肤时字段拿到的是**新实例**；而这两个 DP 本身只是普通
                        // Brush，控件不会替调用方重新解析。测试若只在循环外解析一次，深色那轮手里还是
                        // 浅色主题的深色文字（#E5000000）→ 合成出 #151515 压 #323232 = 1.42:1 的**假失败**
                        // （真实渲染是 #A0A0A0 / 4.90:1）。这里显式模拟宿主的 DynamicResource 行为。
                        view.FieldBackground = (Brush)Application.Current.FindResource("MaterialDesign.Brush.Card.Background");
                        view.FieldForeground = (Brush)Application.Current.FindResource("MaterialDesign.Brush.Foreground");
                        Pump(window);

                        var unselected = dark ? FindLightButton(view) : FindDarkButton(view);

                        // 未选中按钮是透明底 → 实际压在祖先链合成出来的容器底色上。
                        var backdrop = CompositeBackdrop(unselected);

                        // 半透明前景先合成一次，再整体乘上元素 Opacity —— 两步都要算。
                        var fg = ((SolidColorBrush)unselected.Foreground).Color;
                        var alpha = fg.A / 255.0 * unselected.Opacity;
                        var effective = Color.FromRgb(
                            (byte)Math.Round(fg.R * alpha + backdrop.R * (1 - alpha)),
                            (byte)Math.Round(fg.G * alpha + backdrop.G * (1 - alpha)),
                            (byte)Math.Round(fg.B * alpha + backdrop.B * (1 - alpha)));

                        var ratio = ContrastRatio(effective, backdrop);
                        Assert.True(ratio >= 4.5,
                            $"未选中分段标签对比度 {ratio:0.00}:1 < 4.5:1（{(dark ? "深色" : "浅色")}，"
                            + $"标签 {effective}，底 {backdrop}，Opacity {unselected.Opacity}）");
                    }
                }
                finally
                {
                    window.Close();
                    restore();
                }
            });
        }

        /// <summary>
        /// 侧栏**次要文字**（副标题「测试工作台 · Material」+ 页脚版本号）必须过 WCAG AA 4.5:1。
        /// <para>
        /// 曾经的 bug：两处都用 `MaterialDesign.Brush.ForegroundLight`（= `#000000/7F`，
        /// 深色 `#FFFFFF/7F`）。压在侧栏（`Card.Background`，浅 `#FFFFFF` / 深 `#454545`）上实测
        /// **浅色 3.95:1 / 深色 3.76:1** —— 低于正文下限 4.5:1，而且字号只有 11px。
        /// 根因是**档位用错**：MD 那个 50% alpha 本来是「禁用」档（spec 的 38~50% 一档），
        /// 不是「次要文字」档（spec 的 60% 一档）。
        /// </para>
        /// <para>
        /// 修法：换成「主前景 + `Opacity=0.7`」，与 ThemePickerView 的 Caption 同一配方 ——
        /// 全仓的「次要文字」就这一种写法。本用例实测 **浅色 6.3855:1 / 深色 4.9403:1**。
        /// ⚠️ 深色那侧只有 0.44 的余量，且这个数是 `#FFFFFF/E5 × 0.7` 压在 `Card.Background`
        /// （深 `#454545`）上的确定值 —— 侧栏底色若哪天变浅，必须重新量。
        /// 这里同时钉两件事：① 对比度 ≥ 4.5:1；② **机制** —— 前景必须是 `MaterialDesign.Brush.Foreground`，
        /// 不许退回 `ForegroundLight`（否则哪天有人「顺手简化」又把档位调回去）。
        /// </para>
        /// </summary>
        [Fact]
        public void SidebarSecondaryText_MeetsContrastInBothThemes()
        {
            _host.Invoke(() =>
            {
                var theme = ContainerLocator.Container.Resolve<ThemeService>();
                var restore = RedirectThemeConfig(theme);
                try
                {
                    var window = Application.Current.MainWindow;
                    Assert.NotNull(window);
                    var sidebar = FindSidebar(window!);

                    // 按**内容**定位这两个元素：它们的 Foreground/Opacity 正是本用例要验的东西，
                    // 拿它们当筛选条件会变成同义反复。
                    var wanted = new[] { "测试工作台 · Material", "Material · Toolkit · Prism · WPF · .NET 9" };
                    var failures = new List<string>();

                    foreach (var dark in new[] { false, true })
                    {
                        theme.SetTheme(dark, theme.Primary, theme.Secondary);
                        Pump(window!);

                        var expected = ((SolidColorBrush)Application.Current.FindResource(
                            "MaterialDesign.Brush.Foreground")).Color;

                        var targets = FindDescendants<TextBlock>(sidebar)
                            .Where(t => wanted.Contains(t.Text))
                            .ToList();
                        Assert.True(targets.Count == wanted.Length,
                            $"侧栏里只找到 {targets.Count}/{wanted.Length} 个次要文字元素（可视树没建起来？）");

                        foreach (var text in targets)
                        {
                            var backdrop = CompositeBackdrop(text);
                            var fg = ((SolidColorBrush)text.Foreground).Color;

                            // 半透明前景先合成一次，再整体乘上元素 Opacity —— 两步都要算。
                            var alpha = fg.A / 255.0 * text.Opacity;
                            var effective = Color.FromRgb(
                                (byte)Math.Round(fg.R * alpha + backdrop.R * (1 - alpha)),
                                (byte)Math.Round(fg.G * alpha + backdrop.G * (1 - alpha)),
                                (byte)Math.Round(fg.B * alpha + backdrop.B * (1 - alpha)));

                            var ratio = ContrastRatio(effective, backdrop);
                            if (ratio < 4.5)
                                failures.Add($"{(dark ? "深色" : "浅色")}「{text.Text}」{ratio:F2}:1"
                                    + $"（{effective} 压 {backdrop}，Opacity {text.Opacity}）");

                            if (fg != expected)
                                failures.Add($"{(dark ? "深色" : "浅色")}「{text.Text}」前景不是主前景："
                                    + $"{fg}（期望 {expected}）—— 是不是又退回 ForegroundLight 了？");
                        }
                    }

                    Assert.True(failures.Count == 0,
                        "侧栏次要文字不达标：\n" + string.Join("\n", failures));
                }
                finally
                {
                    restore();
                }
            });
        }

        /// <summary>
        /// 用户报的 bug：浅色主题下左侧侧栏还是深色系。
        /// 侧栏底色原先是写死的 #0F172A，现在必须跟着主题走（浅色浅底 / 深色深底）。
        /// 直接拿真实壳的侧栏 Border 断言，防的是「XAML 里又被人写回一个固定色」。
        /// </summary>
        [Fact]
        public void ShellSidebar_FollowsThemeInsteadOfStayingDark()
        {
            _host.Invoke(() =>
            {
                var theme = ContainerLocator.Container.Resolve<ThemeService>();
                var restore = RedirectThemeConfig(theme);
                try
                {
                    var window = Application.Current.MainWindow;
                    Assert.NotNull(window);
                    var sidebar = FindSidebar(window!);

                    foreach (var dark in new[] { false, true })
                    {
                        theme.SetTheme(dark, theme.Primary, theme.Secondary);
                        Pump(window!);

                        // 键的值随主题变，必须每轮重新取。
                        var cardBg = ((SolidColorBrush)Application.Current.FindResource("MaterialDesign.Brush.Card.Background")).Color;
                        var actual = ((SolidColorBrush)sidebar.Background).Color;
                        Assert.Equal(cardBg, actual);

                        // 兜底：浅色主题下必须是浅底（写死的 #0F172A R=15 会在这里挂掉）
                        Assert.True(dark ? actual.R < 120 : actual.R > 200,
                            $"{(dark ? "深色" : "浅色")}主题下侧栏底色异常：{actual}");
                    }
                }
                finally
                {
                    restore();
                }
            });
        }

        /// <summary>
        /// 端到端守一条硬约束：侧栏那个 ThemePickerView 必须真的接上宿主 DI 的单例。
        /// 曾经的 bug：控件在构造里写了 `DataContext = ViewModel`，于是宿主按文档写的
        /// `Theme="{Binding Theme}"` 被静默引到控件自己 VM 的 Theme 上，宿主单例永远进不来，
        /// 控件用的是自建兜底实例。症状极具迷惑性 —— 换肤照样能换（两份 ThemeService
        /// 改的是同一个 BundledTheme），只是状态各说各话：辅色下拉显示旧值、别的消费方读到过期状态。
        /// 游离视图上验不出来，必须拿真实壳的可视树。
        /// </summary>
        [Fact]
        public void ShellPicker_IsBoundToTheHostSingleton()
        {
            _host.Invoke(() =>
            {
                var window = Application.Current.MainWindow;
                Assert.NotNull(window);

                var picker = FindDescendant<ThemePickerView>(window!);
                Assert.NotNull(picker);

                var singleton = ContainerLocator.Container.Resolve<ThemeService>();
                Assert.Same(singleton, picker!.ViewModel.Theme);
            });
        }

        /// <summary>
        /// 侧栏导航徽标 chip 的文字必须在**它自己的 chip 底**上可读。
        /// <para>
        /// 曾经的 bug：chip 照抄了 NavItemText 的做法，选中态把前景改成 Primary.Foreground，
        /// 结果深色主题下变成「黑字压在 #2E3C43 深灰 chip 上」—— 对比度只有 1.8:1，几乎看不清。
        /// 原因很直接：chip 有自己的底色，不能沿用「主色上的可读前景」这条规则。
        /// </para>
        /// 必须扫**全部主色**：浅色主题下 `Chip.Background` 是半透明 `#12000000`，
        /// 选中行的 pill 颜色会透上来，所以 chip 的实际底色也跟着主色走 —— 只验 Teal 会漏掉深色主色。
        /// 这里不比对画刷对象，而是按 WCAG 相对亮度算**真实对比度**（含半透明层合成），下限 4.5:1。
        /// </summary>
        [Fact]
        public void NavChip_TextStaysReadableOnItsOwnBackground_InBothThemes()
        {
            _host.Invoke(() =>
            {
                var theme = ContainerLocator.Container.Resolve<ThemeService>();
                var restore = RedirectThemeConfig(theme);
                try
                {
                    var window = Application.Current.MainWindow;
                    Assert.NotNull(window);
                    var sidebar = FindSidebar(window!);

                    var colors = new ThemePickerView().ViewModel.PrimaryColors.ToList();
                    var failures = new List<string>();

                    foreach (var dark in new[] { false, true })
                    {
                        foreach (var color in colors)
                        {
                            theme.SetTheme(dark, color, theme.Secondary);
                            Pump(window!);

                            // 只取侧栏里的 chip —— 内容区页面上也可能有 Chip，全窗口捞会捞到无关对象。
                            var chips = FindDescendants<Chip>(sidebar).ToList();
                            Assert.Equal(3, chips.Count);   // 侧栏三个导航项各一个徽标

                            foreach (var chip in chips)
                            {
                                // 显式 Style 若不写 BasedOn，会把 MD 的隐式样式整个顶掉 ——
                                // 那样 Template 为空，Background 设了也不会被绘制（本仓踩过一次）。
                                Assert.NotNull(chip.Template);

                                // 取**真实解析后**的前景/底色（含 DataTrigger 的 Setter），而不是去查资源键：
                                // 这条用例要防的正是「触发器把前景改坏了」，查键等于绕开被测对象。
                                var ratio = ContrastOf(chip, chip.Foreground, chip.Background);
                                if (ratio < 4.5)
                                {
                                    failures.Add(
                                        $"{(dark ? "深色" : "浅色")} + {color}「{chip.Content}」："
                                        + $"{ratio:F2}:1（{chip.Foreground}）");
                                }
                            }
                        }
                    }

                    Assert.True(failures.Count == 0,
                        $"共 {failures.Count} 个「主题 × 主色」组合下 chip 文字对比度不足 4.5:1：\n"
                        + string.Join("\n", failures));
                }
                finally
                {
                    restore();
                }
            });
        }

        /// <summary>
        /// 导航项标题在选中行上走的是 `Primary.Foreground`（「主色上的可读前景」），
        /// 这条规则必须对**所有可选主色**都成立 —— 只在默认的 Teal 上验一次等于没验：
        /// 浅色主色（Amber / Lime）与深色主色（Indigo / Brown）算出来的前景是两个方向。
        /// 覆盖的是「用户随手换个主色，侧栏反而看不清了」这类只在特定配色下才暴露的问题。
        /// </summary>
        [Fact]
        public void NavTitle_StaysReadableForEveryPrimaryColor()
        {
            _host.Invoke(() =>
            {
                var theme = ContainerLocator.Container.Resolve<ThemeService>();
                var restore = RedirectThemeConfig(theme);
                try
                {
                    var window = Application.Current.MainWindow;
                    Assert.NotNull(window);
                    var list = FindDescendant<ListBox>(FindSidebar(window!));
                    Assert.NotNull(list);

                    var colors = new ThemePickerView().ViewModel.PrimaryColors.ToList();
                    Assert.True(colors.Count >= 10, $"可选主色只有 {colors.Count} 个，列表像是被裁剪过");

                    var failures = new List<string>();
                    foreach (var dark in new[] { false, true })
                    {
                        foreach (var color in colors)
                        {
                            theme.SetTheme(dark, color, theme.Secondary);
                            Pump(window!);

                            // 选中行的标题 —— 侧栏里唯一压在 Primary 上的文字，也是唯一走 Primary.Foreground 的地方。
                            var item = (ListBoxItem)list!.ItemContainerGenerator.ContainerFromIndex(0)!;

                            // ⚠️ 别用 FindDescendant<TextBlock>(item)：深度优先第一个命中的是 **chip 模板内部**
                            // 那个显示徽标的 TextBlock，量出来的会是 chip 的文字。按 Style 引用精确定位标题。
                            var navTextStyle = (Style)Application.Current.FindResource("NavItemText");
                            var title = FindDescendants<TextBlock>(item)
                                .FirstOrDefault(t => ReferenceEquals(t.Style, navTextStyle));
                            Assert.NotNull(title);

                            var ratio = ContrastOf(title!, title!.Foreground, ownBackground: null);
                            if (ratio < 4.5)
                                failures.Add($"{(dark ? "深色" : "浅色")} + {color}：{ratio:F2}:1（{title.Foreground}）");

                            // 只断言「对比度够」是**不够的**：万一触发器哪天失灵、标题退回 MaterialDesign.Brush.Foreground，
                            // 深色主色在浅色主题下照样能蒙混过关。这里把机制也钉死 —— 选中态必须真的换成 Primary.Foreground。
                            var expected = ((SolidColorBrush)Application.Current.FindResource("MaterialDesign.Brush.Primary.Foreground")).Color;
                            Assert.Equal(expected, ((SolidColorBrush)title.Foreground).Color);
                        }
                    }

                    Assert.True(failures.Count == 0,
                        $"共 {failures.Count}/{colors.Count * 2} 个「主题 × 主色」组合下导航标题对比度不足 4.5:1：\n"
                        + string.Join("\n", failures));
                }
                finally
                {
                    restore();
                }
            });
        }

        /// <summary>
        /// 侧栏导航项**悬停**时的标题也必须可读。
        /// <para>
        /// 曾经的 bug：悬停底色写死 `#1E293B`（深 navy）。侧栏底色是跟随主题的
        /// `Card.Background`（浅 `#FFFFFF` / 深 `#454545`），而标题前景也跟随浅深 ——
        /// 于是浅色主题下变成「近黑的字压在深 navy 上」，实测只有 **1.40:1**，
        /// 鼠标一划过去文字就没了（深色主题下反而有 12:1，所以只在浅色下暴露）。
        /// 换成 MD 的半透明叠层 `ListView.Hover`（浅 `#19000000` / 深 `#19FFFFFF`）后
        /// 实测 **14.30:1 / 6.20:1**。
        /// </para>
        /// 为什么不直接模拟鼠标：`IsMouseOver` 是只读 DP，测试里造不出 hover 态。
        /// 所以改成「把触发器实际给的画刷取出来，在两种主题下各解析一次」，再看两件事：
        /// ① **跟随主题**：两种主题下解析出来的颜色必须不同（写死的画刷两次必然一样）；
        /// ② **够不够读**：按 WCAG 算含半透明合成的对比度，下限 4.5:1。
        /// 两条缺一不可 —— 只验 ① 会放过「跟随主题但读不清」的键，只验 ② 会放过
        /// 「恰好够读但写死」的颜色。
        /// ⚠️ 断言必须建立在**触发器真正引用的那个画刷**上，不能自己去 `FindResource`
        /// 一个写死的键名 —— 那样测试与被测对象就脱钩了，改触发器它也不会红（本用例第一版就这么栽的：
        /// 把触发器换成 `MaterialDesign.Brush.Foreground` 后照样绿）。
        /// </summary>
        [Fact]
        public void NavHover_TextStaysReadableInBothThemes()
        {
            _host.Invoke(() =>
            {
                var theme = ContainerLocator.Container.Resolve<ThemeService>();
                var restore = RedirectThemeConfig(theme);
                try
                {
                    var window = Application.Current.MainWindow;
                    Assert.NotNull(window);
                    var sidebar = FindSidebar(window!);

                    // 悬停触发器**真正引用**的那个画刷（而不是我们自己写死一个键名去查 —— 那样会脱钩）。
                    var hoverSetter = FindNavHoverBackgroundSetter();

                    var resolved = new List<(bool Dark, Color Hover, Color Foreground)>();
                    foreach (var dark in new[] { false, true })
                    {
                        theme.SetTheme(dark, PrimaryColor.Teal, theme.Secondary);
                        Pump(window!);

                        // 悬停时那一行实际压的底色 = 触发器的画刷合成到侧栏底色上。
                        var hoverLayer = ((SolidColorBrush)ResolveHoverBrush(hoverSetter)).Color;
                        var sidebarBg = ((SolidColorBrush)sidebar.Background).Color;

                        // 悬停时标题仍是默认前景 —— 只有「选中」才换 Primary.Foreground，
                        // 而选中与悬停是两个互不相干的触发器。
                        var foreground = ((SolidColorBrush)Application.Current
                            .FindResource("MaterialDesign.Brush.Foreground")).Color;

                        resolved.Add((dark, Over(hoverLayer, sidebarBg), foreground));
                    }

                    // ── ① 跟随主题：写死的画刷两种主题下解析出来是同一个颜色 ──
                    Assert.NotEqual(resolved[0].Hover, resolved[1].Hover);

                    // ── ② 够不够读：按真实合成结果算 WCAG 对比度 ──
                    var failures = new List<string>();
                    foreach (var r in resolved)
                    {
                        var rendered = Over(r.Foreground, r.Hover);
                        var ratio = ContrastRatio(rendered, r.Hover);
                        if (ratio < 4.5)
                            failures.Add($"{(r.Dark ? "深色" : "浅色")}：{ratio:F2}:1（{rendered} 压 {r.Hover}）");
                    }

                    Assert.True(failures.Count == 0,
                        "侧栏悬停态标题对比度不足 4.5:1：\n" + string.Join("\n", failures));
                }
                finally
                {
                    restore();
                }
            });
        }

        /// <summary>
        /// 从 `NavListBox` 样式里挖出「悬停时给 `bd` 设 `Background`」那个 Setter。
        /// 注意 `bd.Background` 在**选中**触发器里也被设过，所以必须按触发器的
        /// `Property == IsMouseOver` 先把悬停那一个挑出来，不能直接取第一个 Background Setter。
        /// </summary>
        private static Setter FindNavHoverBackgroundSetter()
        {
            var listStyle = (Style)Application.Current.FindResource("NavListBox");

            var containerStyle = (Style)listStyle.Setters
                .OfType<Setter>()
                .First(s => s.Property == ListBox.ItemContainerStyleProperty).Value!;

            var template = (ControlTemplate)containerStyle.Setters
                .OfType<Setter>()
                .First(s => s.Property == Control.TemplateProperty).Value!;

            foreach (var trigger in template.Triggers.OfType<Trigger>())
            {
                if (trigger.Property != UIElement.IsMouseOverProperty)
                    continue;

                var setter = trigger.Setters
                    .OfType<Setter>()
                    .FirstOrDefault(s => s.TargetName == "bd" && s.Property == Panel.BackgroundProperty);
                if (setter is not null)
                    return setter;
            }

            throw new InvalidOperationException("NavListBox 里找不到「悬停时给 bd 设 Background」的触发器");
        }

        /// <summary>
        /// 取出悬停触发器**实际会给**的那个画刷：写死的颜色就是画刷本身，
        /// `{DynamicResource X}` 则按**当前主题**把 X 解析出来。
        /// 认不出的类型直接抛（带上类型名），免得测试静默退化成空转。
        /// </summary>
        private static Brush ResolveHoverBrush(Setter setter) => setter.Value switch
        {
            Brush brush => brush,
            DynamicResourceExtension { ResourceKey: { } key } => (Brush)Application.Current.FindResource(key),
            var other => throw new InvalidOperationException(
                $"认不出悬停底色的类型：{other?.GetType().FullName ?? "null"}"),
        };

        /// <summary>可视树里第一个 <typeparamref name="T"/>。</summary>
        private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T hit) return hit;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var found = FindDescendant<T>(VisualTreeHelper.GetChild(root, i));
                if (found is not null) return found;
            }
            return null;
        }

        /// <summary>可视树里所有 <typeparamref name="T"/>。</summary>
        private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T hit) yield return hit;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                foreach (var found in FindDescendants<T>(VisualTreeHelper.GetChild(root, i)))
                    yield return found;
            }
        }

        /// <summary>
        /// 把 <paramref name="element"/> 祖先链上的背景自外向内叠一遍，得到它实际压着的底色。
        /// 中间那些 Transparent 的层（未选中行的 bd、ListBox 自身）会被自然跳过。
        /// </summary>
        private static Color CompositeBackdrop(DependencyObject element)
        {
            var layers = new List<Color>();
            for (var node = VisualTreeHelper.GetParent(element);
                 node is not null;
                 node = VisualTreeHelper.GetParent(node))
            {
                if (node is Border { Background: SolidColorBrush { Color.A: > 0 } brush })
                    layers.Add(brush.Color);
            }

            // layers 是「由内到外」；最外层那层是不透明的，从它开始往里叠。
            var acc = layers.Count > 0 ? layers[^1] : Colors.White;
            for (var i = layers.Count - 2; i >= 0; i--)
                acc = Over(layers[i], acc);
            return acc;
        }

        /// <summary>
        /// 元素**真实呈现**的对比度：前景按 alpha 压在「元素自身底色 + 祖先链底色」合成出的实际底色上，
        /// 再算 WCAG 比值。<paramref name="ownBackground"/> 传 null 表示元素自身不铺底
        /// （如导航项标题那个 TextBlock，直接压在行的底色上）。
        /// </summary>
        private static double ContrastOf(DependencyObject element, Brush foreground, Brush? ownBackground)
        {
            var backdrop = CompositeBackdrop(element);
            var bg = ownBackground is SolidColorBrush own ? Over(own.Color, backdrop) : backdrop;
            var fg = Over(((SolidColorBrush)foreground).Color, bg);
            return ContrastRatio(fg, bg);
        }

        /// <summary>半透明前景压在不透明底上之后，实际呈现出来的颜色。</summary>
        private static Color Over(Color fg, Color bg)
        {
            var a = fg.A / 255.0;
            return Color.FromRgb(
                (byte)Math.Round(fg.R * a + bg.R * (1 - a)),
                (byte)Math.Round(fg.G * a + bg.G * (1 - a)),
                (byte)Math.Round(fg.B * a + bg.B * (1 - a)));
        }

        /// <summary>WCAG 对比度（1:1 ~ 21:1）。正文可读下限一般取 4.5:1。</summary>
        private static double ContrastRatio(Color a, Color b)
        {
            var la = RelativeLuminance(a);
            var lb = RelativeLuminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        /// <summary>
        /// 测试相关入口 RelativeLuminance；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Pow、Channel；具体条件和先后次序见方法体。
        /// </summary>
        private static double RelativeLuminance(Color c)
        {
            static double Channel(byte v)
            {
                var s = v / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
        }

        /// <summary>真实壳里 Grid.Column=0 的那个侧栏 Border。</summary>
        private static Border FindSidebar(Window window)
        {
            var grid = (Grid)window.Content;
            foreach (var child in grid.Children)
            {
                if (child is Border border && Grid.GetColumn(border) == 0)
                    return border;
            }
            throw new InvalidOperationException("MainWindow 里找不到 Grid.Column=0 的侧栏 Border");
        }

        /// <summary>
        /// 测试相关入口 ShowColorPickersFalse_HidesPickersButKeepsSegmentButtons；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Invoke、ShowInWindow、Equal、FindColorPickerPanel、FindLightButton；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ShowColorPickersFalse_HidesPickersButKeepsSegmentButtons()
        {
            _host.Invoke(() =>
            {
                var view = new ThemePickerView();
                var window = ShowInWindow(view);
                try
                {
                    Assert.Equal(Visibility.Visible, FindColorPickerPanel(view).Visibility);
                    Assert.Equal(Visibility.Visible, FindLightButton(view).Visibility);
                    Assert.Equal(Visibility.Visible, FindDarkButton(view).Visibility);

                    view.ShowColorPickers = false;
                    Pump(window);

                    // 配色整块收起，但浅深切换不能跟着消失 —— 否则这控件就只剩个空壳了。
                    Assert.Equal(Visibility.Collapsed, FindColorPickerPanel(view).Visibility);
                    Assert.Equal(Visibility.Visible, FindLightButton(view).Visibility);
                    Assert.Equal(Visibility.Visible, FindDarkButton(view).Visibility);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        /// <summary>
        /// 测试相关入口 Pickers_OnlyOfferColorsThatCanBePreviewed；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Invoke、NotEmpty、All、NotNull、Resolve；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Pickers_OnlyOfferColorsThatCanBePreviewed()
        {
            _host.Invoke(() =>
            {
                var view = new ThemePickerView();

                // 枚举里混着 PrimaryColor.Inherit 这种「跟随上层」的占位值：
                // 它没有颜色，列出来用户只会选到一块空白，必须被过滤掉。
                Assert.NotEmpty(view.ViewModel.PrimaryColors);
                Assert.NotEmpty(view.ViewModel.SecondaryColors);
                Assert.All(view.ViewModel.PrimaryColors, c => Assert.NotNull(ThemeColorToBrushConverter.Resolve(c)));
                Assert.All(view.ViewModel.SecondaryColors, c => Assert.NotNull(ThemeColorToBrushConverter.Resolve(c)));
                Assert.DoesNotContain(PrimaryColor.Inherit, view.ViewModel.PrimaryColors);
            });
        }

        /// <summary>
        /// 测试相关入口 SwatchColor_MatchesTheAppliedThemeBrush；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Invoke、Resolve、RedirectThemeConfig、SetTheme、FindResource；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SwatchColor_MatchesTheAppliedThemeBrush()
        {
            _host.Invoke(() =>
            {
                var theme = ContainerLocator.Container.Resolve<ThemeService>();
                var restore = RedirectThemeConfig(theme);
                try
                {
                    theme.SetTheme(false, PrimaryColor.Indigo, SecondaryColor.Teal);
                    Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);

                    // 预览色块必须和 BundledTheme 真正应用的主色一致，否则预览就是在骗人。
                    var applied = Application.Current.FindResource("MaterialDesign.Brush.Primary") as SolidColorBrush;
                    Assert.NotNull(applied);
                    Assert.Equal(ThemeColorToBrushConverter.Resolve(PrimaryColor.Indigo), applied!.Color);
                }
                finally
                {
                    restore();
                }
            });
        }

        /// <summary>
        /// 测试相关入口 FindLightButton；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 FindName；具体条件和先后次序见方法体。
        /// </summary>
        private static Button FindLightButton(ThemePickerView view) =>
            (Button)view.FindName("LightThemeButton")!;

        /// <summary>
        /// 测试相关入口 FindDarkButton；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 FindName；具体条件和先后次序见方法体。
        /// </summary>
        private static Button FindDarkButton(ThemePickerView view) =>
            (Button)view.FindName("DarkThemeButton")!;

        // 配色块容器现在是 Border（原来叫 PanelBorder 的那一层），这里只要 Visibility，按 FrameworkElement 取。
        private static FrameworkElement FindColorPickerPanel(ThemePickerView view) =>
            (FrameworkElement)view.FindName("ColorPickerPanel")!;

        /// <summary>取分段按钮模板里那个药丸 Border（模板内 x:Name="Bg"）。</summary>
        private static Border FindPill(Button button)
        {
            button.ApplyTemplate();
            var pill = (Border?)button.Template.FindName("Bg", button);
            Assert.NotNull(pill);
            return pill!;
        }

        /// <summary>
        /// 分段凹槽那层 Border：从分段按钮往上走，第一个 Border 祖先就是它
        /// （按钮模板里的 Border 在按钮自己的可视树内部，往上走不会撞到）。
        /// </summary>
        private static Border FindSegmentContainer(ThemePickerView view)
        {
            DependencyObject? cur = FindLightButton(view);
            while (cur is not null)
            {
                if (cur is Border border)
                    return border;

                cur = VisualTreeHelper.GetParent(cur);
            }

            throw new InvalidOperationException("找不到分段凹槽 Border");
        }

        /// <summary>
        /// 把控件挂进一个不抢焦点、屏幕外的窗口并跑完布局：
        /// 样式触发器（高亮态）与 Visibility 都要在真实视觉树里才算数。
        /// </summary>
        private static Window ShowInWindow(ThemePickerView view)
        {
            var window = new Window
            {
                Content = view,
                Width = 300,
                Height = 300,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false,
                Left = -4000,
                Top = -4000,
            };

            window.Show();
            Pump(window);
            return window;
        }

        /// <summary>
        /// 测试相关入口 Pump；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 UpdateLayout、Invoke；具体条件和先后次序见方法体。
        /// </summary>
        private static void Pump(Window window)
        {
            window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        }

        /// <summary>
        /// 把 <see cref="ThemeService.ConfigPath"/> 指向临时文件，并返回「还原」委托：
        /// 用例会真的切换主题（落盘），不能污染运行目录里的 Config/Theme.json；
        /// 还原时还要把主题状态改回去，避免影响后续用例。
        /// 全部操作都留在 UI 线程上（改主题会动 Application.Resources）。
        /// </summary>
        private static Action RedirectThemeConfig(ThemeService theme)
        {
            var originalPath = ThemeService.ConfigPath;
            var tempPath = Path.Combine(Path.GetTempPath(), $"theme-picker-{Guid.NewGuid():N}.json");
            var before = (theme.IsDark, theme.Primary, theme.Secondary);

            ThemeService.ConfigPath = tempPath;

            return () =>
            {
                theme.SetTheme(before.IsDark, before.Primary, before.Secondary);
                ThemeService.ConfigPath = originalPath;
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            };
        }
    }
}
