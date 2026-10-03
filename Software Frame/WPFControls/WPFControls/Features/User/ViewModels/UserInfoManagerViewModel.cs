// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：UserInfoManagerViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.ProgramControl.ViewModels、WPFControls、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、Prism.Ioc、Prism.Services.Dialogs。导入不代表所有类型都会在运行时加载。
using WPFControls.ProgramControl.ViewModels;
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Ioc;
using Prism.Services.Dialogs;
using System;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace WPFControls.ProgramControl.ViewModels.User
{
    /// <summary>
    /// UserInfoManagerViewModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    internal class UserInfoManagerViewModel
    {
        internal UserInfoService Service;

        public UserInfoModel Datas => Service.Model;

        public string FilePath => Service._UserService.FilePath;
        public string FileName => Service._UserService.FileName;

      

        /// <summary>
        /// 初始化 UserInfoManagerViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
        /// </summary>
        public UserInfoManagerViewModel(IContainerProvider container)
        {
            Service = container.Resolve<UserInfoService>();

            
        }
    }


}