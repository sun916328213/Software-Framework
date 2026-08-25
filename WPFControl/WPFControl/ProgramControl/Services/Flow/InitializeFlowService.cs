using Prism.Ioc;
using System.IO;
using WPFControl.ProgramControl.Models;

namespace WPFControl.ProgramControl.Services
{
    /// <summary>
    /// 初始化流程服务：封装初始化流程（InitializeFlow.flow）的加载逻辑。
    /// 持有独立的 FlowChartService，画布展示、编辑与执行共用同一份流程状态。
    /// </summary>
    public class InitializeFlowService : FlowChartFileService
    {
        /// <summary>
        /// 初始化流程数据模型。
        /// </summary>
        public InitializeFlowModel Model { get; set; } = new InitializeFlowModel();

        /// <summary>
        /// 初始化流程默认文件完整路径。
        /// </summary>
        public override string DefaultFilePath => Path.Combine(Model.FilePath, Model.FileName);

        public InitializeFlowService(IContainerProvider container) : base(container)
        {
        }
    }
}