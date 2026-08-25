using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;

namespace WPFControl.ProgramControl.Models
{
    /// <summary>
    /// 启动流程数据模型：承载启动流程的状态数据。
    /// </summary>
    public class InitializeFlowModel : ObservableObject
    {
        /// <summary>
        /// 启动流程文件路径
        /// </summary>
        public string FilePath { get; set; } = "Config\\Flow";

        /// <summary>
        /// 启动流程文件名
        /// </summary>
        public string FileName { get; set; } = "InitializeFlow.flow";

      
    }
}
