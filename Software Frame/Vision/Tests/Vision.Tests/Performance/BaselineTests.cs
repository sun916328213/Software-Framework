using System.Diagnostics;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tests.TestAssets;
using Vision.Tools;
using Xunit;
using Xunit.Abstractions;

namespace Vision.Tests.Performance;

/// <summary>
/// 单次执行耗时记录，不设时间阈值；含引擎校验/算法/快照发布，不含装配与模型准备。
/// 未预热，不是稳定基准或与旧Tool.ElapsedMs同口径的性能对比。
/// </summary>
[Trait("Category", "Performance")]
public sealed class BaselineTests(ITestOutputHelper output)
{
    [Fact]
    public async Task OpenCv_GoldenPipeline_RecordsElapsed()
    {
        var image = GoldenImages.Rect600();
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var thresholdRequest = new ThresholdRangeOperation(image, 128, 255);
        var timer = Stopwatch.StartNew();
        var threshold = await engine.ExecuteAsync(context, thresholdRequest, default);
        timer.Stop();
        var thresholdMs = timer.Elapsed.TotalMilliseconds;
        Assert.Equal(VisionExecutionStatus.Success, threshold.Status);

        // 真正消费前一步的二值派生图，而非再次读取原图假称流水线。
        var blobRequest = new BlobOperation(threshold.Value!.Image, 255, 255);
        timer.Restart();
        var blob = await engine.ExecuteAsync(context, blobRequest, default);
        timer.Stop();
        Assert.Equal(VisionExecutionStatus.Success, blob.Status);
        Assert.Equal(GoldenImages.Rect600Expected().Area, Assert.Single(blob.Value!.Items).Area);
        Assert.Equal(threshold.Value.Image.FrameId, blob.Value.FrameId);
        output.WriteLine($"[single-call,end-to-end,no-warmup] Threshold(Rect600)={thresholdMs:F3}ms Blob(binary)={timer.Elapsed.TotalMilliseconds:F3}ms");
        await engine.ReleaseContextAsync(context, default);
    }

    [Fact]
    public async Task OpenCv_TemplateMatch_RecordsElapsed()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context,
            new TemplateModelDefinition(GoldenImages.PatternTemplate(), new(0, 0), new()), 1, default);
        var request = new TemplateMatchOperation(GoldenImages.PastePattern(), model, new(), 0.9);
        var timer = Stopwatch.StartNew();
        var result = await engine.ExecuteAsync(context, request, default);
        timer.Stop();
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var match = Assert.Single(result.Value!.Matches);
        var (x, y) = GoldenImages.PastePatternExpected();
        Assert.InRange(match.Position.X, x - 1, x + 1);
        Assert.InRange(match.Position.Y, y - 1, y + 1);
        output.WriteLine($"[single-call,end-to-end,no-warmup,prepare-excluded] TemplateMatch(PastePattern)={timer.Elapsed.TotalMilliseconds:F3}ms");
        await engine.ReleaseContextAsync(context, default);
    }
}
