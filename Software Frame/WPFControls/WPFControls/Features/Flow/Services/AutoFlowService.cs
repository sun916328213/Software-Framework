// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：AutoFlowService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、FlowEditor.Controls.ViewModels、Prism.Ioc、System.IO、System.Threading.Tasks、WPFControls.ProgramControl.Models。导入不代表所有类型都会在运行时加载。
using WPFControls;
using FlowEditor.Controls.ViewModels;
using Prism.Ioc;
using System.IO;
using System.Threading.Tasks;
using WPFControls.ProgramControl.Models;

namespace WPFControls.ProgramControl.Services
{
    /// <summary>
    /// 自动流程服务：封装自动流程组（AutoFlow.flows）的加载逻辑。
    /// 持有独立缓存的 FlowChartContainerViewModel（流程组容器），由 App 启动预热阶段加载流程组并填入 Tabs。
    /// ViewModel 构造时直接复用同一容器 VM，不再覆盖。
    /// </summary>
    public class AutoFlowService
    {
        private readonly IContainerProvider _containerProvider;
        private FlowChartContainerViewModel? _containerViewModel;

        /// <summary>
        /// 自动流程数据模型。
        /// </summary>
        public AutoFlowModel Model { get; set; } = new AutoFlowModel();

        /// <summary>
        /// 自动流程组默认文件完整路径（多流程容器 .flows）。
        /// </summary>
        public string DefaultFilePath => Path.Combine(Model.FilePath, Model.FileName);

        /// <summary>
        /// 与本服务关联的流程组容器 ViewModel（首次访问时创建并缓存）。
        /// 容器级流程名：节点日志按「流程名\任务名」路由，自动流程任务落在 D:\DeviceData\Log\Flow\AutoFlow\{任务名}\
        /// </summary>
        public FlowChartContainerViewModel ContainerViewModel
            => _containerViewModel ??= CreateContainerViewModel();

        /// <summary>
        /// 初始化 AutoFlowService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public AutoFlowService(IContainerProvider container)
        {
            _containerProvider = container;
        }

        /// <summary>
        /// 从默认路径加载流程组（.flows），并填入 <see cref="ContainerViewModel"/> 的 Tabs。
        /// 文件不存在或内容为空直接返回；反序列化失败抛出由调用方捕获。
        /// 必须在 UI 线程调用以保证 ObservableCollection 绑定安全。
        /// </summary>
        public Task LoadFlowGroupFromAsync()
        {
            return ContainerViewModel.LoadFlowGroup(DefaultFilePath);
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateContainerViewModel。
        /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
        /// </summary>
        private FlowChartContainerViewModel CreateContainerViewModel()
        {
            var vm = _containerProvider.Resolve<FlowChartContainerViewModel>();
            vm.ProcessName = "AutoFlow";
            return vm;
        }
    }
}