using FlowEditor.Nodes;
using DPLibrary_Halcon_F48.Tools.Geometry;
using FlowEditor.Core;
using System.Threading.Tasks;
using FlowNodes.Vision.SingleTool.Definition;

namespace FlowNodes.Vision.SingleTool.Geometry
{
    [FlowTool("几何计算", "Calculator", ToolGroup = "视觉", ToolSubGroup = "Geometry", SortIndex = 40, Description = "几何测量计算工具")]
    internal class GeometryNode : HVisionToolNodeBase<HTool_Geometry>
    {
        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
RunTool();
            await Task.CompletedTask;
        }
    }
}
