using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Services.Dialogs;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace WPFControl.ProgramControl.ViewModels
{
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

    public class UserInfoViewModel : ObservableObject, IDialogAware
    {
        private UserInfoService Service;

        public UserInfoModel Datas => Service.Model;

   
        // IDialogAware implementation
        public string Title => "用户登录";
        
        public event System.Action<IDialogResult> RequestClose;

        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogClosed()
        {
            // 对话框关闭时的清理工作
        }

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

 
        
        private void CloseDialog()
        {
            RequestClose?.Invoke(new DialogResult( ButtonResult.Cancel));
        }
        
      
    }
}