using FlowEditor.Nodes;
using DPLibrary_Halcon_F48.Tools;
using FlowEditor.Core;
using System.Threading.Tasks;
using FlowNodes.Vision.SingleTool.Definition;

namespace FlowNodes.Vision.SingleTool.Calib
{
    [FlowTool("N点标定", "MapMarkerRadius", ToolGroup = "视觉", ToolSubGroup = "Calib", SortIndex = 40, Description = "多点标定工具")]
    internal class NPointsNode : HVisionToolNodeBase<HTool_NPoints>
    {
        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
RunTool();
            await Task.CompletedTask;
        }
    }
}
