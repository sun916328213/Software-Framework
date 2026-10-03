// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：StartupFlowModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WPFControls.ProgramControl.Models
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