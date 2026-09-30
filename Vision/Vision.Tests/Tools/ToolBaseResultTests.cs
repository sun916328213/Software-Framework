using System.Threading;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Tools.Halcon.Internal;
using Vision.Tools.OpenCv.Internal;
using Xunit;

namespace Vision.Tests.Tools;

[Trait("Category", "Contract")]
public sealed class ToolBaseResultTests
{
    [Fact]
    public void HalconBase_PreservesFailureReturnedByRunCore()
    {
        using var tool = new FailingHalconTool();

        var result = tool.Run(null);

        Assert.False(result.Success);
        Assert.Equal("算法未找到目标", result.Message);
        Assert.NotNull(result.Validation);
        Assert.True(result.Validation!.IsValid);
    }

    [Fact]
    public void OpenCvBase_PreservesFailureReturnedByRunCore()
    {
        using var tool = new FailingOpenCvTool();
        var image = VisionImage.FromBuffer(1, 1, PixelFormat.Mono8, [0]);

        var result = tool.Run(image);

        Assert.False(result.Success);
        Assert.Equal("算法未找到目标", result.Message);
        Assert.NotNull(result.Validation);
        Assert.True(result.Validation!.IsValid);
    }

    [Fact]
    public void HalconBase_ValidationFailureResetsLastElapsedMs()
    {
        using var tool = new FailingHalconTool();
        tool.Run(null);
        Assert.True(tool.LastElapsedMs > 0);

        tool.Parameters.Invalid = true;
        var result = tool.Run(null);

        Assert.False(result.Success);
        Assert.Equal(0, result.ElapsedMs);
        Assert.Equal(0, tool.LastElapsedMs);
    }

    [Fact]
    public void OpenCvBase_ValidationFailureResetsLastElapsedMs()
    {
        using var tool = new FailingOpenCvTool();
        var image = VisionImage.FromBuffer(1, 1, PixelFormat.Mono8, [0]);
        tool.Run(image);
        Assert.True(tool.LastElapsedMs > 0);

        tool.Parameters.Invalid = true;
        var result = tool.Run(image);

        Assert.False(result.Success);
        Assert.Equal(0, result.ElapsedMs);
        Assert.Equal(0, tool.LastElapsedMs);
    }

    private sealed class FailingResult : ToolResult;

    private sealed class StubParameters
    {
        public bool Invalid { get; set; }
    }

    private sealed class FailingHalconTool : HalconToolBase<StubParameters, FailingResult>
    {
        public override string Name => "FailureStub";

        protected override ValidationResult ValidateCore(StubParameters parameters) =>
            parameters.Invalid ? new ValidationResult().AddError("INVALID", "参数无效") : new ValidationResult();

        protected override FailingResult RunCore(HalconDotNet.HObject? image, StubParameters parameters)
        {
            Thread.Sleep(20);
            return new() { Success = false, Message = "算法未找到目标" };
        }
    }

    private sealed class FailingOpenCvTool : OpenCvToolBase<StubParameters, FailingResult>
    {
        public override string Name => "FailureStub";

        protected override ValidationResult ValidateCore(StubParameters parameters) =>
            parameters.Invalid ? new ValidationResult().AddError("INVALID", "参数无效") : new ValidationResult();

        protected override FailingResult RunCore(VisionImage image, StubParameters parameters)
        {
            Thread.Sleep(20);
            return new() { Success = false, Message = "算法未找到目标" };
        }
    }
}
