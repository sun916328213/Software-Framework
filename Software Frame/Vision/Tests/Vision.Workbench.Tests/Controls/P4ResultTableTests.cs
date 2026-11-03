using System.Data;
using Vision.Controls.Presentation;
using Vision.Controls.ViewModels;
using Vision.Core.AI;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Tools.Halcon.Identification;

namespace Vision.Workbench.Tests.Controls;

[Trait("Category", "Unit")]
public sealed class P4ResultTableTests
{
    [Fact]
    public void DetectionRows_AreReadOnlySnapshots_AndSortingDoesNotChangeOriginalOrderOrIds()
    {
        var result = new AIDetectionResult
        {
            Success = true,
            Boxes = [new() { Label = "first", ClassId = 3, Score = 0.6, Box = new(1, 2, 3, 4) },
                     new() { Label = "second", ClassId = 7, Score = 0.9, Box = new(5, 6, 7, 8) }]
        };
        using var table = VisionResultTable.Create(result)!;
        Assert.False(table.View.AllowEdit);
        Assert.False(table.View.AllowNew);
        Assert.False(table.View.AllowDelete);
        table.View.Sort = "分数 DESC";
        Assert.Equal(2, table.View[0]["ID"]);
        Assert.Equal("first", result.Boxes[0].Label);
        result.Boxes[1].Label = "changed";
        result.Boxes.Clear();
        Assert.Equal("second", table.View[0]["类别"]);
        Assert.Equal(5d, table.View[0]["左上 X"]);
        Assert.ThrowsAny<DataException>(() => table.View[0]["类别"] = "not allowed");
    }

    [Fact]
    public void CodeTable_DoesNotPresentUnimplementedCentersAsRealLocations()
    {
        using var result = new BarcodeResult { Success = true, Codes = [new() { Text = "ABC", Symbology = "auto" }] };
        using var table = VisionResultTable.Create(result)!;
        Assert.Equal(3, table.View.Table!.Columns.Count);
        Assert.Equal("ABC", table.View[0]["解码文本"]);
        Assert.DoesNotContain(table.View.Table.Columns.Cast<DataColumn>(), c => c.ColumnName.Contains("中心"));
        Assert.Contains("尚未提供真实位置", VisionDisplay.Details(result));
        Assert.Empty(VisionDisplay.ResultItems(result)); // 不冒充 Blob 共享选择。
    }

    [Fact]
    public void FailedOrUnsupportedResults_ProduceNoTable_WhileSuccessfulEmptyResultsKeepTheirSchema()
    {
        Assert.Null(VisionResultTable.Create(new AIDetectionResult { Success = false }));
        Assert.Null(VisionResultTable.Create(new ToolResult { Success = true }));
        using var empty = VisionResultTable.Create(new AIClassificationResult { Success = true })!;
        Assert.Empty(empty.View.Cast<DataRowView>());
        Assert.Equal(3, empty.View.Table!.Columns.Count);
        using var anomaly = VisionResultTable.Create(new AIAnomalyResult { Success = true, Score = 2.5 })!;
        Assert.Equal(2.5, anomaly.View[0]["原始异常分数"]);
        Assert.Equal("未提供有效定位", anomaly.View[0]["定位信息"]);
    }

    [Fact]
    public void CardOwnsTableLifetime_RerunFailureParameterChangeAndDisposeUnpublishOldTables()
    {
        var tool = new FakeTool();
        using var card = new ToolCardViewModel(tool);
        card.Run();
        var first = card.TableRows;
        Assert.True(card.HasTabularResults);
        card.Run();
        Assert.NotSame(first, card.TableRows);
        card.ClearResult();
        Assert.Null(card.TableRows);
        Assert.False(card.HasTabularResults);
        tool.Fail = true;
        card.Run();
        Assert.Null(card.TableRows);
        tool.Fail = false;
        card.Run();
        Assert.True(card.HasTabularResults);
        card.Dispose();
        Assert.Null(card.TableRows);
        Assert.True(tool.Disposed);
    }

    private sealed class FakeTool : IExecutableTool
    {
        public bool Fail { get; set; }
        public bool Disposed { get; private set; }
        public string Name => "Fake results";
        public long LastElapsedMs => 0;
        public ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image) => new AIClassificationResult
        { Success = !Fail, Scores = [new() { Label = "test", ClassId = 2, Score = 0.8 }] };
        public void EnsureLoaded() { }
        public void Dispose() => Disposed = true;
    }
}
