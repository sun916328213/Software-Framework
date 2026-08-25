using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using HalconDotNet;


namespace WPFControl.CustomControl.Views.Display
{
    /// <summary>
    /// DisplaySingleGroupView.xaml 的交互逻辑
    /// </summary>
    public partial class DisplaySingleGroupView :  UserControl
    {
        public static readonly DependencyProperty DisplayImageProperty;
        public static readonly DependencyProperty TitleProperty;

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
            var control = (DisplaySingleGroupView)d;
            HObject image = e.NewValue as HObject;
            control.HalconWindow.Image =(image);
 
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

        public DisplaySingleGroupView()
        {
            InitializeComponent();

        }

        static DisplaySingleGroupView()
        {
            DisplayImageProperty = DependencyProperty.Register("DisplayImage", typeof(HObject), typeof(DisplaySingleGroupView), new FrameworkPropertyMetadata(null, OnDisplayImage));
            TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(DisplaySingleGroupView), new FrameworkPropertyMetadata(null));
        }


    }
}