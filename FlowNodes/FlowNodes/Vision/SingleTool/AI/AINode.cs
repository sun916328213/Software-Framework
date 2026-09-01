using FlowEditor.Nodes;
using DPLibrary_Halcon_F48.Tools.AI;
using FlowEditor.Core;
using System.Threading.Tasks;
using FlowNodes.Vision.SingleTool.Definition;

namespace FlowNodes.Vision.SingleTool.AI
{
    [FlowTool("AI检测", "RobotOutline", ToolGroup = "视觉", ToolSubGroup = "AI", SortIndex = 40, Description = "调用 AI 模型执行推理检测")]
    internal class AINode : HVisionToolNodeBase<AITool>
    {
        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            RunTool();
            await Task.CompletedTask;
        }
    }
}
