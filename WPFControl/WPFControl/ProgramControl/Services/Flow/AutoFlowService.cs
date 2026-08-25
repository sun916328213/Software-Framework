using Prism.Ioc;
using System.IO;
using WPFControl.ProgramControl.Models;

namespace WPFControl.ProgramControl.Services
{
    /// <summary>
    /// 自动流程服务：封装自动流程（AutoFlow.flow）的加载逻辑。
    /// 持有独立的 FlowChartService，画布展示、编辑与执行共用同一份流程状态。
    /// </summary>
    public class AutoFlowService : FlowChartFileService
    {
        /// <summary>
        /// 自动流程数据模型。
        /// </summary>
        public AutoFlowModel Model { get; set; } = new AutoFlowModel();

        /// <summary>
        /// 自动流程默认文件完整路径。
        /// </summary>
        public override string DefaultFilePath => Path.Combine(Model.FilePath, Model.FileName);

        public AutoFlowService(IContainerProvider container) : base(container)
        {
        }
    }
}