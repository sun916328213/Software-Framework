using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Extension.Core.ThemeEx;
using WPFCustomControls.Controls.ViewModels;

namespace WPFCustomControls.Controls
{
    /// <summary>
    /// 主题选择复用控件：主色 / 辅色选择 + 浅色 / 深色分段按钮，全框架共用一套 UI。
    /// <para>
    /// 与框架的唯一耦合点就是 <see cref="Theme"/> 依赖属性（一个
    /// <see cref="ThemeService"/> 实例）。宿主应传入自己 DI 容器里的单例，
    /// 保证控件改的主题就是全应用在用的那一份；不传则控件自建兜底实例，
    /// 放进任意窗口也能独立工作。
    /// </para>
    /// 换肤逻辑全在 Extension 的 <see cref="ThemeService"/> 里，本控件不持有任何配色常量。
    /// <para>
    /// 宿主若想在换肤时附带副作用（写日志、刷状态栏等），请订阅
    /// <see cref="ThemeService.ThemeChanged"/>，而不是往控件里塞命令：
    /// 那样既能收到本控件触发的变更，也能收到应用里其它来源的变更。
    /// </para>
    /// </summary>
    public partial class ThemePickerView : UserControl
    {
        /// <summary>主题服务（<see cref="ThemeService"/>）。宿主注入单例；null 时控件自建兜底实例。</summary>
        public static readonly DependencyProperty ThemeProperty =
            DependencyProperty.Register(
                nameof(Theme),
                typeof(ThemeService),
                typeof(ThemePickerView),
                new PropertyMetadata(null, OnThemePropertyChanged));

        /// <summary>是否显示主色 / 辅色两个下拉框（只想要浅深切换时置 false）。</summary>
        public static readonly DependencyProperty ShowColorPickersProperty =
            DependencyProperty.Register(
                nameof(ShowColorPickers),
                typeof(bool),
                typeof(ThemePickerView),
                new PropertyMetadata(true));

        /// <summary>下拉框底色。默认 #1E293B（深色品牌侧栏配套，侧栏刻意不随主题变）。</summary>
        public static readonly DependencyProperty FieldBackgroundProperty =
            DependencyProperty.Register(
                nameof(FieldBackground),
                typeof(Brush),
                typeof(ThemePickerView),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B))));

        /// <summary>下拉框文字 / 切换图标前景。默认 #E2E8F0，保证深色底上可读。</summary>
        public static readonly DependencyProperty FieldForegroundProperty =
            DependencyProperty.Register(
                nameof(FieldForeground),
                typeof(Brush),
                typeof(ThemePickerView),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0))));

        /// <summary>可选标题文字，留空则不占位。</summary>
        public static readonly DependencyProperty CaptionProperty =
            DependencyProperty.Register(
                nameof(Caption),
                typeof(string),
                typeof(ThemePickerView),
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// 浅深分段控件的容器底色。默认 #1AFFFFFF（深色品牌侧栏配套）。
        /// 放到跟随主题的浅底上时，宿主需覆盖成主题相关画刷，否则白色半透明在浅底上看不见。
        /// </summary>
        public static readonly DependencyProperty SegmentBackgroundProperty =
            DependencyProperty.Register(
                nameof(SegmentBackground),
                typeof(Brush),
                typeof(ThemePickerView),
                new PropertyMetadata(new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF))));

        /// <summary>浅深分段控件的容器描边。默认 #26FFFFFF（同上，深色侧栏配套）。</summary>
        public static readonly DependencyProperty SegmentBorderProperty =
            DependencyProperty.Register(
                nameof(SegmentBorder),
                typeof(Brush),
                typeof(ThemePickerView),
                new PropertyMetadata(new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF))));

        /// <summary>浅深分段控件「选中那一侧」的底色。默认 #33FFFFFF（深色侧栏配套）。</summary>
        public static readonly DependencyProperty SegmentSelectedBackgroundProperty =
            DependencyProperty.Register(
                nameof(SegmentSelectedBackground),
                typeof(Brush),
                typeof(ThemePickerView),
                new PropertyMetadata(new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF))));

        /// <summary>
        /// 浅深分段控件「选中那一侧」的前景（图标 + 文字）。
        /// 默认 null = 跟随 <see cref="FieldForeground"/>（深色侧栏就是这种情况）；
        /// 宿主把选中态底色换成实色（如 MD 的 Primary）时，应同时给出对比色前景
        /// （如 <c>MaterialDesign.Brush.Primary.Foreground</c>），否则文字会看不清。
        /// </summary>
        public static readonly DependencyProperty SegmentSelectedForegroundProperty =
            DependencyProperty.Register(
                nameof(SegmentSelectedForeground),
                typeof(Brush),
                typeof(ThemePickerView),
                new PropertyMetadata(null));

        [Bindable(true)]
        [Category("自定义属性"), Description("主题服务（ThemeService 单例）"), DisplayName("主题服务")]
        public ThemeService? Theme
        {
            get => (ThemeService?)GetValue(ThemeProperty);
            set => SetValue(ThemeProperty, value);
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("是否显示主色/辅色下拉框"), DisplayName("显示配色选择")]
        public bool ShowColorPickers
        {
            get => (bool)GetValue(ShowColorPickersProperty);
            set => SetValue(ShowColorPickersProperty, value);
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("下拉框底色"), DisplayName("字段底色")]
        public Brush FieldBackground
        {
            get => (Brush)GetValue(FieldBackgroundProperty);
            set => SetValue(FieldBackgroundProperty, value);
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("下拉框文字/图标前景"), DisplayName("字段前景")]
        public Brush FieldForeground
        {
            get => (Brush)GetValue(FieldForegroundProperty);
            set => SetValue(FieldForegroundProperty, value);
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("可选标题，留空不显示"), DisplayName("标题")]
        public string Caption
        {
            get => (string)GetValue(CaptionProperty);
            set => SetValue(CaptionProperty, value);
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("浅深分段控件容器底色"), DisplayName("分段底色")]
        public Brush SegmentBackground
        {
            get => (Brush)GetValue(SegmentBackgroundProperty);
            set => SetValue(SegmentBackgroundProperty, value);
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("浅深分段控件容器描边"), DisplayName("分段描边")]
        public Brush SegmentBorder
        {
            get => (Brush)GetValue(SegmentBorderProperty);
            set => SetValue(SegmentBorderProperty, value);
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("浅深分段控件选中态底色"), DisplayName("分段选中底色")]
        public Brush SegmentSelectedBackground
        {
            get => (Brush)GetValue(SegmentSelectedBackgroundProperty);
            set => SetValue(SegmentSelectedBackgroundProperty, value);
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("浅深分段控件选中态前景，null 跟随字段前景"), DisplayName("分段选中前景")]
        public Brush? SegmentSelectedForeground
        {
            get => (Brush?)GetValue(SegmentSelectedForegroundProperty);
            set => SetValue(SegmentSelectedForegroundProperty, value);
        }

        /// <summary>控件自己的视图模型（内部绑定用，宿主无需关心）。</summary>
        public ThemePickerViewModel ViewModel { get; }

        public ThemePickerView()
        {
            ViewModel = new ThemePickerViewModel();
            // 内部绑定全部走 DataContext，宿主在控件实例上设的属性（如 Theme）不受影响。
            DataContext = ViewModel;
            InitializeComponent();

            // 宿主没注入 Theme 时自建兜底实例（读同一份 Config/Theme.json），
            // 保证控件单独放进任意窗口也能工作。
            ViewModel.Theme = Theme;
        }

        private static void OnThemePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ThemePickerView)d).ViewModel.Theme = e.NewValue as ThemeService;
        }
    }
}
