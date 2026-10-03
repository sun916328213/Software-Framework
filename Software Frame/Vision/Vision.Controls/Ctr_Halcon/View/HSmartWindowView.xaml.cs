// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：HSmartWindowView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Controls、Vision.Controls.Ctr_Halcon.Helper、Extension.UI.Controls、Extension.Imaging.Halcon、Extension.Imaging.OpenCv、HalconDotNet。导入不代表所有类型都会在运行时加载。
using Vision.Controls;
using Vision.Controls.Ctr_Halcon.Helper;
using Extension.UI.Controls;
using Extension.Imaging.Halcon;
using Extension.Imaging.OpenCv;
using HalconDotNet;
using System.Windows;
using System.Windows.Controls;

namespace Vision.Controls.Ctr_Halcon.View
{
    /// <summary>
    /// HSmartWindowView.xaml 的交互逻辑
    /// </summary>
    public partial class HSmartWindowView : UserControl
    {
        private byte first_show = 0;

        public static readonly DependencyProperty IsShowCrossProperty =
           DependencyProperty.Register("IsShowCross", typeof(bool), typeof(HSmartWindowView), new PropertyMetadata(false, OnShowCross));

        /// <summary>
        /// 是否显示十字线
        /// </summary>
        public bool IsShowCross
        {
            get => (bool)GetValue(IsShowCrossProperty);
            set => SetValue(IsShowCrossProperty, value);
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnShowCross。
        /// 正文中的关键协作调用包括 ShowCross；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnShowCross(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            bool value = (bool)e.NewValue;
            HSmartWindowView hSmartWindowView = (HSmartWindowView)d;
            HSmartWindowControlWPF hSmart = hSmartWindowView.HSmart;
            if (value == true)
            {
                hSmart.ShowCross((HObject)hSmartWindowView.Image);
            }
            else
            {
                hSmartWindowView.Image = hSmartWindowView.Image;
            }
        }

        public static readonly DependencyProperty ShowImageProperty =
         DependencyProperty.Register("Image", typeof(object), typeof(HSmartWindowView), new PropertyMetadata(null, OnShowImage));

        /// <summary>
        /// 显示图像
        /// </summary>
        public HObject Image
        {
            get => (HObject)GetValue(ShowImageProperty);
            set => SetValue(ShowImageProperty, value);
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnShowImage。
        /// 正文中的关键协作调用包括 ShowImage、Init、Fit、ShowCross；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnShowImage(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            HSmartWindowView hSmartWindowView = (HSmartWindowView)d;
            HObject image = (HObject)e.NewValue;
            HSmartWindowControlWPF hSmart = hSmartWindowView.HSmart;

            hSmart.ShowImage(image);

            if (hSmartWindowView.first_show == 0)
            {
                hSmart.Init();
                hSmart.Fit();
                hSmartWindowView.first_show = 1;
            }
            if (hSmartWindowView.IsShowCross)
            {
                hSmart.ShowCross((HObject)hSmartWindowView.Image);
            }
        }

        /// <summary>
        /// 内部 Halcon 显示控件（供外部直接操作窗口/事件）
        /// </summary>
        public HSmartWindowControlWPF HSmart => HSmartCtl;

        /// <summary>
        /// 初始化 HSmartWindowView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public HSmartWindowView()
        {
            InitializeComponent();
        }

        #region 菜单功能

        // 右键菜单打开事件处理程序
        private void MainGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // 可以在这里添加逻辑，例如根据某些条件决定是否显示菜单
            // e.Handled = true; // 设置为true可以取消菜单显示
        }

        // 菜单项点击事件处理程序
        private void FitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            HSmart.Fit();
        }

        /// <summary>
        /// 执行保存操作；对应入口为 SaveMenuItem_Click。
        /// 正文中的关键协作调用包括 SaveImage、SaveFile；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                HImageEx.SaveImage(Image, FileDialogEx.SaveFile());
            }
            catch { }
        }

        #endregion
    }
}
