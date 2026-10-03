// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：CapacityStatisticsViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、Dm、System、System.Linq。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dm;
using System;
using System.Linq;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;

namespace WPFControls.ProgramControl.ViewModels
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

        /// <summary>
        /// 初始化 CapacityStatisticsViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 GetStatistics、SaveStatistics、ToList；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public CapacityStatisticsViewModel(CapacityStatisticsService service)
        {
            _service = service;


            QueryCommand = new RelayCommand(() => _service.GetStatistics());
            SaveCommand = new AsyncRelayCommand(async () => await _service.SaveStatistics(Datas.CapacityStatisticsDatas.ToList()));
        }
    }
}
