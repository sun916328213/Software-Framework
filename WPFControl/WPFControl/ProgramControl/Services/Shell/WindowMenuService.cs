using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Ioc;
using Prism.Services.Dialogs;
using System.Windows;
using WPFControl.ProgramControl.Models;
using WPFCustomControls.Window.Views;

namespace WPFControl.ProgramControl.Services
{
    /// <summary>
    /// 窗口菜单服务
    /// </summary>
    public class WindowMenuService: ObservableObject
    {
        internal WindowMenuModel Model { get; set; } = new WindowMenuModel();

        internal readonly UserInfoService _UserInfoService;
        private readonly  IDialogService _DialogService;
        internal readonly StatisticService _StatisticService;
        
        // 保存窗口正常状态时的位置和尺寸
        private double _normalLeft, _normalTop, _normalWidth, _normalHeight;
        // 标记是否处于"最大化"状态
        private bool _isMaximized = false;
 
        // 标记是否已初始化
        private bool _isInitialized = false;

        public WindowMenuService(IContainerProvider container)
        {
            _UserInfoService = container.Resolve<UserInfoService>();
            _DialogService = container.Resolve<IDialogService>();
            _StatisticService = container.Resolve<StatisticService>();

            // 确保在 UI 线程上访问 MainWindow，避免跨线程调用异常
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Application.Current.MainWindow != null)
                    {
                        Application.Current.MainWindow.StateChanged += (sender, e) =>
                        {
                            if (Application.Current.MainWindow.WindowState == WindowState.Maximized)
                            {
                                _isMaximized = false;
                                ToggleMaximize();
                            }
                        };
                    }
                });
            }
        }

        public void UpdataTitle(string pTitle)
        {
            Model.Title = pTitle;
        }

        /// <summary>
        /// 关闭应用程序
        /// </summary>
        public void CloseApplication()
        {
            MessagePrompt messagePrompt = new MessagePrompt("提示", "请确认是否关闭软件！！");
            if (messagePrompt.ShowDialog() == true)
                Application.Current.MainWindow.Close();
        }

        /// <summary>
        /// 最大化/还原窗口
        /// </summary>
        public void ToggleMaximize()
        {
            var window = Application.Current.MainWindow;
            if (window != null)
            {
                window.WindowState = WindowState.Normal ;
                // 初始化窗口位置和尺寸（仅在第一次调用时）
                if (!_isInitialized)
                {
                    _normalWidth = window.Width;
                    _normalHeight = window.Height;
                    _normalLeft = window.Left;
                    _normalTop = window.Top;
                    _isInitialized = true;
                }
                
                if (_isMaximized)
                {
                    // 恢复到正常状态
                    window.Left = _normalLeft;
                    window.Top = _normalTop;
                    window.Width = _normalWidth;
                    window.Height = _normalHeight;
                    _isMaximized = false;
                    Model.IsMaximized = false;
                }
                else
                {
                    // 保存当前窗口位置和尺寸
                    _normalLeft = window.Left;
                    _normalTop = window.Top;
                    _normalWidth = window.Width;
                    _normalHeight = window.Height;
                    
                    // 设置为"最大化"状态，减去边框补偿
                    window.Left = SystemParameters.WorkArea.Left ;
                    window.Top = SystemParameters.WorkArea.Top ;
                    window.Width = SystemParameters.WorkArea.Width  ;
                    window.Height = SystemParameters.WorkArea.Height  ;
                    _isMaximized = true;
                    Model.IsMaximized = true;
                }
            }
        }

        /// <summary>
        /// 最小化窗口
        /// </summary>
        public void Minimize()
        {
            var window = Application.Current.MainWindow;
            if (window != null)
            {
                window.WindowState = WindowState.Minimized;
                Model.IsMinimized = true;
            }
        }

        /// <summary>
        /// 用户登录
        /// </summary>
        public void Login()
        {
            _DialogService.ShowDialog("UserInfoView");
        }

       
    }
}