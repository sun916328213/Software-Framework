// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件定义：BarCodeNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tools.Halcon.Sessions、Vision.Controls.View、FlowNodes、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime。导入不代表所有类型都会在运行时加载。
using Vision.Tools.Halcon.Sessions;
using Vision.Controls.View;
using FlowNodes;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowNodes.Vision.SingleTool.Definition;

namespace FlowNodes.Vision.SingleTool.CodeReader
{
    /// <summary>
    /// BarCodeNode 的类型定义。FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
    /// 继承或实现的契约：VisionToolNodeBase&lt;BarcodeSession&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    [FlowTool("条码识别", "BarcodeScan", ToolGroup = "视觉", ToolSubGroup = "CodeReader", SortIndex = 40, Description = "一维条码识别工具")]
    internal class BarCodeNode : VisionToolNodeBase<BarcodeSession>
    {
        /// <summary>
        /// 执行当前入口的处理流程；对应入口为 ExecuteAsync。
        /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、RunTool；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
RunTool();
            await Task.CompletedTask;
        }
    }
}
