using HalconDotNet;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;


namespace WPFControl.CustomControl.Views.Display
{
    /// <summary>
    /// DisplaySingleView.xaml 的交互逻辑
    /// </summary>
    public partial class DisplaySingleView_DP :  UserControl
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

        public DisplaySingleView_DP()
        {
            InitializeComponent();

        }

        static DisplaySingleView_DP()
        {
            DisplayImageProperty = DependencyProperty.Register("DisplayImage", typeof(HObject), typeof(DisplaySingleView_DP), new FrameworkPropertyMetadata(null));
            TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(DisplaySingleView_DP), new FrameworkPropertyMetadata(null));
        }

        /// <summary>
        /// 获取显示图像
        /// </summary>
        /// <returns></returns>
        public HObject GetDisplayImage()
        {
            using var raw = HalconWindow.HSmart?.HalconWindow?.DumpWindowImage();
            if (raw == null)
            {
                return null;
            }
            return raw.Clone();
        }

        /// <summary>
        /// 清除显示
        /// </summary>
        /// <returns></returns>
        public void  Clear()
        {
              HalconWindow.HSmart?.HalconWindow?.ClearWindow();
        }
    }
}
