using FlowEditor.Nodes;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DPLibrary_Halcon_F48.Tools.Blob;
using FlowEditor.Core;
using FlowNodes.Vision.Definition;

namespace FlowNodes.Vision
{
    [FlowTool("Blob分析", "Image", ToolGroup = "视觉", ToolSubGroup = "Blob", SortIndex = 40, Description = "通过灰度阈值与形状特征对图像进行 Blob 分析")]
    internal class BlobNode : HVisionToolNodeBase<HTool_Blob>
    {
        private string _resultSummary = "";
        /// <summary>本次执行的 Blob 结果摘要（可绑定到输出端口）。</summary>
        [JsonIgnore]
        public string ResultSummary
        {
            get => _resultSummary;
            set => SetProperty(ref _resultSummary, value);
        }

        /// <summary>最近一次执行的 Blob 结果列表（可绑定到输出端口）。</summary>
        [JsonIgnore]
        public List<HBlobResult> BlobResults => Results.OfType<HBlobResult>().ToList();

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            RunTool();

            var blobs = BlobResults;
            ResultSummary = blobs.Count > 0
                ? $"检出 {blobs.Count} 个 Blob；最大面积 {blobs.Max(r => double.TryParse(r.Area, out var area) ? area : 0):F3}"
                : "未检出 Blob";

            await Task.CompletedTask;
        }
    }
}
