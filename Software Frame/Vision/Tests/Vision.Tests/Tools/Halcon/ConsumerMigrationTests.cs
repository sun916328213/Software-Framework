// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ConsumerMigrationTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Text.Json、Vision.Core.Geometry、Vision.Core.Tools、Vision.Tools.Halcon.Coordinate、Vision.Tools.Halcon.Identification、Vision.Tools.Halcon.Measurement。导入不代表所有类型都会在运行时加载。
using System.Text.Json;
using Vision.Core.Geometry;
using Vision.Core.Tools;
using Vision.Tools.Halcon.Coordinate;
using Vision.Tools.Halcon.Identification;
using Vision.Tools.Halcon.Measurement;
using Vision.Tools.Halcon.Sessions;
using Xunit;

namespace Vision.Tests.Tools.Halcon;

/// <summary>
/// ConsumerMigrationTests 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ConsumerMigrationTests
{
    /// <summary>
    /// 测试相关入口 FindLineFitsMeasuredEdgeInsteadOfReturningTheRequestedLine；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Fill、FromBuffer、RunImage、True、InRange；具体条件和先后次序见方法体。
    /// </summary>
    [HalconRuntimeFact]
    [Trait("Category", "RequiresRuntime")]
    public void FindLineFitsMeasuredEdgeInsteadOfReturningTheRequestedLine()
    {
        var pixels = new byte[256 * 256];
        for (var y = 110; y < 256; y++) Array.Fill(pixels, (byte)255, y * 256, 256);
        var image = Vision.Core.Imaging.VisionImage.FromBuffer(256, 256, Vision.Core.Imaging.PixelFormat.Mono8, pixels);
        using var tool = new FindLineTool();
        tool.Parameters.LineStart = new(50, 100);
        tool.Parameters.LineEnd = new(200, 100);
        var result = tool.RunImage(image);
        Assert.True(result.Success, result.Message);
        Assert.InRange(result.Start.Y, 108, 112);
        Assert.InRange(result.End.Y, 108, 112);
        Assert.True(result.EdgePointCount >= 2);
    }
    /// <summary>
    /// 测试相关入口 PersistedSessionRetainsIdentityNameAndParametersWithoutRuntimeState；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、Equal、Serialize、DoesNotContain、Deserialize；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PersistedSessionRetainsIdentityNameAndParametersWithoutRuntimeState()
    {
        using var original = new TransformPointSession { Name = "calibrated-point" };
        original.Parameters.Matrix = "2,0,4|0,3,5|0,0,1";
        original.Parameters.Point = new(7, 11);
        original.Run();
        Assert.Equal(new Point2D(18, 38), original.LastResult!.Point);

        var json = JsonSerializer.Serialize(original);
        Assert.DoesNotContain("LastResult", json);
        Assert.DoesNotContain("Engine", json);
        Assert.DoesNotContain("EditableParameters", json);
        using var restored = JsonSerializer.Deserialize<TransformPointSession>(json)!;
        Assert.Equal(original.ID, restored.ID);
        Assert.Equal(original.Name, restored.Name);
        restored.Run();
        Assert.Equal(new Point2D(18, 38), restored.LastResult!.Point);
        Assert.Equal(E_ToolState.RunCompleted, restored.ToolState);
    }

    /// <summary>
    /// 测试相关入口 InvalidSessionParametersProduceFailureAndResetDoesNotKeepOldResults；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、True、Equal、False、Single；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void InvalidSessionParametersProduceFailureAndResetDoesNotKeepOldResults()
    {
        using var session = new TransformPointSession();
        session.Run();
        Assert.True(session.LastResult!.Success);
        session.Parameters.Matrix = "broken";
        session.Run();
        Assert.Equal(E_ToolState.Failed, session.ToolState);
        Assert.False(session.LastResult!.Success);
        Assert.Single(session.Results);
        Assert.NotEmpty(session.RunMessage);
        session.Reset();
        Assert.Empty(session.Results);
        Assert.Null(session.LastResult);
    }

    /// <summary>
    /// 测试相关入口 GeometryReturnsActualIntersectionAndRejectsParallelLines；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、True、Equal、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void GeometryReturnsActualIntersectionAndRejectsParallelLines()
    {
        using var tool = new GeometryTool();
        tool.Parameters = new()
        {
            Operation = GeometryOperation.LineIntersection,
            FirstStart = new(0, 0), FirstEnd = new(10, 10),
            SecondStart = new(0, 10), SecondEnd = new(10, 0)
        };
        var result = tool.Run(null);
        Assert.True(result.Success);
        Assert.Equal(new Point2D(5, 5), result.Intersection);
        tool.Parameters.SecondStart = new(0, 1);
        tool.Parameters.SecondEnd = new(10, 11);
        Assert.False(tool.Run(null).Success);
    }

    /// <summary>
    /// 测试相关入口 AutoBarcodeRejectsInvalidScaleBeforeTouchingNativeRuntime；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、False、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void AutoBarcodeRejectsInvalidScaleBeforeTouchingNativeRuntime(double scale)
    {
        using var tool = new AutoBarcodeTool { Parameters = new() { Scales = [scale] } };
        var result = tool.Run(null);
        Assert.False(result.Success);
        Assert.Contains(result.Validation.Issues, issue => issue.Code == "SCALES_INVALID");
    }

    /// <summary>
    /// 测试相关入口 LineGroupRejectsInvalidMemberBeforeTouchingNativeRuntime；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Add、Run、False、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void LineGroupRejectsInvalidMemberBeforeTouchingNativeRuntime()
    {
        using var tool = new LineGroupTool();
        tool.Parameters.Lines.Add(new() { LineStart = new(3, 4), LineEnd = new(3, 4) });
        var result = tool.Run(null);
        Assert.False(result.Success);
        Assert.Contains(result.Validation.Issues, issue => issue.Code == "LINE_INVALID");
    }
}
