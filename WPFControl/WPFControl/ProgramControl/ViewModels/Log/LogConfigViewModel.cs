using CommunityToolkit.Mvvm.ComponentModel;
using System;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    /// <summary>
    /// 日志配置界面（仿 CamManagerViewModel）：经 DeviceManagerView 编辑并保存统一日志配置（Config\LogConfig.json）
    /// </summary>
    public class LogConfigViewModel : ObservableObject
    {
        readonly LogConfigService Service;

        public LogConfigModel Datas => Service.Model;

        public Action SaveCallbackCommand { get; }

        public LogConfigViewModel(LogConfigService service)
        {
            Service = service;

            // DeviceManagerView 保存后重新加载，基础日志与动态流程日志都会换用新配置。
            SaveCallbackCommand = () => Service.Load();
        }
    }
}
