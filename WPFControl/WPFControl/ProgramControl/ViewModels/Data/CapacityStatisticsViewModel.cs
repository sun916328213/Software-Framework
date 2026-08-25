using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    /// <summary>
    /// 产能统计视图模型
    /// </summary>
    /// <typeparam name="T">数据库记录类型</typeparam>
    public class CapacityStatisticsViewModel<T> : ObservableObject where T : ISQLDataBase
    {
        private readonly CapacityStatisticsService<T> _service;

        private DateTime _startTime;
        private DateTime _endTime;

        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime StartTime
        {
            get => _startTime;
            set => SetProperty(ref _startTime, value);
        }

        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime EndTime
        {
            get => _endTime;
            set => SetProperty(ref _endTime, value);
        }

        private CapacityStatisticsModel _datas = new CapacityStatisticsModel();

        /// <summary>
        /// 产能统计数据
        /// </summary>
        public CapacityStatisticsModel Datas
        {
            get => _datas;
            set => SetProperty(ref _datas, value);
        }

        /// <summary>
        /// 查询命令
        /// </summary>
        public RelayCommand QueryCommand { get; }

        public CapacityStatisticsViewModel(CapacityStatisticsService<T> service)
        {
            _service = service;

            StartTime = DateTime.Now.Date.AddDays(-1);
            EndTime = DateTime.Now.Date;

            QueryCommand = new RelayCommand(Query);
        }

        private void Query()
        {
            var data = _service.GetStatistics(StartTime, EndTime);
            if (data != null)
                Datas = data;
        }
    }
}