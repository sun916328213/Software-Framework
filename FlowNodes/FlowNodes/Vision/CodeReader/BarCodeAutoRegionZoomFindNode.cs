using FlowEditor.Nodes;
using DPLibrary_Halcon_F48.Tools;
using FlowEditor.Core;
using FlowNodes.Vision.Definition;
using System.Threading.Tasks;

namespace FlowNodes.Vision
{
    [FlowTool("条码识别(自动变倍)", "BarcodeScan", ToolGroup = "视觉", ToolSubGroup = "CodeReader", SortIndex = 40, Description = "自动调整区域并变倍查找条码")]
    internal class BarCodeAutoRegionZoomFindNode : HVisionToolNodeBase<HTool_BarCode_AutoRegionZoomFind>
    {
        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
RunTool();
            await Task.CompletedTask;
        }
    }
}
