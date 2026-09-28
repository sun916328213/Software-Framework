using Vision.Core.Tools;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Vision.Controls.Wpf.Ctr_Halcon.View
{
    public partial class Ctr_ToolOnlyData_DP : UserControl, IDisposable
    {
        public Ctr_ToolOnlyData_DP()
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
                  ownerType: typeof(Ctr_ToolOnlyData_DP), // 所属控件类型
                  new FrameworkPropertyMetadata(
                      "Title", // 默认值
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
            if (d is Ctr_ToolOnlyData_DP control)
            {
                string val = (string)e.NewValue;
                // 这里可以添加属性变化后的逻辑
            }
        }

        #endregion

        #region  依赖属性( 读码器实例 - Tool : Reader)

        /// <summary>
        /// 工具实例附加
        /// </summary>
        public static readonly DependencyProperty ToolProperty =
              DependencyProperty.Register(
                  name: "Tool", // 属性名称
                  propertyType: typeof(ITool), // 属性类型
                  ownerType: typeof(Ctr_ToolOnlyData_DP), // 所属控件类型
                  new FrameworkPropertyMetadata(
                      null, // 默认值
                      FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                      OnReaderChanged // 属性变化回调
                  )
              );

        /// <summary>
        /// 工具实例源
        /// </summary>
        public ITool Tool
        {
            get => (ITool)GetValue(ToolProperty);
            set => SetValue(ToolProperty, value);
        }

        /// <summary>
        /// 工具实例变化时的处理
        /// </summary>
        private static void OnReaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_ToolOnlyData_DP control)
            {
                ITool val = (ITool)e.NewValue;
                if (val != null)
                {
                    // 这里可以添加属性变化后的逻辑
                    control.Title = val.Name;
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
                  ownerType: typeof(Ctr_ToolOnlyData_DP), // 所属控件类型
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
            if (d is Ctr_ToolOnlyData_DP control)
            {
                bool val = (bool)e.NewValue;
                // 这里可以添加属性变化后的逻辑
            }
        }

        #endregion

 
        public void ShowAsWindow() 
        {
            // 创建一个临时 Window 来承载自己
            var win = new Window
            {
                Width = 500,
                Content = this,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Owner = Application.Current.MainWindow,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,                              // 允许透明
                Topmost = true                                          // 始终在最前
            };
            IsShowWindow = true;
            win.ShowDialog();
            win = null;
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
                    Window.GetWindow(this)?.DragMove();
                }
            }
        }

        public void Dispose()
        {
            // 断开引用链，防止环
            DataContext = null;
            Tool = null;
        }

    }
}
