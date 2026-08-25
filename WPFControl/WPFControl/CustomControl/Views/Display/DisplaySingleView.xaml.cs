using HalconDotNet;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;


namespace WPFControl.CustomControl.Views.Display
{
    /// <summary>
    /// DisplaySingleView.xaml 的交互逻辑
    /// </summary>
    public partial class DisplaySingleView :  UserControl
    {
        public static readonly DependencyProperty DisplayImageProperty;
        public static readonly DependencyProperty TitleProperty;
        public static readonly DependencyProperty HandleProperty;

        /// <summary>
        /// 显示图像
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("显示图像"), DisplayName("显示图像")]
        public HObject DisplayImage
        {
            get { return (HObject)GetValue(DisplayImageProperty); }
            set { SetValue(DisplayImageProperty, value); }
        }

        private static void OnDisplayImage(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (DisplaySingleView)d;
            HObject image = e.NewValue as HObject;
            control.HalconWindow.Image=(image);
        }


        /// <summary>
        /// 显示图像
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("显示控件句柄"), DisplayName("显示控件句柄")]
        public HObject Handle
        {
            get { return (HObject)GetValue(HandleProperty); }
            set { SetValue(HandleProperty, value); }
        }
 

        /// <summary>
        /// 显示标题
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("显示标题"), DisplayName("显示标题")]
        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public DisplaySingleView()
        {
            InitializeComponent();

        }

        static DisplaySingleView()
        {
            DisplayImageProperty = DependencyProperty.Register("DisplayImage", typeof(HObject), typeof(DisplaySingleView), new FrameworkPropertyMetadata(null, OnDisplayImage));
            TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(DisplaySingleView), new FrameworkPropertyMetadata(null));
            HandleProperty = DependencyProperty.Register("Handle", typeof(HWindow), typeof(DisplaySingleView), new FrameworkPropertyMetadata(null));
        }


    }
}