using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Ioc;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;


namespace WPFControl.ProgramControl.ViewModels
{
    /// <summary>
    /// 窗口菜单视图模型
    /// </summary>
    public class WindowMenuViewModel : ObservableObject
    {
        private readonly WindowMenuService Service;

        public WindowMenuModel Datas => Service.Model;

        public StatisicsModel StatisicsModel=> Service._StatisticService.Model;

        public UserInfoModel UserInfoModel=>Service._UserInfoService.Model;

        public WindowMenuViewModel(IContainerProvider  container)
        {
            Service = container.Resolve<WindowMenuService>();

            // 监听Model属性变化
            Service.Model.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(WindowMenuModel.IsMaximized))
                {
                    OnPropertyChanged(nameof(MaximizeIconKind));
                }
            };

            // 初始化命令
            CloseCommand = new RelayCommand(CloseExecute);
            MaximizeCommand = new RelayCommand(MaximizeExecute);
            MinimizeCommand = new RelayCommand(MinimizeExecute);
            LoginCommand = new RelayCommand(LoginExecute);
            LogoutCommand = new RelayCommand(()=> Service._UserInfoService.LogoutCommand());
        }

 
        /// <summary>
        /// 关闭命令
        /// </summary>
        public RelayCommand CloseCommand { get; private set; }

        /// <summary>
        /// 最大化命令
        /// </summary>
        public RelayCommand MaximizeCommand { get; private set; }

        /// <summary>
        /// 最小化命令
        /// </summary>
        public RelayCommand MinimizeCommand { get; private set; }

        /// <summary>
        /// 登录命令
        /// </summary>
        public RelayCommand  LoginCommand { get; private set; }

        /// <summary>
        /// 登录命令
        /// </summary>
        public RelayCommand  LogoutCommand { get; private set; }

        /// <summary>
        /// 最大化图标类型
        /// </summary>
        public string MaximizeIconKind => Service.Model.IsMaximized ? "WindowRestore" : "WindowMaximize";

        /// <summary>
        /// 关闭执行
        /// </summary>
        private void CloseExecute()
        {
                Service.CloseApplication();
        }

        /// <summary>
        /// 最大化执行
        /// </summary>
        private void MaximizeExecute()
        {
            Service.ToggleMaximize();
        }

        /// <summary>
        /// 最小化执行
        /// </summary>
        private void MinimizeExecute()
        {
            Service.Minimize();
        }

        /// <summary>
        /// 登录执行
        /// </summary>
        private void LoginExecute()
        {
            Service.Login();
 
        }
    }
}