using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Ioc;
using ScriptEditor.ViewModels;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
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

        public ShutdownFlowViewModel(IContainerProvider containerProvider)
        {
            _service = containerProvider.Resolve<ShutdownFlowService>();
            // 复用 ShutdownFlowService 持有的 ScriptManagerService 实例，
            // 确保视图展示的脚本列表与 ShutdownFlowService 加载/执行的是同一份（关闭流程独立状态）。
            _scriptManagerVM = new ScriptManagerViewModel(_service.ScriptManager);
        }
    }
}
