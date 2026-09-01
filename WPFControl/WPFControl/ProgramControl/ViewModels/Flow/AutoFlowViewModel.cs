using CommunityToolkit.Mvvm.ComponentModel;
using FlowEditor.Interfaces;
using FlowEditor.ViewModels;
using Prism.Ioc;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    /// <summary>
    /// 自动流程 ViewModel：通过 AutoFlowService 管理自动流程，
    /// 并暴露 AutoFlowModel 供 View 绑定状态，暴露 FlowChartContainerVM 供子控件绑定。
    /// </summary>
    public class AutoFlowViewModel : ObservableObject
    {
        private readonly AutoFlowService _service;
        private readonly FlowChartContainerViewModel _containerVM;

        /// <summary>
        /// 自动流程数据模型（供 View 绑定状态属性）。
        /// </summary>
        public AutoFlowModel Model => _service.Model;

        /// <summary>
        /// 流程图容器 ViewModel（供 FlowChartContainerView 绑定 DataContext）。
        /// </summary>
        public FlowChartContainerViewModel FlowChartContainerVM => _containerVM;

        public AutoFlowViewModel(IContainerProvider containerProvider)
        {
            _service = containerProvider.Resolve<AutoFlowService>();

            // 加载默认自动流程（幂等：已加载则重新加载，未加载则从默认文件加载）。
            _service.LoadFromDefaultPath();

            // 复用 AutoFlowService 持有的 FlowChartService 实例，
            // 确保视图展示的流程图与 AutoFlowService 加载/执行的是同一份（自动流程独立状态）。
            var flowChartVM = new FlowChartViewModel(
                _service.FlowChart,
                containerProvider.Resolve<IExportService>(),
                containerProvider.Resolve<IDialogService>(),
                containerProvider);

            _containerVM = containerProvider.Resolve<FlowChartContainerViewModel>();
            // 流程名（容器级）：节点日志按「流程名\任务名」路由，自动流程任务落在 D:\DeviceData\Log\Flow\AutoFlow\{任务名}\
            _containerVM.ProcessName = "AutoFlow";
            _containerVM.ReplaceWithFlow(flowChartVM);
        }
    }
}