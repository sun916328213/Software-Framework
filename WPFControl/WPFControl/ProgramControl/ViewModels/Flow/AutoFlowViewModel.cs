using CommunityToolkit.Mvvm.ComponentModel;
using FlowEditor.ViewModels;
using Prism.Ioc;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    /// <summary>
    /// 自动流程 ViewModel：通过 AutoFlowService 持有默认文件路径，
    /// 并暴露 AutoFlowModel 供 View 绑定状态，暴露 FlowChartContainerVM 供子控件绑定。
    /// 流程图本身已由 App 启动预热阶段加载到容器 VM（流程组 .flows），此处仅绑定容器 VM；
    /// 不再调用 ReplaceWithFlow 覆盖预热结果。
    /// </summary>
    public class AutoFlowViewModel : ObservableObject
    {
        private readonly AutoFlowService _service;

        /// <summary>
        /// 自动流程数据模型（供 View 绑定状态属性）。
        /// </summary>
        public AutoFlowModel Model => _service.Model;

        /// <summary>
        /// 流程图容器 ViewModel（供 FlowChartContainerView 绑定 DataContext）。
        /// </summary>
        public FlowChartContainerViewModel FlowChartContainerVM => _service.ContainerViewModel;

        public AutoFlowViewModel(IContainerProvider containerProvider)
        {
            _service = containerProvider.Resolve<AutoFlowService>();
            // 容器 VM 由 AutoFlowService 缓存为单例（App 预热阶段已加载 AutoFlow.flows）；
            // 访问 FlowChartContainerVM 触发首次创建（仅一次）。
        }
    }
}