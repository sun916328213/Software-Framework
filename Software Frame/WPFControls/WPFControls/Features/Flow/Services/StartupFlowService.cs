// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：StartupFlowService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、Prism.Ioc、ScriptEditor.Controls.Services、ScriptEditor.Runtime.Services、System.IO、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using WPFControls;
using Prism.Ioc;
using ScriptEditor.Controls.Services;
using ScriptEditor.Runtime.Services;
using System.IO;
using System.Threading.Tasks;
using WPFControls.ProgramControl.Models;

namespace WPFControls.ProgramControl.Services
{
    /// <summary>
    /// 启动流程服务：封装启动流程脚本（StartupFlow.taskproj）的加载与保存逻辑。
    /// 持有独立的 ScriptManagerService，脚本列表展示、编辑与执行共用同一份状态。
    /// </summary>
    public class StartupFlowService
    {
        /// <summary>
        /// 启动流程数据模型。
        /// </summary>
        public StartupFlowModel Model { get; set; } = new StartupFlowModel();

        private readonly ScriptManagerService _scriptManager;

        /// <summary>
        /// 启动流程默认文件完整路径。
        /// </summary>
        public string DefaultFilePath => Path.Combine(Model.FilePath, Model.FileName);

        /// <summary>
        /// 初始化 StartupFlowService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
        /// </summary>
        public StartupFlowService(IContainerProvider container)
        {
            _scriptManager = container.Resolve<ScriptManagerService>();
        }

        /// <summary>
        /// 脚本管理器服务（供 ViewModel / 调用方复用同一份脚本状态）。
        /// </summary>
        public ScriptManagerService ScriptManager => _scriptManager;

        /// <summary>
        /// 从默认路径加载启动流程脚本。
        /// 语义：只加载与替换（内存 ← taskproj/文件夹），**不产生任何写盘动作**。
        /// - 文件夹源码（NN_标题.cs）比 taskproj 新 → 以源码替换内存；
        ///   taskproj 保持旧快照，直到下次在编辑器中保存时自然收敛；
        /// - 否则 taskproj 即事实源，直接使用（镜像同步由编辑器保存路径负责，
        ///   加载期导出会刷新源码时间戳，导致下次启动误判"文件夹较新"）。
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
        /// 保存当前启动流程脚本到默认路径。
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