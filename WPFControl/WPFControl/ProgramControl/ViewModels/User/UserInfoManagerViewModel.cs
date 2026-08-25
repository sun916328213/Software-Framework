using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Ioc;
using Prism.Services.Dialogs;
using System;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace WPFControl.ProgramControl.ViewModels
{
    internal class UserInfoManagerViewModel
    {
        internal UserInfoService Service;

        public UserInfoModel Datas => Service.Model;

        public string FilePath => Service._UserService.FilePath;
        public string FileName => Service._UserService.FileName;

      

        public UserInfoManagerViewModel(IContainerProvider container)
        {
            Service = container.Resolve<UserInfoService>();

            
        }
    }


}