using Vision.Tools.Halcon.Calibration;
using Xunit;

namespace Vision.Tests.Tools.Halcon;

public sealed class CalibrationEditorRuntimeTests
{
    [Fact(Skip = "需要授权离线 HALCON Runtime/License；验证三点标定数值及重复执行/释放，不连接设备。")]
    [Trait("Category", "RequiresRuntime")]
    public void ThreeNonCollinearPointPairs_ReturnAnalyticScaleAndMatrix_OnRepeatedRuns()
    {
        using var tool = new NPointsCalibrationTool();
        tool.Parameters.Points =
        [
            new() { PixelX = 0, PixelY = 0, WorldX = 5, WorldY = 7 },
            new() { PixelX = 10, PixelY = 0, WorldX = 25, WorldY = 7 },
            new() { PixelX = 0, PixelY = 10, WorldX = 5, WorldY = 37 }
        ];
        for (var i = 0; i < 10; i++)
        {
            using var result = tool.Run(null);
            Assert.True(result.Success, result.Message);
            Assert.Equal(2, result.ScaleX, 4);
            Assert.Equal(3, result.ScaleY, 4);
            Assert.False(string.IsNullOrWhiteSpace(result.Matrix));
        }
    }
}
