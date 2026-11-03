using System.Text.Json.Serialization;
using FlowEditor.Core;
using Vision.Core;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace Vision.FlowNode;

/// <summary>全图原编码灰度闭区间阈值；不转换颜色、不归一化、不隐式截断Mono8阈值。</summary>
[NodeTypeId("vision.engine.threshold-range"), FlowTool("视觉区间阈值", "ImageFilter", ToolGroup = "视觉")]
public sealed class ThresholdRangeNode : VisionOperationNode<BinaryImageOutput>
{
    private VisionFrame? _image;
    private ushort _minimum = 128;
    private ushort _maximum = 255;

    public ThresholdRangeNode(IVisionEngine engine) : base(engine)
    {
        AddCustomInputPort(nameof(Image), nameof(Image));
        AddCustomOutputPort(nameof(OutputImage), nameof(OutputImage));
    }

    [JsonIgnore, NodeInput(Required = true)]
    public VisionFrame? Image { get => _image; set => SetInput(ref _image, value); }
    [NodeProperty(DisplayName = "灰度下限（含）")]
    public ushort Minimum { get => _minimum; set => SetInput(ref _minimum, value); }
    [NodeProperty(DisplayName = "灰度上限（含）")]
    public ushort Maximum { get => _maximum; set => SetInput(ref _maximum, value); }
    [JsonIgnore, NodeOutput]
    public VisionFrame? OutputImage => Result?.Image;

    protected override void ReadFlowInputs() => Image = FindInputPort(nameof(Image))?.Value as VisionFrame;
    protected override VisionOperation<BinaryImageOutput> CreateOperation() =>
        new ThresholdRangeOperation(Image ?? throw new InvalidOperationException("缺少本次图像输入。"), Minimum, Maximum);

    public override Dictionary<string, object?> CaptureDocumentState() => new()
    {
        ["Version"] = 1,
        [nameof(Minimum)] = Minimum,
        [nameof(Maximum)] = Maximum
    };

    public override void RestoreDocumentState(Dictionary<string, object?> state)
    {
        VisionNodeDocument.Check(state, nameof(Minimum), nameof(Maximum));
        var minimum = VisionNodeDocument.Read<ushort>(state, nameof(Minimum));
        var maximum = VisionNodeDocument.Read<ushort>(state, nameof(Maximum));
        if (minimum > maximum)
        {
            throw new InvalidOperationException("阈值下限高于上限。");
        }

        Minimum = minimum;
        Maximum = maximum;
    }
}
