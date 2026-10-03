// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：DisplaySingleView_DP。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、WPFControls.Controls.Views、HalconDotNet、System.ComponentModel、System.Windows、System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using WPFControls;
using WPFControls.Controls.Views;
using HalconDotNet;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;


namespace WPFControls.ProgramControl.Views.Display
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

        /// <summary>
        /// 初始化 DisplaySingleView_DP 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public DisplaySingleView_DP()
        {
            InitializeComponent();

        }

        /// <summary>
        /// 初始化 DisplaySingleView_DP 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Register；具体条件和先后次序见方法体。
        /// </summary>
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
