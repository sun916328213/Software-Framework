using FlowEditor.Services;
using Prism.Ioc;
using System.IO;
using System.Threading.Tasks;

namespace WPFControl.ProgramControl.Services
{
    /// <summary>
    /// 流程图文件服务基类：封装 .flow 流程图文件默认路径与加载逻辑。
    /// AutoFlowService、InitializeFlowService 等持有独立 FlowChartService 的流程服务共用。
    /// </summary>
    public abstract class FlowChartFileService
    {
        private readonly FlowChartService _flowChartService;
        private bool _loaded;

        /// <summary>
        /// 流程图服务（供 ViewModel / 调用方复用同一份流程状态）。
        /// </summary>
        public FlowChartService FlowChart => _flowChartService;

        /// <summary>
        /// 流程默认文件完整路径。
        /// </summary>
        public abstract string DefaultFilePath { get; }

        protected FlowChartFileService(IContainerProvider container)
        {
            _flowChartService = container.Resolve<FlowChartService>();
        }

        /// <summary>
        /// 从默认路径加载流程（同步，须在 UI 线程调用以保证集合绑定安全）。
        /// 幂等：已成功加载过则直接返回 true，避免启动预热与 ViewModel 构造重复反序列化流程文件。
        /// </summary>
        public bool LoadFromDefaultPath()
        {
            if (_loaded) return true;
            if (!File.Exists(DefaultFilePath))
            {
                _flowChartService.Model.StatusText = $"默认流程不存在：{DefaultFilePath}";
                return false;
            }
            if (_flowChartService.Load(DefaultFilePath))
                _loaded = true;
            return _loaded;
        }

        /// <summary>
        /// 从默认路径加载流程（异步封装，仍在调用线程上执行，保证绑定安全）。
        /// </summary>
        public Task<bool> LoadFromDefaultPathAsync() => Task.FromResult(LoadFromDefaultPath());
    }
}