// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：LogQueryViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.ProgramControl.ViewModels、WPFControls、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、LiveChartsCore、System.Collections.ObjectModel。导入不代表所有类型都会在运行时加载。
using WPFControls.ProgramControl.ViewModels;
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using System.Collections.ObjectModel;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;

namespace WPFControls.ProgramControl.ViewModels.Log
{
    /// <summary>
    /// 日志查询视图模型
    /// 连接视图和服务层，处理用户交互逻辑
    /// </summary>
    internal class LogQueryViewModel : ObservableObject
    {
        /// <summary>
        /// 日志查询服务
        /// </summary>
        private readonly LogQueryService Service;

      
        public LogQueryModel Datas => Service.Model;

 
        /// <summary>
        /// 查询命令
        /// </summary>
        public IAsyncRelayCommand QueryCommand { get; }

        /// <summary>
        /// 筛选命令
        /// </summary>
        public RelayCommand  FilterCommand { get; }

        /// <summary>
        /// 筛选重置命令
        /// </summary>
        public RelayCommand FilterResetCommand { get; }

        /// <summary>
        /// 等级筛选命令
        /// </summary>
        public RelayCommand SelectFilterCommand { get; }

        /// <summary>
        /// 等级重置命令
        /// </summary>
        public RelayCommand SelectResetCommand { get; }

        /// <summary>
        /// 保存命令
        /// </summary>
        public IAsyncRelayCommand SaveCommand { get; }

        /// <summary>
        /// 翻页命令
        /// </summary>
        public RelayCommand<int> PageChangedCommand { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="service">日志查询服务</param>
        public LogQueryViewModel(LogQueryService service)
        {
            Service = service;

            // 初始化查询命令
            QueryCommand = new AsyncRelayCommand(async () =>
            {
                await Service.Query_Log();
            });

            FilterCommand = new RelayCommand(() =>
            {
                  Service.Filter_Log();
            });

            FilterResetCommand = new RelayCommand(() =>
            {
                Service.FilterReset_Log();
            });

            SelectFilterCommand = new RelayCommand(() =>
            {
                Service.SelectFilter_Log();
            });

            SelectResetCommand = new RelayCommand(() =>
            {
                Service.SelectReset_Log();
            });
 
            // 初始化保存命令
            SaveCommand = new AsyncRelayCommand(async () =>
            {
                await service.Save_Log();
            });

            // 初始化翻页命令
            PageChangedCommand = new RelayCommand<int>(page => Service.Page_Log(page));
        }
    }
}
