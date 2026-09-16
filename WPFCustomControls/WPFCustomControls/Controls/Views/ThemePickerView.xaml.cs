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

        /// <summary>
        /// 下拉框底色。默认 #1E293B —— 这是给「固定深色侧栏」配的兜底值。
        /// 宿主侧栏若跟随主题（现在 Workbench 就是这样），必须显式传入浅色画刷，
        /// 否则会变成深色输入框压在浅底上。
        /// </summary>
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

        /// <summary>控件自己的视图模型（内部绑定用，宿主无需关心）。</summary>
        public ThemePickerViewModel ViewModel { get; }

        public ThemePickerView()
        {
            ViewModel = new ThemePickerViewModel();
            InitializeComponent();

            // 内部绑定所需的 DataContext 挂在 XAML 根节点上，而**不是**控件自身。
            //
            // ⚠️ 曾经写的是 `DataContext = ViewModel`，那是个静默 bug：
            // 控件自身一旦顶掉 DataContext，宿主按文档写的
            //     <wpfcc:ThemePickerView Theme="{Binding Theme}" />
            // 就会绑到本控件 VM 的 Theme 属性上（构造时刚赋进去的兜底实例），
            // 宿主 DI 里的单例永远进不来。症状极具迷惑性：换肤照样能换
            //（两个 ThemeService 改的是同一个 BundledTheme），只是两份实例的状态各说各话，
            // 表现为「辅色下拉显示的还是旧值」「别的消费方读到的是过期状态」。
            // 现在控件自身的 DataContext 保持继承，宿主可以正常绑自己的 VM。
            RootPanel.DataContext = ViewModel;

            ViewModel.Theme = Theme;
        }

        private static void OnThemePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ThemePickerView)d).ViewModel.Theme = e.NewValue as ThemeService;
        }
    }
}
