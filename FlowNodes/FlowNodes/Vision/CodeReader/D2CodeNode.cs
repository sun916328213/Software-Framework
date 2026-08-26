using FlowEditor.Nodes;
using DPLibrary_Halcon_F48.Tools;
using FlowEditor.Core;
using FlowNodes.Vision.Definition;
using System.Threading.Tasks;

namespace FlowNodes.Vision
{
    [FlowTool("二维码识别", "QrcodeScan", ToolGroup = "视觉", ToolSubGroup = "CodeReader", SortIndex = 40, Description = "二维码/DataMatrix 识别")]
    internal class D2CodeNode : HVisionToolNodeBase<HTool_2DCode>
    {
        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
RunTool();
            await Task.CompletedTask;
        }
    }
}
