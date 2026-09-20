using Extension.UI.Controls;
 using Extension.Imaging.Halcon;
using Extension.Imaging.OpenCv;
using HalconDotNet;
using System.Windows;
using System.Windows.Controls;
using Vision.Method;

namespace Vision.Control
{
    /// <summary>
    /// HSmartWindowView.xaml 的交互逻辑
    /// </summary>
    public partial class HSmartWindowView : UserControl
    {
        private byte first_show = 0;

        public static readonly DependencyProperty IsShowCrossProperty =
           DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(HSmartWindowView), new PropertyMetadata(false,OnShowCross));

        /// <summary>
        /// 是否显示十字线
        /// </summary>
        public bool IsShowCross
        {
            get => (bool)GetValue(IsShowCrossProperty);
            set => SetValue(IsShowCrossProperty, value);
        }

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
         DependencyProperty.Register("Image", typeof(object), typeof(HSmartWindowView), new PropertyMetadata(null,OnShowImage));

        /// <summary>
        /// 显示图像
        /// </summary>
        public HObject Image
        {
            get => (HObject) GetValue(ShowImageProperty);
            set => SetValue(ShowImageProperty, value);
        }

        private static void OnShowImage(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            HSmartWindowView hSmartWindowView = (HSmartWindowView)d;
            HObject image = (HObject)e.NewValue;
            HSmartWindowControlWPF hSmart = hSmartWindowView.HSmart;
 
            hSmart.ShowImgae(image);

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