using System.Text.Json.Serialization;
using FlowEditor.Core;
using Vision.Core;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace Vision.FlowNode;

/// <summary>读取明确绝对路径；路径是配置，不在构造、恢复文档或getter中读文件。</summary>
[NodeTypeId("vision.engine.image-file"), FlowTool("视觉文件读取", "Image", ToolGroup = "视觉")]
public sealed class ImageFileNode : VisionOperationNode<ImageFileOutput>
{
    private string _filePath = "";
    public ImageFileNode(IVisionEngine engine) : base(engine)
    {
        // 文件读取默认作为链入口；需要控制流前驱时由流程配置显式打开IN。
        IsInEnabled = false;
        AddCustomOutputPort(nameof(OutputImage), nameof(OutputImage));
    }

    [NodeProperty(DisplayName = "图像绝对路径")]
    public string FilePath
    {
        get => _filePath;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length != 0)
            {
                value = new ImageFileOperation(value).FilePath;
            }

            SetInput(ref _filePath, value);
        }
    }

    [JsonIgnore, NodeOutput]
    public VisionFrame? OutputImage => Result?.Image;

    protected override VisionOperation<ImageFileOutput> CreateOperation() => new ImageFileOperation(FilePath);
    public override Dictionary<string, object?> CaptureDocumentState() => new()
    {
        ["Version"] = 1,
        [nameof(FilePath)] = FilePath
    };

    public override void RestoreDocumentState(Dictionary<string, object?> state)
    {
        VisionNodeDocument.Check(state, nameof(FilePath));
        FilePath = VisionNodeDocument.Read<string>(state, nameof(FilePath));
    }
}
