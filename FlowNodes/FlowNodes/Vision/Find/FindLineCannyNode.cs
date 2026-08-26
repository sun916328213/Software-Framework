using FlowEditor.Nodes;
using DPLibrary_Halcon_F48.Tools.FindLine;
using FlowEditor.Core;
using FlowNodes.Vision.Definition;
using System.Threading.Tasks;

namespace FlowNodes.Vision
{
    [FlowTool("找线(Canny)", "VectorLine", ToolGroup = "视觉", ToolSubGroup = "Find", SortIndex = 40, Description = "Canny 亚像素边缘找线")]
    internal class FindLineCannyNode : HVisionToolNodeBase<HTool_FindLineCanny>
    {
        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
RunTool();
            await Task.CompletedTask;
        }
    }
}
