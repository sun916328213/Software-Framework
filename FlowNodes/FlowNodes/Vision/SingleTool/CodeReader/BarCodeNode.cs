using FlowEditor.Nodes;
using DPLibrary_Halcon_F48.Tools;
using FlowEditor.Core;
using System.Threading.Tasks;
using FlowNodes.Vision.SingleTool.Definition;

namespace FlowNodes.Vision.SingleTool.CodeReader
{
    [FlowTool("条码识别", "BarcodeScan", ToolGroup = "视觉", ToolSubGroup = "CodeReader", SortIndex = 40, Description = "一维条码识别工具")]
    internal class BarCodeNode : HVisionToolNodeBase<HTool_BarCode>
    {
        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
RunTool();
            await Task.CompletedTask;
        }
    }
}
