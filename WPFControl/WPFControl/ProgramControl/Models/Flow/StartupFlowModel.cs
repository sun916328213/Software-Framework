using CommunityToolkit.Mvvm.ComponentModel;

namespace WPFControl.ProgramControl.Models
{
    /// <summary>
    /// 启动流程数据模型：承载启动流程的状态数据（文件路径、文件名等）。
    /// </summary>
    public class StartupFlowModel : ObservableObject
    {
        /// <summary>
        /// 启动流程文件目录（任务文件夹制：taskproj 与合并脚本 .cs 同居此文件夹）。
        /// </summary>
        public string FilePath { get; set; } = "Config\\Flow\\StartupFlow";

        /// <summary>
        /// 启动流程文件名
        /// </summary>
        public string FileName { get; set; } = "StartupFlow.taskproj";
    }
}