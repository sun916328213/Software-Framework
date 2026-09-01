using FlowEditor.Nodes;
using DPLibrary_Halcon_F48.Tools;
using FlowEditor.Core;
using System.Threading.Tasks;
using FlowNodes.Vision.SingleTool.Definition;

namespace FlowNodes.Vision.SingleTool.Calib
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
