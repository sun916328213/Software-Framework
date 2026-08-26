using FlowEditor.Nodes;
using DPLibrary_Halcon_F48.Tools;
using FlowEditor.Core;
using FlowNodes.Vision.Definition;
using System.Threading.Tasks;

namespace FlowNodes.Vision
{
    [FlowTool("坐标变换", "VectorSquare", ToolGroup = "视觉", ToolSubGroup = "Calib", SortIndex = 40, Description = "标定点坐标转换工具")]
    internal class TransforPointNode : HVisionToolNodeBase<HTool_TransforPoint>
    {
        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
RunTool();
            await Task.CompletedTask;
        }
    }
}
