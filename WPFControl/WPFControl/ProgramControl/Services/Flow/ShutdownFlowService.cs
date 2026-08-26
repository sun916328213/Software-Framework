using Prism.Ioc;
using ScriptEditor.Services;
using System.IO;
using System.Threading.Tasks;
using WPFControl.ProgramControl.Models;

namespace WPFControl.ProgramControl.Services
{
    /// <summary>
    /// 关闭流程服务：封装关闭流程脚本（ShutdownFlow.taskproj）的加载与保存逻辑。
    /// 持有独立的 ScriptManagerService，脚本列表展示、编辑与执行共用同一份状态。
    /// </summary>
    public class ShutdownFlowService
    {
        /// <summary>
        /// 关闭流程数据模型。
        /// </summary>
        public ShutdownFlowModel Model { get; set; } = new ShutdownFlowModel();

        private readonly ScriptManagerService _scriptManager;

        /// <summary>
        /// 关闭流程默认文件完整路径。
        /// </summary>
        public string DefaultFilePath => Path.Combine(Model.FilePath, Model.FileName);

        public ShutdownFlowService(IContainerProvider container)
        {
            _scriptManager = container.Resolve<ScriptManagerService>();
        }

        /// <summary>
        /// 脚本管理器服务（供 ViewModel / 调用方复用同一份脚本状态）。
        /// </summary>
        public ScriptManagerService ScriptManager => _scriptManager;

        /// <summary>
        /// 从默认路径加载关机流程脚本。
        /// 语义：只加载与替换，不产生任何写盘动作（详见 StartupFlowService 注释）。
        /// </summary>
        public async Task LoadFromDefaultPathAsync()
        {
            if (!File.Exists(DefaultFilePath))
                return;

            await _scriptManager.LoadTaskFromPathAsync(DefaultFilePath);

            if (_scriptManager.FolderHasNewerSources(DefaultFilePath))
                _scriptManager.ImportScriptsFromFolder(DefaultFilePath);
        }

        /// <summary>
        /// 保存当前关闭流程脚本到默认路径。
        /// </summary>
        public async Task SaveToDefaultPathAsync()
        {
            var dir = Path.GetDirectoryName(DefaultFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await _scriptManager.SaveToPathAsync(DefaultFilePath);
        }
    }
}