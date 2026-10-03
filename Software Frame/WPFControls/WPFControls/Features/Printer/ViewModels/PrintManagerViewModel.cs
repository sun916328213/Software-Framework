// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：PrintManagerViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.ProgramControl.ViewModels、WPFControls、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、System、WPFControls.Controls.Models。导入不代表所有类型都会在运行时加载。
using WPFControls.ProgramControl.ViewModels;
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using WPFControls.Controls.Models;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;

namespace WPFControls.ProgramControl.ViewModels.Printer
{
    /// <summary>
    /// PrintManagerViewModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    class PrintManagerViewModel 
    {
        private PrintManagerService Service;

        public PrintManagerModel Datas => Service.Model;

        public RelayCommand<PrintInfo> AddCommand { get; }
        public RelayCommand SettingCommand { get; }

        public RelayCommand PrintCommand { get; }
        public RelayCommand ClearCommand { get; }

        public RelayCommand UpCommand { get; }

        public RelayCommand DownCommand { get; }

        /// <summary>
        /// 初始化 PrintManagerViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Add、RemoveAt、Up、Down、RemoveAll；具体条件和先后次序见方法体。
        /// </summary>
        public PrintManagerViewModel(PrintManagerService model)
        {
            Service = model;

            AddCommand = new RelayCommand<PrintInfo>((s) =>
           {
               Service.Add(s);
           });

            ClearCommand = new RelayCommand(() =>
            {
                Service.RemoveAt();
            });

            UpCommand = new RelayCommand(() =>
            {
                Service.Up();
            });

            DownCommand = new RelayCommand(() =>
            {
                Service.Down();
            });

            SettingCommand = new RelayCommand(() =>
            {
                Service.RemoveAll();
            });
            PrintCommand = new RelayCommand(() =>
             {
                 Service.TestPrint();
             });
        }
    }
}
