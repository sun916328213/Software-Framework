using System.Text.Json.Serialization;
using FlowEditor.Core;
using Vision.Core;
using Vision.Core.Engine;
using Vision.Core.Operations;
using Vision.Core.Results;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace Vision.FlowNode;

/// <summary>明确消费二值快照的255前景，八连通、保孔、面积降序；不截断超预算组件。</summary>
[NodeTypeId("vision.engine.blob"), FlowTool("视觉二值Blob", "VectorPolygon", ToolGroup = "视觉")]
public sealed class BlobNode : VisionOperationNode<BlobOutput>
{
    private BinaryImageOutput? _source;
    private int _maximumResults = 1000;

    public BlobNode(IVisionEngine engine) : base(engine)
    {
        AddCustomInputPort(nameof(Source), nameof(Source));
    }

    [JsonIgnore, NodeInput(Required = true)]
    public BinaryImageOutput? Source { get => _source; set => SetInput(ref _source, value); }
    [NodeProperty(DisplayName = "结果安全上限（1..1000）")]
    public int MaximumResults
    {
        get => _maximumResults;
        set
        {
            if (value is < 1 or > 1000)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            SetInput(ref _maximumResults, value);
        }
    }

    protected override void ReadFlowInputs() => Source = FindInputPort(nameof(Source))?.Value as BinaryImageOutput;
    protected override VisionOperation<BlobOutput> CreateOperation() =>
        new BlobOperation((Source ?? throw new InvalidOperationException("缺少本次二值快照。")).Image,
            255, 255, maximumResults: MaximumResults);

    public override Dictionary<string, object?> CaptureDocumentState() => new()
    {
        ["Version"] = 1,
        [nameof(MaximumResults)] = MaximumResults
    };

    public override void RestoreDocumentState(Dictionary<string, object?> state)
    {
        VisionNodeDocument.Check(state, nameof(MaximumResults));
        MaximumResults = VisionNodeDocument.Read<int>(state, nameof(MaximumResults));
    }
}
