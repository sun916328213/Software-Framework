using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using System.Collections.ObjectModel;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
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
