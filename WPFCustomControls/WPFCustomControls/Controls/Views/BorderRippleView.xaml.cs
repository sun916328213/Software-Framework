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

        private static void OnForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BorderRippleView control)
            {
                control.TextDisplay.Foreground = (Brush)e.NewValue;
            }
        }
    }
}
