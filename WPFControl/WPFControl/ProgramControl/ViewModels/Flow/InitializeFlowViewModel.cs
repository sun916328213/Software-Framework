using CommunityToolkit.Mvvm.ComponentModel;
using FlowEditor.Interfaces;
using FlowEditor.ViewModels;
using Prism.Ioc;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    /// <summary>
    /// 初始化流程 ViewModel：通过 InitializeFlowService 管理初始化流程，
    /// 并暴露 InitializeFlowModel 供 View 绑定状态，暴露 FlowChartContainerVM 供子控件绑定。
    /// </summary>
    public class InitializeFlowViewModel : ObservableObject
    {
        private readonly InitializeFlowService _service;
        private readonly FlowChartContainerViewModel _containerVM;

        /// <summary>
        /// 初始化流程数据模型（供 View 绑定状态属性）。
        /// </summary>
        public InitializeFlowModel Model => _service.Model;

        /// <summary>
        /// 流程图容器 ViewModel（供 FlowChartContainerView 绑定 DataContext）。
        /// </summary>
        public FlowChartContainerViewModel FlowChartContainerVM => _containerVM;

        public InitializeFlowViewModel(IContainerProvider containerProvider)
        {
            _service = containerProvider.Resolve<InitializeFlowService>();

            // 加载默认初始化流程（幂等：已加载则重新加载，未加载则从默认文件加载）。
            _service.LoadFromDefaultPath();

            // 复用 InitializeFlowService 持有的 FlowChartService 实例，
            // 确保视图展示的流程图与 InitializeFlowService 加载/执行的是同一份（初始化流程独立状态）。
            var flowChartVM = new FlowChartViewModel(
                _service.FlowChart,
                containerProvider.Resolve<IExportService>(),
                containerProvider.Resolve<IDialogService>(),
                containerProvider);

            _containerVM = containerProvider.Resolve<FlowChartContainerViewModel>();
            _containerVM.ReplaceWithFlow(flowChartVM);
        }
    }
}