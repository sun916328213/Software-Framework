using Prism.Ioc;
using Prism.Regions;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shell;
using System.Xml;
using WPFControl.ProgramControl.Services;

namespace WPFControl.WindowUI
{
    /// <summary>
    /// 主窗口基类，提供通用的窗口框架和标题栏功能
    /// </summary>
    public class BaseMainWindow : Window
    {
        protected IContainerProvider Container;
        protected WindowMenuService WindowMenuService;

        /// <summary>
        /// 标题栏高度，子类可重写
        /// </summary>
        protected virtual double TitleBarHeight => 35;

        public BaseMainWindow()
        {
            // 设置窗口基本属性
            this.Width = 1600;
            this.Height = 900;
            this.Background = Brushes.Transparent;
            this.FontSize = 14;
            this.Margin = new Thickness(0);
            this.BorderThickness = new Thickness(0);
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.WindowStyle = WindowStyle.None;
            this.SnapsToDevicePixels = true;
            this.Title = "主窗口";

            // 设置WindowChrome
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 1,// 自定义标题栏高度设为1，隐藏原生系统标题栏，实现自定义标题区域
                CornerRadius = new CornerRadius(12),//圆角
                GlassFrameThickness = new Thickness(1),// 透明玻璃边框厚度1，配合Aero/亚克力效果使用
                ResizeBorderThickness = new Thickness(4),// 窗口拖拽缩放边框宽度4px，增大拖拽识别区域，方便拉伸缩放窗口
                UseAeroCaptionButtons = false// 关闭系统自带Aero标题栏最小化/最大化/关闭按钮，完全自定义标题按钮
            });

            // 注册标题栏拖动事件
            this.MouseLeftButtonDown += BaseMainWindow_MouseLeftButtonDown;
            this.Loaded += BaseMainWindow_Loaded;
        }

        /// <summary>
        /// 异步显示窗口，先执行初始化操作
        /// </summary>
        public async Task ShowAsync(Func<Task> initAction)
        {
            await initAction?.Invoke();
            Show();
        }

        private void BaseMainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            OnWindowLoaded();
        }

        /// <summary>
        /// 窗口加载完成后的钩子方法，子类可重写
        /// </summary>
        protected virtual void OnWindowLoaded()
        {
            WindowMenuService = Container?.Resolve<WindowMenuService>();
        }


        private void BaseMainWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 使用子类定义的标题栏高度
            if (e.GetPosition(this).Y <= TitleBarHeight)
            {
                if (e.ClickCount == 2)
                {
                    // 双击切换最大化/还原
                    WindowMenuService?.ToggleMaximize();
                }
                else
                {
                    // 拖动窗口
                    this.DragMove();
                }
            }
        }
    }
}
