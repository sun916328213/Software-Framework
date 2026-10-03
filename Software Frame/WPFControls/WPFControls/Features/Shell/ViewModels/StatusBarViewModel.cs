// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：StatusBarViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、WPFControls.ProgramControl.Models、WPFControls.ProgramControl.Services、System。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;
using System;

namespace WPFControls.ProgramControl.ViewModels
{
    /// <summary>
    /// StatusBarViewModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    public class StatusBarViewModel  
    {
        private readonly StatusBarService Service;

         

        public CStatusBar StatusBar=> Service.Model.StatusBar;

 
        /// <summary>
        /// 初始化 StatusBarViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public StatusBarViewModel(StatusBarService model)
        {
            Service = model;

        
        }
    }
}