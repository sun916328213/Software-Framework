using FlowEditor.Nodes;
using DPLibrary_Halcon_F48.Tools;
using FlowEditor.Core;
using System.Threading.Tasks;
using FlowNodes.Vision.SingleTool.Definition;

namespace FlowNodes.Vision.SingleTool.Fixtrue
{
    [FlowTool("基准拟合", "AxisTriple", ToolGroup = "视觉", ToolSubGroup = "Fixtrue", SortIndex = 40, Description = "基准坐标系拟合工具")]
    internal class FixtrueNode : HVisionToolNodeBase<HTool_Fixtrue>
    {
        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
RunTool();
            await Task.CompletedTask;
        }
    }
}
