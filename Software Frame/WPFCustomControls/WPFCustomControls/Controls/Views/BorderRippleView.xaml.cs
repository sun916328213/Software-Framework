// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：BorderRippleView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、MaterialDesignThemes.Wpf、System、System.Collections.Generic、System.Linq、System.Text。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WPFCustomControls.Controls
{
    /// <summary>
    /// BorderRippleView.xaml 的交互逻辑
    /// </summary>
    public partial class BorderRippleView : UserControl
    {
        /// <summary>
        /// 初始化 BorderRippleView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public BorderRippleView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 获取或设置图标
        /// </summary>
        public PackIconKind Icon
        {
            get { return (PackIconKind)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        /// <summary>
        /// Icon依赖属性
        /// </summary>
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register("Icon", typeof(PackIconKind), typeof(BorderRippleView),
                new PropertyMetadata(PackIconKind.Help, OnIconChanged));

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnIconChanged。
        /// </summary>
        private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BorderRippleView control)
            {
                control.IconDisplay.Kind = (PackIconKind)e.NewValue;
            }
        }

        /// <summary>
        /// 获取或设置图标
        /// </summary>
        public PackIconKind SelectIcon
        {
            get { return (PackIconKind)GetValue(SelectIconProperty); }
            set { SetValue(SelectIconProperty, value); }
        }

        /// <summary>
        /// Icon依赖属性
        /// </summary>
        public static readonly DependencyProperty SelectIconProperty =
            DependencyProperty.Register("SelectIcon", typeof(PackIconKind), typeof(BorderRippleView),
                new PropertyMetadata(PackIconKind.Help, OnIconChanged));

     

        /// <summary>
        /// 获取或设置文本
        /// </summary>
        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        /// <summary>
        /// Text依赖属性
        /// </summary>
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(BorderRippleView),
                new PropertyMetadata(string.Empty, OnTextChanged));

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnTextChanged。
        /// </summary>
        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BorderRippleView control)
            {
                control.TextDisplay.Text = (string)e.NewValue;
            }
        }

        /// <summary>
        /// 获取或设置边框圆角
        /// </summary>
        public CornerRadius CornerRadius
        {
            get { return (CornerRadius)GetValue(CornerRadiusProperty); }
            set { SetValue(CornerRadiusProperty, value); }
        }

        /// <summary>
        /// CornerRadius依赖属性
        /// </summary>
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register("CornerRadius", typeof(CornerRadius), typeof(BorderRippleView),
                new PropertyMetadata(new CornerRadius(20), OnCornerRadiusChanged));

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnCornerRadiusChanged。
        /// </summary>
        private static void OnCornerRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BorderRippleView control)
            {
                control.MainBorder.CornerRadius = (CornerRadius)e.NewValue;
            }
        }

        /// <summary>
        /// 获取或设置背景色
        /// </summary>
        public new  Brush Background
        {
            get { return (Brush)GetValue(BackgroundProperty); }
            set { SetValue(BackgroundProperty, value); }
        }

        /// <summary>
        /// Background依赖属性
        /// </summary>
        public new static readonly DependencyProperty  BackgroundProperty =
            DependencyProperty.Register("Background", typeof(Brush), typeof(BorderRippleView),
                new PropertyMetadata(Brushes.Transparent, OnBackgroundChanged));

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnBackgroundChanged。
        /// </summary>
        private static void OnBackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BorderRippleView control)
            {
                control.MainBorder.Background = (Brush)e.NewValue;
            }
        }

        /// <summary>
        /// 获取或设置是否显示加载状态
        /// </summary>
        public bool IsLoading
        {
            get { return (bool)GetValue(IsLoadingProperty); }
            set { SetValue(IsLoadingProperty, value); }
        }

        /// <summary>
        /// IsLoading依赖属性
        /// </summary>
        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register("IsLoading", typeof(bool), typeof(BorderRippleView),
                new PropertyMetadata(false, OnIsLoadingChanged));

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnIsLoadingChanged。
        /// </summary>
        private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BorderRippleView control)
            {
                control.LoadingProgressBar.Opacity = (bool)e.NewValue ? 1 : 0;
            }
        }

        /// <summary>
        /// 获取或设置ProgressBar前景色
        /// </summary>
        public Brush ProgressForeground
        {
            get { return (Brush)GetValue(ProgressForegroundProperty); }
            set { SetValue(ProgressForegroundProperty, value); }
        }

        /// <summary>
        /// ProgressForeground依赖属性
        /// </summary>
        public static readonly DependencyProperty ProgressForegroundProperty =
            DependencyProperty.Register("ProgressForeground", typeof(Brush), typeof(BorderRippleView),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x03, 0xa9, 0xf4)), OnProgressForegroundChanged));

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnProgressForegroundChanged。
        /// </summary>
        private static void OnProgressForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BorderRippleView control)
            {
                control.LoadingProgressBar.Foreground = (Brush)e.NewValue;
            }
        }

        /// <summary>
        /// 获取或设置图标大小
        /// </summary>
        public double IconSize
        {
            get { return (double)GetValue(IconSizeProperty); }
            set { SetValue(IconSizeProperty, value); }
        }

        /// <summary>
        /// IconSize依赖属性
        /// </summary>
        public static readonly DependencyProperty IconSizeProperty =
            DependencyProperty.Register("IconSize", typeof(double), typeof(BorderRippleView),
                new PropertyMetadata(35.0, OnIconSizeChanged));

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnIconSizeChanged。
        /// </summary>
        private static void OnIconSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BorderRippleView control)
            {
                control.IconDisplay.Width = (double)e.NewValue;
                control.IconDisplay.Height = (double)e.NewValue;
            }
        }

        /// <summary>
        /// 获取或设置文本颜色
        /// </summary>
        public new Brush Foreground
        {
            get { return (Brush)GetValue(ForegroundProperty); }
            set { SetValue(ForegroundProperty, value); }
        }

        /// <summary>
        /// Foreground依赖属性
        /// </summary>
        public new static readonly DependencyProperty ForegroundProperty =
            DependencyProperty.Register("Foreground", typeof(Brush), typeof(BorderRippleView),
                new PropertyMetadata(Brushes.Black, OnForegroundChanged));

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnForegroundChanged。
        /// </summary>
        private static void OnForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BorderRippleView control)
            {
                control.TextDisplay.Foreground = (Brush)e.NewValue;
            }
        }
    }
}
