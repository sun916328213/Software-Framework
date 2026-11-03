#nullable enable
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Tools;

namespace Vision.Controls.ViewModels;

/// <summary>公共数值工具的薄适配，复用原Card，不加载厂商SDK。</summary>
public sealed class ToleranceToolCardAdapter(ToleranceTool tool) : IExecutableTool
{
    public string Name => tool.Name;
    public long LastElapsedMs => tool.LastElapsedMs;
    public IReadOnlyList<NumericParameterDefinition> NumericInputs => tool.NumericInputs;
    public ValidationResult Validate() => tool.Validate();
    public ToolResult Run(VisionImage? image) => tool.Run(image);
    public void EnsureLoaded() => tool.Validate();
    public Func<ToolResult>? PrepareBackgroundRun(VisionImage? image, NumericParameterValue? input) =>
        input == null ? null : tool.PrepareRun(image, input);
    public void Dispose() => tool.Dispose();
}
