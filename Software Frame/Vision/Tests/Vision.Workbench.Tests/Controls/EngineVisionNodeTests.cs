using System.Text;
using System.Text.Json;
using FlowEditor.Controls.Core;
using FlowEditor.Controls.Discovery;
using FlowEditor.Core;
using FlowEditor.Core.Models;
using FlowEditor.Core.Nodes;
using Vision.Core;
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Models;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.FlowNode;
using Vision.Tools;
using Xunit;

namespace Vision.Workbench.Tests.Controls;

/// <summary>离线节点模型验证：不创建Application/窗口，不启动宿主，不触碰HALCON或真实设备。</summary>
public sealed class EngineVisionNodeTests
{
    [Theory]
    [InlineData(false, 2, 2, true)]
    [InlineData(true, 0, 0, true)]
    [InlineData(false, 1, 1, false)]
    public async Task RealOpenCvFourNodeChainPreservesEmptyAndNg(bool empty, int minimum, int maximum, bool passed)
    {
        using var scene = new Scene(empty);
        await using var engine = new ControlledEngine();
        await using var file = new ImageFileNode(engine) { FilePath = scene.Path };
        await using var threshold = new ThresholdRangeNode(engine) { Minimum = 128, Maximum = 255 };
        await using var blob = new BlobNode(engine);
        await using var count = new BlobCountNode(engine) { Minimum = minimum, Maximum = maximum };
        await file.ExecuteAsync(NodeExecutionContext.CreateTest());
        threshold.FindInputPort(nameof(threshold.Image))!.Value = file.OutputImage;
        await threshold.ExecuteAsync(NodeExecutionContext.CreateTest());
        blob.FindInputPort(nameof(blob.Source))!.Value = threshold.Result;
        await blob.ExecuteAsync(NodeExecutionContext.CreateTest());
        var inputFrame = Assert.IsType<VisionFrame>(file.OutputImage);
        var binaryFrame = Assert.IsType<VisionFrame>(threshold.OutputImage);
        var blobSnapshot = Assert.IsType<BlobOutput>(blob.Result);
        Assert.Equal(empty ? VisionExecutionStatus.NoMatch : VisionExecutionStatus.Success, blob.LastExecutionStatus);
        Assert.False(file.RequiresFlowInput);
        // 源节点已关闭，数量判定仍消费完整托管快照，不重跑图像算法。
        await file.DisposeAsync();
        await threshold.DisposeAsync();
        await blob.DisposeAsync();
        count.FindInputPort(nameof(count.Source))!.Value = blobSnapshot;
        await count.ExecuteAsync(NodeExecutionContext.CreateTest());
        Assert.Equal(empty ? 0 : 2, blobSnapshot.Items.Count);
        Assert.Equal(4, engine.CreateCalls);
        Assert.Equal(3, engine.ReleaseCalls);
        Assert.Equal(passed, count.Passed);
        Assert.Equal(NodeStatus.Success, count.Status);
        Assert.Equal(VisionExecutionStatus.Success, count.LastExecutionStatus);
        Assert.Equal(inputFrame.CoordinateSpaceId, count.Result!.CoordinateSpaceId);
        Assert.Equal(binaryFrame.FrameId, count.Result.FrameId);
        Assert.NotEqual(inputFrame.FrameId, binaryFrame.FrameId);
        Assert.Equal(passed, count.FindOutputPort(nameof(count.Passed))!.Value);
        Assert.Null(System.Windows.Application.Current);
    }

    [Fact]
    public async Task MissingCurrentPortCannotReusePreviousInputOrResult()
    {
        using var scene = new Scene(false);
        await using var engine = VisionEngineFactory.Create(new VisionEngineSelection("opencv"));
        await using var file = new ImageFileNode(engine) { FilePath = scene.Path };
        await using var node = new ThresholdRangeNode(engine);
        await file.ExecuteAsync(CancellationToken.None);
        node.FindInputPort(nameof(node.Image))!.Value = file.OutputImage;
        await node.ExecuteAsync(NodeExecutionContext.CreateTest());
        Assert.NotNull(node.Result);
        node.FindInputPort(nameof(node.Image))!.Value = null;
        await Assert.ThrowsAsync<InvalidOperationException>(() => node.ExecuteAsync(NodeExecutionContext.CreateTest()));
        Assert.Null(node.Image);
        Assert.Null(node.Result);
        Assert.Null(node.FindOutputPort(nameof(node.Result))!.Value);
        Assert.Null(node.OutputImage);
    }

    [Fact]
    public async Task ChangedConfigurationAndFailedExecutionClearPublishedPorts()
    {
        using var scene = new Scene(false);
        await using var engine = VisionEngineFactory.Create(new VisionEngineSelection("opencv"));
        await using var node = new ImageFileNode(engine) { FilePath = scene.Path };
        await node.ExecuteAsync(CancellationToken.None);
        node.FilePath = scene.Path + ".missing";
        Assert.Null(node.Result);
        Assert.Null(node.FindOutputPort(nameof(node.OutputImage))!.Value);
        await Assert.ThrowsAsync<InvalidOperationException>(() => node.ExecuteAsync(CancellationToken.None));
        Assert.Equal(VisionExecutionStatus.Failed, node.LastExecutionStatus);
        Assert.NotNull(node.LastIssue);
        Assert.Null(node.Result);
    }

    [Fact]
    public async Task ParameterChangeNotificationNeverExposesOldResult()
    {
        using var scene = new Scene(false);
        await using var engine = VisionEngineFactory.Create(new VisionEngineSelection("opencv"));
        await using var node = new ImageFileNode(engine) { FilePath = scene.Path };
        await node.ExecuteAsync(CancellationToken.None);
        var observed = false;
        node.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(node.FilePath))
            {
                observed = true;
                Assert.Null(node.Result);
            }
        };
        node.FilePath = scene.Path + ".changed";
        Assert.True(observed);
    }

    [Fact]
    public async Task CloseWaitsRejectsLatePublicationAndDoesNotDisposeBorrowedEngine()
    {
        using var scene = new Scene(false);
        await using var engine = new ControlledEngine();
        var node = new ImageFileNode(engine) { FilePath = scene.Path };
        engine.Hold = true;
        var run = node.ExecuteAsync(CancellationToken.None);
        await engine.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.ThrowsAsync<InvalidOperationException>(() => node.ExecuteAsync(CancellationToken.None));
        Assert.Throws<InvalidOperationException>(() => node.FilePath = scene.Path + ".changed");
        var close = node.DisposeAsync().AsTask();
        Assert.False(close.IsCompleted);
        Assert.Equal(0, engine.ReleaseCalls);
        engine.Resume.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        await close;
        Assert.Null(node.Result);
        Assert.Equal(1, engine.ReleaseCalls);
        Assert.Equal(0, engine.DisposeCalls);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => node.ExecuteAsync(CancellationToken.None));
    }

    [Fact]
    public async Task FailedReleaseRetainsContextForRetry()
    {
        using var scene = new Scene(false);
        await using var engine = new ControlledEngine();
        var node = new ImageFileNode(engine) { FilePath = scene.Path };
        await node.ExecuteAsync(CancellationToken.None);
        engine.FailRelease = true;
        await Assert.ThrowsAsync<IOException>(() => node.DisposeAsync().AsTask());
        await node.DisposeAsync();
        await node.DisposeAsync();
        Assert.Equal(2, engine.ReleaseCalls);
        Assert.Equal(0, engine.DisposeCalls);
        Assert.Null(node.Result);
    }

    [Fact]
    public async Task CancelledCallCannotPublishEvenWhenEngineReturnsValue()
    {
        using var scene = new Scene(false);
        await using var engine = new ControlledEngine();
        await using var node = new ImageFileNode(engine) { FilePath = scene.Path };
        await node.ExecuteAsync(CancellationToken.None);
        engine.Hold = true;
        using var cancel = new CancellationTokenSource();
        var run = node.ExecuteAsync(cancel.Token);
        await engine.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancel.Cancel();
        engine.Resume.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Null(node.Result);
        Assert.Null(node.FindOutputPort(nameof(node.Result))!.Value);
        Assert.Equal(VisionExecutionStatus.Cancelled, node.LastExecutionStatus);
        engine.Hold = false;
        await node.ExecuteAsync(CancellationToken.None);
        Assert.NotNull(node.Result);
        Assert.Equal(1, engine.CreateCalls);
    }

    [Fact]
    public async Task UnsupportedRemainsUnsupportedWithoutOldValueOrFallback()
    {
        using var scene = new Scene(false);
        await using var engine = new ControlledEngine();
        await using var node = new ImageFileNode(engine) { FilePath = scene.Path };
        await node.ExecuteAsync(CancellationToken.None);
        engine.Unsupported = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => node.ExecuteAsync(CancellationToken.None));
        Assert.Equal(VisionExecutionStatus.Unsupported, node.LastExecutionStatus);
        Assert.Null(node.Result);
    }

    [Fact]
    public async Task FourNodeDocumentsRoundTripOnlyConfigurationWithoutCreatingContexts()
    {
        await using var engine = new ControlledEngine();
        var catalog = new FlowNodeCatalog([typeof(ImageFileNode).Assembly]);
        Assert.Equal(4, catalog.Discover());
        var factory = new ReflectionNodeFactory(null, catalog);
        factory.RegisterService<IVisionEngine>(engine);
        var mapper = new FlowDocumentMapper(factory, catalog);
        FlowEditor.Controls.Nodes.NodeModel[] nodes =
        [
            new ImageFileNode(engine) { FilePath = System.IO.Path.GetFullPath("offline.pgm") },
            new ThresholdRangeNode(engine) { Minimum = 16, Maximum = 240 },
            new BlobNode(engine) { MaximumResults = 12 },
            new BlobCountNode(engine) { Minimum = 2, Maximum = 5 }
        ];
        foreach (var node in nodes)
        {
            var document = mapper.ToNodeDocument(node);
            var json = JsonSerializer.Serialize(document);
            Assert.DoesNotContain("CoordinateSpaceId", json);
            Assert.DoesNotContain("EngineInstanceId", json);
            Assert.DoesNotContain("Pixels", json);
            var copy = factory.Create(JsonSerializer.Deserialize<NodeDocumentModel>(json)!);
            Assert.Equal(node.GetType(), copy.GetType());
            Assert.Equal(document.Properties.Keys.Order(), mapper.ToNodeDocument(copy).Properties.Keys.Order());
            Assert.Equal(JsonSerializer.Serialize(((INodeDocumentState)node).CaptureDocumentState()),
                JsonSerializer.Serialize(((INodeDocumentState)copy).CaptureDocumentState()));
            Assert.Equal(node.CustomInputPorts.Select(port => port.Id), copy.CustomInputPorts.Select(port => port.Id));
            Assert.Equal(node.CustomOutputPorts.Select(port => port.Id), copy.CustomOutputPorts.Select(port => port.Id));
            await ((IAsyncDisposable)copy).DisposeAsync();
            await ((IAsyncDisposable)node).DisposeAsync();
        }

        Assert.Equal(0, engine.CreateCalls);
        Assert.Equal(0, engine.ReleaseCalls);
    }

    [Fact]
    public async Task UnknownVersionAndLegacyStateAreRejectedNotReinterpreted()
    {
        await using var engine = new ControlledEngine();
        await using var node = new BlobCountNode(engine);
        var state = node.CaptureDocumentState();
        state["Version"] = 2;
        Assert.Throws<InvalidOperationException>(() => node.RestoreDocumentState(state));
        state["Version"] = 1;
        state["ToolIdentity"] = "OpenCV/Blob";
        Assert.Throws<InvalidOperationException>(() => node.RestoreDocumentState(state));
        Assert.Equal(0, engine.CreateCalls);
        var catalog = new FlowNodeCatalog([typeof(ImageFileNode).Assembly]);
        Assert.Null(catalog.TryResolveType("vision.opencv.blob", "Vision.FlowNode.OpenCv.BlobNode, Vision.FlowNode"));
    }

    /// <summary>同一真实OpenCV引擎外只控制等待/清理故障；不模拟视觉算法或厂商回退。</summary>
    private sealed class ControlledEngine : IVisionEngine
    {
        private readonly IVisionEngine _inner = VisionEngineFactory.Create(new VisionEngineSelection("opencv"));
        internal bool Hold, FailRelease, Unsupported;
        internal int CreateCalls, ReleaseCalls, DisposeCalls;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public VisionEngineDescriptor Descriptor => _inner.Descriptor;
        public ValueTask<VisionContext> CreateContextAsync(CancellationToken ct)
        {
            CreateCalls++;
            return _inner.CreateContextAsync(ct);
        }
        public ValueTask<IReadOnlyList<VisionIssue>> ValidateAsync(VisionContext context, VisionOperation operation, CancellationToken ct) => _inner.ValidateAsync(context, operation, ct);
        public ValueTask<VisionModelReference> PrepareModelAsync(VisionContext context, VisionModelDefinition definition, long revision, CancellationToken ct) => _inner.PrepareModelAsync(context, definition, revision, ct);
        public async ValueTask<VisionOutcome<T>> ExecuteAsync<T>(VisionContext context, VisionOperation<T> operation, CancellationToken ct) where T : class
        {
            if (Unsupported)
            {
                return new(VisionExecutionStatus.Unsupported, null, new VisionIssue("Unsupported", "受控的不支持边界。"));
            }

            if (Hold)
            {
                Entered.TrySetResult();
                await Resume.Task;
                // 故意忽略取消，验证节点自身不发布迟到成功。
                return await _inner.ExecuteAsync(context, operation, CancellationToken.None);
            }

            return await _inner.ExecuteAsync(context, operation, ct);
        }
        public ValueTask ReleaseContextAsync(VisionContext context, CancellationToken ct)
        {
            ReleaseCalls++;
            if (FailRelease)
            {
                FailRelease = false;
                throw new IOException("受控清理失败。");
            }

            return _inner.ReleaseContextAsync(context, ct);
        }
        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            return _inner.DisposeAsync();
        }
    }

    /// <summary>独立写出的8×8 PGM：两个互不连通的2×2亮块，非算法输出反算期望。</summary>
    private sealed class Scene : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".pgm");
        internal Scene(bool empty)
        {
            var pixels = new byte[64];
            if (!empty)
            {
                foreach (var index in new[] { 9, 10, 17, 18, 45, 46, 53, 54 })
                {
                    pixels[index] = 220;
                }
            }

            File.WriteAllBytes(Path, Encoding.ASCII.GetBytes("P5\n8 8\n255\n").Concat(pixels).ToArray());
        }
        public void Dispose() => File.Delete(Path);
    }
}
