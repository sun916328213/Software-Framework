// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：ShutdownFlowViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、Prism.Ioc、ScriptEditor.Controls.ViewModels、WPFControls.ProgramControl.Models、WPFControls.ProgramControl.Services。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Ioc;
using ScriptEditor.Controls.ViewModels;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;

namespace WPFControls.ProgramControl.ViewModels
{
    /// <summary>
    /// 关闭流程 ViewModel：通过 ShutdownFlowService 管理关闭流程脚本，
    /// 并暴露 ShutdownFlowModel 供 View 绑定状态，暴露 ScriptManagerVM 供子控件绑定。
    /// </summary>
    public class ShutdownFlowViewModel : ObservableObject
    {
        private readonly ShutdownFlowService _service;
        private readonly ScriptManagerViewModel _scriptManagerVM;

        /// <summary>
        /// 启动流程数据模型（供 View 绑定状态属性）。
        /// </summary>
        public ShutdownFlowModel Model => _service.Model;

        /// <summary>
        /// 脚本管理器 ViewModel（供 ScriptManagerView 绑定 DataContext）。
        /// </summary>
        public ScriptManagerViewModel ScriptManagerVM => _scriptManagerVM;

        /// <summary>
        /// 初始化 ShutdownFlowViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
        /// </summary>
        public ShutdownFlowViewModel(IContainerProvider containerProvider)
        {
            _service = containerProvider.Resolve<ShutdownFlowService>();
            // 复用 ShutdownFlowService 持有的 ScriptManagerService 实例，
            // 确保视图展示的脚本列表与 ShutdownFlowService 加载/执行的是同一份（关闭流程独立状态）。
            _scriptManagerVM = new ScriptManagerViewModel(_service.ScriptManager);
        }
    }
}
