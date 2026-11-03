using System.Text.Json;
using Vision.Tools.Halcon.Measurement;
using Xunit;

namespace Vision.Tests.Tools.Halcon;

[Trait("Category", "Contract")]
public sealed class FindLineCaliperContractTests
{
    [Fact]
    public void LegacyJson_KeepsPreviousDefaults_AndNewSettingsRoundTrip()
    {
        var parameters = JsonSerializer.Deserialize<FindLineParameters>("{\"CaliperCount\":9,\"Threshold\":30}")!;
        Assert.Equal(1, parameters.Sigma); Assert.Equal(CaliperEdgeSelection.All, parameters.EdgeSelection); Assert.Equal(0, parameters.IgnoreEndPoints);
        parameters.Sigma = 2.5; parameters.EdgeSelection = CaliperEdgeSelection.Last; parameters.IgnoreEndPoints = 2;
        var restored = JsonSerializer.Deserialize<FindLineParameters>(JsonSerializer.Serialize(parameters))!;
        Assert.Equal(2.5, restored.Sigma); Assert.Equal(CaliperEdgeSelection.Last, restored.EdgeSelection); Assert.Equal(2, restored.IgnoreEndPoints);
        Assert.Equal(9, restored.CaliperCount); Assert.Equal(30, restored.Threshold);
    }
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0.39)]
    [InlineData(100.01)]
    public void InvalidSigma_IsRejectedWithoutCallingTheRuntime(double sigma)
    {
        using var tool = new FindLineTool(); tool.Parameters.Sigma = sigma;
        Assert.Contains(tool.Validate().Issues, issue => issue.Code == "SIGMA");
    }
    [Fact]
    public void LineGroup_NullListsAndEntries_AreExplicitValidationErrors()
    {
        using var tool = new LineGroupTool();
        tool.Parameters.Lines = null!;
        Assert.Contains(tool.Validate().Issues, issue => issue.Code == "LINES_MISSING");
        tool.Parameters.Lines = [null!];
        Assert.Contains(tool.Validate().Issues, issue => issue.Code == "LINE_MISSING");
    }
    [Fact]
    public void AutoBarcode_InvalidCollectionsAndNonFiniteRoi_DoNotReachSdk()
    {
        using var tool = new Vision.Tools.Halcon.Identification.AutoBarcodeTool();
        tool.Parameters.Scales = null!;
        Assert.Contains(tool.Validate().Issues, issue => issue.Code == "SCALES_INVALID");
        tool.Parameters.Scales = [1, 1.5];
        tool.Parameters.Row1 = double.NaN; tool.Parameters.Column1 = 0; tool.Parameters.Row2 = 40; tool.Parameters.Column2 = 40;
        Assert.Contains(tool.Validate().Issues, issue => issue.Code == "ROI_INVALID");
    }
    [Fact]
    public void InvalidSelectionsAndNegativeClipping_AreNotSilentlyMappedToDefaults()
    {
        using var tool = new FindLineTool();
        tool.Parameters.EdgeSelection = (CaliperEdgeSelection)123;
        tool.Parameters.Transition = (EdgeTransition)123;
        tool.Parameters.IgnoreEndPoints = -1;
        var issues = tool.Validate().Issues;
        Assert.Contains(issues, issue => issue.Code == "EDGE_SELECTION");
        Assert.Contains(issues, issue => issue.Code == "TRANSITION");
        Assert.Contains(issues, issue => issue.Code == "IGNORE_END_POINTS");
    }
}
