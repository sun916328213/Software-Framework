// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：DisplaySingleView_DP。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、WPFControls.Controls.Views、HalconDotNet、System.ComponentModel、System.Windows、System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using WPFControls;
using WPFControls.Controls.Views;
using HalconDotNet;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vision.Controls.View;
using Vision.Core.Imaging;
using Vision.Tools.Halcon.Internal;
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

        /// <summary>共享托管显示区；不创建或拥有编辑会话。</summary>
        public VisionImagePanel ImageDisplay => ManagedDisplay;


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
            ImageDisplay.ImageViewport.IsRoiEditingEnabled = false;

        }

        /// <summary>
        /// 初始化 DisplaySingleView_DP 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Register；具体条件和先后次序见方法体。
        /// </summary>
        static DisplaySingleView_DP()
        {
            DisplayImageProperty = DependencyProperty.Register("DisplayImage", typeof(HObject), typeof(DisplaySingleView_DP), new FrameworkPropertyMetadata(null, OnDisplayImage));
            TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(DisplaySingleView_DP), new FrameworkPropertyMetadata(null));
        }

        private static void OnDisplayImage(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (DisplaySingleView_DP)d;
            var viewport = control.ImageDisplay.ImageViewport;
            viewport.ReportImageError(null);
            if (e.NewValue is HObject image)
            {
                try { viewport.Image = HalconImageConverter.ToVisionImage(image); }
                catch (NotSupportedException error) { viewport.ReportImageError(error.Message); }
            }
        }

        /// <summary>
        /// 获取显示图像
        /// </summary>
        /// <returns></returns>
        public HObject GetDisplayImage()
        {
            var bitmap = CaptureViewport();
            if (bitmap == null) return null;
            var stride = checked(bitmap.PixelWidth * 3);
            var pixels = new byte[checked(stride * bitmap.PixelHeight)];
            bitmap.CopyPixels(pixels, stride, 0);
            // 仍返回当前视口（含缩放/平移）的独立截图，不能用原始输入替代。
            return HalconImageConverter.FromDisplaySnapshot(VisionImage.FromBuffer(
                bitmap.PixelWidth, bitmap.PixelHeight, Vision.Core.Imaging.PixelFormat.Bgr24, pixels));
        }

        /// <summary>捕获当前视口的物理像素，不含标题/菜单；不调用HALCON。未布局时返回null。</summary>
        public BitmapSource CaptureViewport()
        {
            var viewport = ImageDisplay.ImageViewport;
            viewport.VerifyAccess();
            if (viewport.ActualWidth <= 0 || viewport.ActualHeight <= 0) return null;
            var dpi = VisualTreeHelper.GetDpi(viewport);
            var bitmap = new RenderTargetBitmap(
                checked((int)Math.Ceiling(viewport.ActualWidth * dpi.DpiScaleX)),
                checked((int)Math.Ceiling(viewport.ActualHeight * dpi.DpiScaleY)),
                dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            bitmap.Render(viewport);
            var result = new FormatConvertedBitmap(bitmap, PixelFormats.Bgr24, null, 0);
            result.Freeze();
            return result;
        }

        /// <summary>
        /// 清除显示
        /// </summary>
        /// <returns></returns>
        public void  Clear()
        {
            ImageDisplay.ImageViewport.ReportImageError(null);
        }
    }
}
