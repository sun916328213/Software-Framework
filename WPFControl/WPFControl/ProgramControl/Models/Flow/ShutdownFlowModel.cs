using CommunityToolkit.Mvvm.ComponentModel;

namespace WPFControl.ProgramControl.Models
{
    /// <summary>
    /// 关闭流程数据模型：承载关闭流程的状态数据（文件路径、文件名等）。
    /// </summary>
    public class ShutdownFlowModel : ObservableObject
    {
        /// <summary>
        /// 关闭流程文件路径
        /// </summary>
        public string FilePath { get; set; } = "Config";

        /// <summary>
        /// 关闭流程文件名
        /// </summary>
        public string FileName { get; set; } = "ShutdownFlow.taskproj";
    }
}