using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dm;
using System;
using System.Linq;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    /// <summary>
    /// 产能统计视图模型
    /// </summary>
    public class CapacityStatisticsViewModel : ObservableObject
    {
        private readonly CapacityStatisticsService _service;

  
        public CapacityStatisticsModel Datas => _service.Model;


        public RelayCommand QueryCommand { get; }
        public AsyncRelayCommand SaveCommand { get; }

        public CapacityStatisticsViewModel(CapacityStatisticsService service)
        {
            _service = service;


            QueryCommand = new RelayCommand(() => _service.GetStatistics());
            SaveCommand = new AsyncRelayCommand(async () => await _service.SaveStatistics(Datas.CapacityStatisticsDatas.ToList()));
        }
    }
}
