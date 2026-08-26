using FlowEditor.Nodes;
using System.Threading.Tasks;
using DPLibrary_Halcon_F48.Tools;
using FlowEditor.Core;
using FlowNodes.Vision.Definition;

namespace FlowNodes.Vision
{
    [FlowTool("模板匹配", "MagnifyPlus", ToolGroup = "视觉", ToolSubGroup = "TemplateMatch", SortIndex = 40, Description = "从图片中匹配模板")]
    internal class TemplateMatchingNode : HVisionToolNodeBase<HTool_TemplateMatch>
    {
        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            RunTool();
            await Task.CompletedTask;
        }
    }
}
