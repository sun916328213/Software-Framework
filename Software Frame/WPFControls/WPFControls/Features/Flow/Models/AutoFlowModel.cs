// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：AutoFlowModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WPFControls.ProgramControl.Models
{
    /// <summary>
    /// 自动流程数据模型：承载自动流程的状态数据（文件路径、文件名等）。
    /// </summary>
    public class AutoFlowModel : ObservableObject
    {
        /// <summary>
        /// 自动流程文件路径
        /// </summary>
        public string FilePath { get; set; } = "Config\\Flow\\AUtoFlow";

        /// <summary>
        /// 自动流程文件名
        /// </summary>
        public string FileName { get; set; } = "AutoFlow.flows";
    }
}