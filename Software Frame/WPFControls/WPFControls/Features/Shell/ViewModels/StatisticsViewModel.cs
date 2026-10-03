// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：StatisticsViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、System.Windows.Input、WPFControls.ProgramControl.Models、WPFControls.ProgramControl.Services。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;

namespace WPFControls.ProgramControl.ViewModels
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

        /// <summary>
        /// 初始化 StatisticsViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public StatisticsViewModel(StatisticService statisticService)
        {
            Service = statisticService;

            ClearCountCommand = new RelayCommand(Service.Clear);
        }
    }
}