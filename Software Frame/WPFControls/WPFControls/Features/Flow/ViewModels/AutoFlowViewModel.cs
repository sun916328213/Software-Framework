// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：AutoFlowViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、FlowEditor.Controls.ViewModels、Prism.Ioc、WPFControls.ProgramControl.Models、WPFControls.ProgramControl.Services。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using FlowEditor.Controls.ViewModels;
using Prism.Ioc;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;

namespace WPFControls.ProgramControl.ViewModels
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

        /// <summary>
        /// 初始化 AutoFlowViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
        /// </summary>
        public AutoFlowViewModel(IContainerProvider containerProvider)
        {
            _service = containerProvider.Resolve<AutoFlowService>();
            // 容器 VM 由 AutoFlowService 缓存为单例（App 预热阶段已加载 AutoFlow.flows）；
            // 访问 FlowChartContainerVM 触发首次创建（仅一次）。
        }
    }
}