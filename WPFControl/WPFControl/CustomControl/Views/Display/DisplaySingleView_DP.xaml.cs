using DPLibrary_Halcon_F48.Tools;
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
        public static readonly DependencyProperty HToolProperty;
        public static readonly DependencyProperty TitleProperty;
 

        /// <summary>
        /// 显示图像
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("视觉工具"), DisplayName("视觉工具")]
        public HVisionTool HTool
        {
            get { return (HVisionTool)GetValue(HToolProperty); }
            set { SetValue(HToolProperty, value); }
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
            HToolProperty = DependencyProperty.Register("HTool", typeof(HVisionTool), typeof(DisplaySingleView_DP), new FrameworkPropertyMetadata(null));
            TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(DisplaySingleView_DP), new FrameworkPropertyMetadata(null));
        }

        /// <summary>
        /// 获取显示图像
        /// </summary>
        /// <returns></returns>
        public HObject GetDisplayImage()
        {
            return HalconWindow.GetDisplayImage();
        }

        /// <summary>
        /// 获取显示图像
        /// </summary>
        /// <returns></returns>
        public void  Clear()
        {
              HalconWindow.Clear();
        }
    }
}