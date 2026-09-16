using Device.PrintSoftware;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Prism.Ioc;
using WPFControl.CustomControl.ViewModels;
using System.Collections.Generic;
using WPFControl.CustomControl.Models;

namespace WPFControl.CustomControl.Views
{
    /// <summary>  
    /// PrintView.xaml 的交互逻辑  
    /// </summary>  
    public partial class PrintView : UserControl
    {
        public static readonly DependencyProperty PreviewImageProperty;
        public static readonly DependencyProperty PrintInfoProperty;

        /// <summary>  
        /// 获取预览图像  
        /// </summary>  
        /// <param name="obj"></param>  
        /// <returns></returns>  
        public static ImageSource GetPreviewImage(DependencyObject obj)
        {
            return (ImageSource)obj.GetValue(PreviewImageProperty);
        }

        /// <summary>  
        /// 设置预览图像  
        /// </summary>  
        /// <param name="obj"></param>  
        /// <param name="value"></param>  
        public static void SetPreviewImage(DependencyObject obj, object value)
        {
            obj.SetValue(PreviewImageProperty, value);
        }

        /// <summary>  
        /// 获取打印信息  
        /// </summary>  
        /// <param name="obj"></param>  
        /// <returns></returns>  
        public static PrintInfo GetPrintInfo(DependencyObject obj)
        {
            return (PrintInfo)obj.GetValue(PrintInfoProperty);
        }

        /// <summary>  
        /// 设置打印信息  
        /// </summary>  
        /// <param name="obj"></param>  
        /// <param name="value"></param>  
        public static void SetPrintInfo(DependencyObject obj, object value)
        {
            obj.SetValue(PrintInfoProperty, value);
        }
        public PrintView(   )
        {
            InitializeComponent();
        }

       
        static PrintView()
        {
            PreviewImageProperty = DependencyProperty.RegisterAttached("PreviewImage", typeof(ImageSource), typeof(PrintView), new FrameworkPropertyMetadata(null));
            PrintInfoProperty = DependencyProperty.RegisterAttached("PrintInfo", typeof(PrintInfo), typeof(PrintView), new FrameworkPropertyMetadata(null));
        }

    }
}