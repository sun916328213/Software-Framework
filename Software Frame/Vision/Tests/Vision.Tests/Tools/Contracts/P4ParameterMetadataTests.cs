using System.Reflection;
using System.Text.Json;
using Vision.Core;
using Vision.Core.Geometry;
using Vision.Tools.Halcon.Calibration;
using Xunit;

namespace Vision.Tests.Tools.Contracts;

[Trait("Category", "Contract")]
public sealed class P4ParameterMetadataTests
{
    [Fact]
    public void PromotedParameterDtos_DescribeEveryPublicProperty_WithoutConstructingRuntimeTools()
    {
        object[] parameters =
        [
            new Vision.Tools.Halcon.Identification.AutoBarcodeParameters(),
            new Vision.Tools.Halcon.Measurement.LineGroupParameters(),
            new Vision.Tools.Halcon.Measurement.CannyLineParameters(),
            new Vision.Tools.Halcon.Measurement.GeometryParameters(),
            new Vision.Tools.Halcon.Coordinate.TransformPointParameters(),
            new Vision.Tools.OpenCv.Processing.IntensityTransformParameters(),
            new Vision.Tools.OpenCv.Processing.AdaptiveThresholdParameters(),
            new Vision.Tools.OpenCv.Processing.ColorSegmentParameters(),
            new Vision.Tools.OpenCv.Processing.ImageConvertParameters(),
            new Vision.Tools.OpenCv.Processing.ImageFilterParameters(),
            new Vision.Tools.OpenCv.Inspection.GrayStatisticsParameters(),
            new Vision.Tools.OpenCv.Processing.ThresholdParameters(),
            new Vision.Tools.OpenCv.Processing.MorphologyParameters(),
            new Vision.Tools.OpenCv.Processing.CropParameters(),
            new Vision.Tools.OpenCv.Matching.TemplateMatchParameters(),
            new Vision.Tools.Halcon.Matching.ShapeMatchParameters(),
            new Vision.Tools.Halcon.Coordinate.FixtureParameters(),
            new Vision.Tools.Halcon.Acquisition.ImageFileParameters(),
            new Vision.Tools.Halcon.Identification.BarcodeParameters(),
            new Vision.Tools.Halcon.Identification.DataCodeParameters(),
            new Vision.Tools.Halcon.Measurement.FindLineParameters(),
            new NPointsCalibrationParameters(),
            new Vision.Tools.OpenCv.Measurement.FindLineParameters(),
            new Vision.Tools.OpenCv.Measurement.FindCircleParameters(),
            new Vision.Tools.AI.Classification.ClassificationParameters(),
            new Vision.Tools.AI.Detection.DetectionParameters(),
            new Vision.Tools.AI.Segmentation.SegmentationParameters(),
            new Vision.Tools.AI.AnomalyDetection.AnomalyDetectionParameters()
        ];
        foreach (var dto in parameters)
            foreach (var property in dto.GetType().GetProperties())
            {
                var metadata = property.GetCustomAttribute<ToolPropertyAttribute>();
                Assert.NotNull(metadata);
                Assert.False(string.IsNullOrWhiteSpace(metadata.DisplayName));
                // 图像由模板会话管理，不能在标量属性面板中编辑像素缓冲。
                if ((dto is Vision.Tools.OpenCv.Measurement.FindLineParameters && property.Name is "UsesCalipers" or "UsesEdgeBand") ||
                    (dto is Vision.Tools.OpenCv.Processing.ThresholdParameters && property.Name == "UsesRange") ||
                    (dto is Vision.Tools.OpenCv.Processing.ImageFilterParameters && property.Name == "UsesGaussian") ||
                    (dto is Vision.Tools.OpenCv.Processing.IntensityTransformParameters && property.Name is "UsesLinear" or "UsesGamma") ||
                    (dto is Vision.Tools.OpenCv.Processing.ColorSegmentParameters && property.Name is
                        "FirstMinimumLabel" or "FirstMaximumLabel" or "SecondMinimumLabel" or "SecondMaximumLabel" or "ThirdMinimumLabel" or "ThirdMaximumLabel"))
                {
                    Assert.True(property.IsDefined(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute)));
                    Assert.False(property.CanWrite); // 显式列出的派生模式条件不写入DTO/JSON、不作为参数行展示。
                    Assert.False(metadata.IsDisplay);
                }
                else Assert.Equal(property.PropertyType != typeof(Vision.Core.Imaging.VisionImage), metadata.IsDisplay);
            }
    }

    [Fact]
    public void FindLineMetadata_DoesNotFlattenOrRenameExistingPointJsonFields()
    {
        var dto = new Vision.Tools.Halcon.Measurement.FindLineParameters { LineStart = new(-12.125, 23.375) };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(dto));
        Assert.Equal(new[] { "CaliperCount", "CaliperHalfLength", "CaliperHalfWidth", "EdgeSelection", "IgnoreEndPoints", "LineEnd", "LineStart", "Sigma", "Threshold", "Transition" },
            json.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        Assert.Equal(-12.125, json.RootElement.GetProperty("LineStart").GetProperty("X").GetDouble());
        var copy = JsonSerializer.Deserialize<Vision.Tools.Halcon.Measurement.FindLineParameters>(json.RootElement.GetRawText())!;
        Assert.Equal(new Point2D(-12.125, 23.375), copy.LineStart);
    }

    [Fact]
    public void AiSentinelValuesAndCalibrationList_KeepExistingSerializationContracts()
    {
        var ai = new Vision.Tools.AI.Detection.DetectionParameters { ScoreThreshold = -1, MaxResults = -1, MinArea = -1 };
        var copy = JsonSerializer.Deserialize<Vision.Tools.AI.Detection.DetectionParameters>(JsonSerializer.Serialize(ai))!;
        Assert.Equal(-1, copy.ScoreThreshold);
        Assert.Equal(-1, copy.MaxResults);
        Assert.Equal(-1, copy.MinArea);
        Assert.True(double.IsNaN(typeof(Vision.Tools.AI.AIModelParameters).GetProperty("ScoreThreshold")!
            .GetCustomAttribute<ToolPropertyAttribute>()!.Min)); // 不把 ≤0 默认值偷偷钳成正数。
        var calibration = new NPointsCalibrationParameters { Points = [new() { PixelX = 17, PixelY = 18, WorldX = 1.7, WorldY = 1.8 }] };
        var restored = JsonSerializer.Deserialize<NPointsCalibrationParameters>(JsonSerializer.Serialize(calibration))!;
        Assert.Equal(17, Assert.Single(restored.Points).PixelX);
    }

    [Fact]
    public void MissingCalibrationListOrNullPoint_ReturnsValidationErrors_WithoutRuntime()
    {
        using var tool = new NPointsCalibrationTool();
        tool.Parameters.Points = null!;
        Assert.Contains(tool.Validate().Issues, i => i.Code == "POINTS_MISSING");
        tool.Parameters.Points = [null!];
        Assert.Contains(tool.Validate().Issues, i => i.Code == "POINT_INVALID");
    }
}
