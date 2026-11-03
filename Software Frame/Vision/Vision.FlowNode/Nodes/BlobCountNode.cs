using System.Text.Json.Serialization;
using FlowEditor.Core;
using Vision.Core;
using Vision.Core.Engine;
using Vision.Core.Operations;
using Vision.Core.Results;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace Vision.FlowNode;

/// <summary>无图数量闭区间判定；空Blob的零数量可判OK，NG不是执行失败。</summary>
[NodeTypeId("vision.engine.blob-count"), FlowTool("视觉Blob数量判定", "CheckCircleOutline", ToolGroup = "视觉")]
public sealed class BlobCountNode : VisionOperationNode<BlobJudgmentOutput>
{
    private BlobOutput? _source;
    private int _minimum = 1;
    private int _maximum = 1;

    public BlobCountNode(IVisionEngine engine) : base(engine)
    {
        AddCustomInputPort(nameof(Source), nameof(Source));
        AddCustomOutputPort(nameof(Passed), nameof(Passed));
    }

    [JsonIgnore, NodeInput(Required = true)]
    public BlobOutput? Source { get => _source; set => SetInput(ref _source, value); }
    [NodeProperty(DisplayName = "数量下限（含）")]
    public int Minimum { get => _minimum; set => SetInput(ref _minimum, value); }
    [NodeProperty(DisplayName = "数量上限（含）")]
    public int Maximum { get => _maximum; set => SetInput(ref _maximum, value); }
    [JsonIgnore, NodeOutput]
    public bool? Passed => Result?.Passed;

    protected override void ReadFlowInputs() => Source = FindInputPort(nameof(Source))?.Value as BlobOutput;
    protected override VisionOperation<BlobJudgmentOutput> CreateOperation() =>
        BlobJudgmentOperation.ForCount(Source ?? throw new InvalidOperationException("缺少本次Blob快照。"), Minimum, Maximum);

    public override Dictionary<string, object?> CaptureDocumentState() => new()
    {
        ["Version"] = 1,
        [nameof(Minimum)] = Minimum,
        [nameof(Maximum)] = Maximum
    };

    public override void RestoreDocumentState(Dictionary<string, object?> state)
    {
        VisionNodeDocument.Check(state, nameof(Minimum), nameof(Maximum));
        var minimum = VisionNodeDocument.Read<int>(state, nameof(Minimum));
        var maximum = VisionNodeDocument.Read<int>(state, nameof(Maximum));
        if (minimum < 0 || maximum < minimum)
        {
            throw new InvalidOperationException("数量闭区间无效。");
        }

        Minimum = minimum;
        Maximum = maximum;
    }
}
