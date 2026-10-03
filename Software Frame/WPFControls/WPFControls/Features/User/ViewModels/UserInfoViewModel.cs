// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：MessageType、UserInfoViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、Prism.Services.Dialogs、WPFControls.ProgramControl.Models、WPFControls.ProgramControl.Services、CommunityToolkit.Mvvm.Input.RelayCommand。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Services.Dialogs;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace WPFControls.ProgramControl.ViewModels
{
    /// <summary>
    /// MessageType 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    public enum MessageType
    {
        CloseWindow,
        UserChanged,
        Home,
        ShowAddUser,
        ShowModifyUser,
        ShowDeleteUser,
        AddUser
    }

    /// <summary>
    /// UserInfoViewModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ObservableObject、IDialogAware。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class UserInfoViewModel : ObservableObject, IDialogAware
    {
        private UserInfoService Service;

        public UserInfoModel Datas => Service.Model;

   
        // IDialogAware implementation
        public string Title => "用户登录";
        
        public event System.Action<IDialogResult> RequestClose;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 CanCloseDialog。
        /// </summary>
        public bool CanCloseDialog()
        {
            return true;
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnDialogClosed。
        /// </summary>
        public void OnDialogClosed()
        {
            // 对话框关闭时的清理工作
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnDialogOpened。
        /// </summary>
        public void OnDialogOpened(IDialogParameters parameters)
        {
            // 对话框打开时的初始化工作
        }


        public RelayCommand AddCommand { get; }
        public RelayCommand AddShowCommand { get; }
        public RelayCommand ModifyCommand { get; }
        public RelayCommand ModifyShowCommand { get; }
        public RelayCommand DeletCommand { get; }
        public RelayCommand DeletShowCommand { get; }
        public RelayCommand LoginCommand { get; }
        public RelayCommand LogoutCommand { get; }
        public RelayCommand ReturnCommand { get; }
        public RelayCommand ExitCommand { get; }


        /// <summary>
        /// 初始化 UserInfoViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 LoginCommand、CloseDialog；具体条件和先后次序见方法体。
        /// </summary>
        public UserInfoViewModel(UserInfoService model)
        {
            Service = model;

            LoginCommand = new RelayCommand(() =>{
              if(  Service.LoginCommand())
                CloseDialog();
            });
            LogoutCommand = new RelayCommand(Service.LogoutCommand);

            AddCommand = new RelayCommand(Service.AddCommand);
            ModifyCommand = new RelayCommand(Service.ModifyCommand);
            DeletCommand = new RelayCommand(Service.DeletCommand);

            AddShowCommand = new RelayCommand(Service. SwitchAdd);
            ModifyShowCommand = new RelayCommand(Service.SwitchModify);
            DeletShowCommand = new RelayCommand(Service.SwitchDelet);
            ReturnCommand = new RelayCommand(Service.SwitchMain);

            ExitCommand = new RelayCommand(CloseDialog);
        }

 
        
        /// <summary>
        /// 执行关闭步骤；对应入口为 CloseDialog。
        /// </summary>
        private void CloseDialog()
        {
            RequestClose?.Invoke(new DialogResult( ButtonResult.Cancel));
        }
        
      
    }
}