using FlowEditor.Nodes;
using DPLibrary_Halcon_F48.Tools.FindLine;
using FlowEditor.Core;
using FlowNodes.Vision.Definition;
using System.Threading.Tasks;

namespace FlowNodes.Vision
{
    [FlowTool("组合查找", "VectorPolyline", ToolGroup = "视觉", ToolSubGroup = "Find", SortIndex = 40, Description = "多模板组合查找工具")]
    internal class FindGroupNode : HVisionToolNodeBase<HTool_FindGroup>
    {
        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            RunTool();
            await Task.CompletedTask;
        }
    }
}
