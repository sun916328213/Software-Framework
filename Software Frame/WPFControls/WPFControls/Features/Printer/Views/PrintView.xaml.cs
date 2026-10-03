// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：PrintView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、System.Windows、System.Windows.Controls、System.Windows.Media、Prism.Ioc、WPFControls.ProgramControl.ViewModels。导入不代表所有类型都会在运行时加载。
using WPFControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Prism.Ioc;
using WPFControls.ProgramControl.ViewModels;
using System.Collections.Generic;
using WPFControls.Controls.Models;

namespace WPFControls.ProgramControl.Views
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
        /// <summary>
        /// 初始化 PrintView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public PrintView(   )
        {
            InitializeComponent();
        }

       
        /// <summary>
        /// 初始化 PrintView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 RegisterAttached；具体条件和先后次序见方法体。
        /// </summary>
        static PrintView()
        {
            PreviewImageProperty = DependencyProperty.RegisterAttached("PreviewImage", typeof(ImageSource), typeof(PrintView), new FrameworkPropertyMetadata(null));
            PrintInfoProperty = DependencyProperty.RegisterAttached("PrintInfo", typeof(PrintInfo), typeof(PrintView), new FrameworkPropertyMetadata(null));
        }

    }
}