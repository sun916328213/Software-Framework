// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：WindowMenuViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、Prism.Ioc、WPFControls.ProgramControl.Models、WPFControls.ProgramControl.Services。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Ioc;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;


namespace WPFControls.ProgramControl.ViewModels
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

        /// <summary>
        /// 初始化 WindowMenuViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Resolve、nameof、OnPropertyChanged、LogoutCommand；具体条件和先后次序见方法体。
        /// </summary>
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