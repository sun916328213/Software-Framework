using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    /// <summary>
    /// 统计信息视图模型
    /// 负责与视图进行数据绑定
    /// </summary>
    public class StatisticsViewModel  
    {
        private readonly StatisticService Service;

        /// <summary>
        /// 统计信息
        /// </summary>
        public StatisicsModel Statistics => Service.Model;

        public ICommand ClearCountCommand { get; private set; }

        public StatisticsViewModel(StatisticService statisticService)
        {
            Service = statisticService;

            ClearCountCommand = new RelayCommand(Service.Clear);
        }
    }
}