using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using System.Collections.ObjectModel;
using WPFControl.CustomControl.Models.Charts;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    /// <summary>
    /// 报警信息查询视图模型
    /// 连接视图和服务层，处理用户交互逻辑
    /// </summary>
    internal class AlarmInfoQueryViewModel : ObservableObject
    {
        /// <summary>
        /// 日志查询服务
        /// </summary>
        private readonly AlarmInfoQueryService Service;

       

        public AlarmInfoQueryModel Datas => Service.Model;

        private ObservableCollection<PieData> _pieData = new ObservableCollection<PieData>();
        /// <summary>
        /// 饼状图数据
        /// </summary>
        public ObservableCollection<PieData> PieData 
        {
            get => _pieData;
            set => SetProperty(ref _pieData, value);
        }


        /// <summary>
        /// 查询命令
        /// </summary>
        public IAsyncRelayCommand QueryCommand { get; }

       

        /// <summary>
        /// 保存命令
        /// </summary>
        public IAsyncRelayCommand SaveCommand { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="service">日志查询服务</param>
        public AlarmInfoQueryViewModel(AlarmInfoQueryService service)
        {
            Service = service;

            // 初始化查询命令
            QueryCommand = new AsyncRelayCommand(async () =>
            {
                await Service.QueryAsync();
                PieData = Service.PieData;
            });
 
            // 初始化保存命令
            SaveCommand = new AsyncRelayCommand(async () =>
            {
                
            });
        }
    }
}
