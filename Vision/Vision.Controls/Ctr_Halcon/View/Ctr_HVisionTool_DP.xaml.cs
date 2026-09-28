using Vision.Core.Tools;
using Vision.Core.Vision;
using DPLibrary_Halcon_F48;
using DPLibrary_Halcon_F48.Tools;
using HalconDotNet;
using MaterialDesignThemes.Wpf;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.ConstrainedExecution;
using System.Security.Permissions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

namespace Vision.Controls.Ctr_Halcon.View
{
    public partial class Ctr_HVisionTool_DP : UserControl, INotifyPropertyChanged,IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public Ctr_HVisionTool_DP()
        {
            InitializeComponent();
            
        }
 
        #region  依赖属性( 名称抬头 - string : Title)

        /// <summary>
        /// 名称抬头附加
        /// </summary>
        public static readonly DependencyProperty TitleProperty =
              DependencyProperty.Register(
                  name: "Title", // 属性名称
                  propertyType: typeof(string), // 属性类型
                  ownerType: typeof(Ctr_HVisionTool_DP), // 所属控件类型
                  new FrameworkPropertyMetadata(
                      null, // 默认值
                      FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                      OnTitleChanged // 属性变化回调
                  )
              );

        /// <summary>
        /// 名称抬头源
        /// </summary>
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>
        /// 名称抬头变化时的处理
        /// </summary>
        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_HVisionTool_DP control)
            {
                string val = (string)e.NewValue;
                // 这里可以添加属性变化后的逻辑
            }
        }

        #endregion

        #region  依赖属性( 读码器实例 - HTool : Reader)

        /// <summary>
        /// 工具实例附加
        /// </summary>
        public static readonly DependencyProperty HToolProperty =
              DependencyProperty.Register(
                  name: "HTool", // 属性名称
                  propertyType: typeof(HVisionTool), // 属性类型
                  ownerType: typeof(Ctr_HVisionTool_DP), // 所属控件类型
                  new FrameworkPropertyMetadata(
                      null, // 默认值
                      FrameworkPropertyMetadataOptions.None,
                      OnHToolChanged // 属性变化回调
                  )
              );

        /// <summary>
        /// 工具实例源
        /// </summary>
        public HVisionTool HTool
        {
            get => (HVisionTool)GetValue(HToolProperty);
            set => SetValue(HToolProperty, value);
        }

        /// <summary>
        /// 工具实例变化时的处理
        /// </summary>
        private static void OnHToolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_HVisionTool_DP control)
            {
                HVisionTool val = (HVisionTool)e.NewValue;
                if (val == null)
                {
                    return;
                }
                // 这里可以添加属性变化后的逻辑
                control.Title = val.Name ?? "Tool";

                // 注册图像变化事件：当用户在 Viewer 中加载图片时同步到工具
                control.RegisterViewerImageSync();
            }
        }

        /// <summary>
        /// 注册 Viewer 图像同步：当用户通过加载文件等方式设置图像时，同步到工具的 Image 属性
        /// </summary>
        private void RegisterViewerImageSync()
        {
            if (Viewer == null) return;

            // 移除旧的监听避免重复
            var dpd = DependencyPropertyDescriptor.FromProperty(Ctr_HWindow_DP.ImageSourceProperty, typeof(Ctr_HWindow_DP));
            if (dpd != null)
            {
                dpd.RemoveValueChanged(Viewer, OnViewerImageSourceChanged);
                dpd.AddValueChanged(Viewer, OnViewerImageSourceChanged);
            }
        }

        private void OnViewerImageSourceChanged(object sender, EventArgs e)
        {
            if (HTool != null && Viewer?.ImageSource != null && Viewer.ImageSource.IsInitialized())
            {
                // 只在工具当前没有图像或图像不同步时更新
                if (HTool.Image == null || !HTool.Image.IsInitialized())
                {
                    HTool.Image = Viewer.ImageSource;
                }
            }
        }
 
 
        #endregion

        #region  依赖属性( 是否以Win显示 - bool : IsShowWindow)

        /// <summary>
        /// 是否以Win显示附加
        /// </summary>
        public static readonly DependencyProperty IsShowWindowProperty =
              DependencyProperty.Register(
                  name: "IsShowWindow", // 属性名称
                  propertyType: typeof(bool), // 属性类型
                  ownerType: typeof(Ctr_HVisionTool_DP), // 所属控件类型
                  new PropertyMetadata(
                      false, // 默认值
                      propertyChangedCallback: OnIsShowWindowChanged // 属性变化回调
                  )
              );

        /// <summary>
        /// 是否以Win显示源
        /// </summary>
        public bool IsShowWindow
        {
            get => (bool)GetValue(IsShowWindowProperty);
            set => SetValue(IsShowWindowProperty, value);
        }

        /// <summary>
        /// 是否以Win显示变化时的处理
        /// </summary>
        private static void OnIsShowWindowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_HVisionTool_DP control)
            {
                bool val = (bool)e.NewValue;
                // 这里可以添加属性变化后的逻辑
            }
        }

        #endregion
 
        public void ShowAsWindow()
        {
            var win = new Window
            {
                Content = this,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Owner = Application.Current?.MainWindow,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,                                  // 允许透明
                Topmost = true                                              // 始终在最前
            };
            IsShowWindow = true;

            win.ShowDialog();
            this.Dispose();
        }

        private void btn_Exit_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }

        private void gd_Title_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (IsShowWindow)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    try
                    {
                        Window.GetWindow(this)?.DragMove();
                    }
                    catch
                    {
                        // 拖拽过程中松开鼠标会抛出异常，忽略即可
                    }
                }
            }
        }

        public void Dispose()
        {
            if (Viewer != null)
            {
                // 移除 Viewer 图像变化监听，断开引用链
                var dpd = DependencyPropertyDescriptor.FromProperty(Ctr_HWindow_DP.ImageSourceProperty, typeof(Ctr_HWindow_DP));
                dpd?.RemoveValueChanged(Viewer, OnViewerImageSourceChanged);

                //一定需要释放，否则界面会继续存在，等待GC清除窗体时再释放
                Viewer.Dispose();
                Viewer = null;
            }

            // 断开引用链，防止环
            DataContext = null;
            HTool = null;
        }
    }
}
