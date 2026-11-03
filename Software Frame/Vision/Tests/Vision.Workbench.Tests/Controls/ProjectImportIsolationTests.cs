using System.Text.Json;
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class ProjectImportIsolationTests(WorkbenchAppFixture host)
{
    [Theory]
    [InlineData("parameters")]
    [InlineData("factory")]
    [InlineData("validation")]
    [InlineData("provider")]
    public void PreparationFailure_PreservesTheLiveSessionAndDisposesOnlyTheCandidate(string failure)
    {
        host.Invoke(() =>
        {
            var originalTool = new TestTool();
            TestTool? candidate = null;
            var original = new ToolEntry("Original", "Test", () => (originalTool, new Parameters(), null));
            var incoming = new ToolEntry("Incoming", "Test", () =>
            {
                if (failure == "factory") throw new InvalidOperationException("factory rejected");
                candidate = new TestTool { ThrowOnValidate = failure == "validation" };
                return (candidate, new Parameters(), failure == "provider" ? _ => { } : null);
            });
            var catalog = new VisionBrowserCatalog([original, incoming], [new("Unavailable", false)],
                _ => throw new InvalidOperationException("provider rejected"), TestBrowserViewModel.CreateInput);
            using var workspace = new VisionBrowserWorkspace(catalog);
            workspace.LoadImage(); workspace.RoiEditor.AddRectangle(20, 20, 10, 12);
            workspace.Model.CurrentCard!.Run();
            var card = workspace.Model.CurrentCard; var parameters = workspace.Model.CurrentParameters;
            var image = workspace.Model.InputImage; var result = card.LastResult;
            var roi = workspace.RoiEditor.Selected;
            var json = failure == "parameters"
                ? """{"Version":1,"Tool":"Incoming","Parameters":{"Count":"broken"},"Rois":[]}"""
                : """{"Version":1,"Tool":"Incoming","Provider":"Unavailable","Parameters":{"Count":9},"Rois":[]}""";

            Assert.ThrowsAny<Exception>(() => workspace.ImportProjectJson(json));

            Assert.Same(original, workspace.Model.SelectedEntry);
            Assert.Same(card, workspace.Model.CurrentCard); Assert.Same(parameters, workspace.Model.CurrentParameters);
            Assert.Same(image, workspace.Model.InputImage); Assert.Same(result, card.LastResult);
            Assert.Same(roi, Assert.Single(workspace.RoiEditor.Rois)); Assert.Same(roi, workspace.RoiEditor.Selected);
            Assert.Equal(0, originalTool.DisposeCount);
            if (candidate != null) Assert.Equal(1, candidate.DisposeCount);
            Assert.Equal(1, originalTool.RunCount);
        });
    }

    [Fact]
    public void SuccessfulPreparation_CommitsANewSessionWithoutExecutingIt()
    {
        host.Invoke(() =>
        {
            var tools = new List<TestTool>();
            var entry = new ToolEntry("Tool", "Test", () =>
            {
                var tool = new TestTool(); tools.Add(tool); return (tool, new Parameters(), null);
            });
            using var workspace = new VisionBrowserWorkspace(new([entry], [],
                _ => throw new InvalidOperationException(), () => null));
            var old = workspace.Model.CurrentCard;
            workspace.ImportProjectJson("""{"Version":1,"Tool":"Tool","Parameters":{"Count":9},"Rois":[]}""");
            Assert.NotSame(old, workspace.Model.CurrentCard);
            Assert.Equal(9, Assert.IsType<Parameters>(workspace.Model.CurrentParameters).Count);
            Assert.Equal(1, tools[0].DisposeCount); Assert.Equal(0, tools[1].DisposeCount);
            Assert.All(tools, tool => Assert.Equal(0, tool.RunCount));
        });
    }

    [Fact]
    public void ProviderSetterFailure_ReleasesCandidateToolBeforeProvider_AndKeepsTheLiveTool()
    {
        host.Invoke(() =>
        {
            var releases = new List<string>();
            var live = new TestTool();
            var candidate = new TestTool { OnDispose = () => releases.Add("tool") };
            var provider = new TestProvider(() => releases.Add("provider"));
            var original = new ToolEntry("Original", "Test", () => (live, new Parameters(), null));
            var incoming = new ToolEntry("Incoming", "Test", () =>
                (candidate, new Parameters(), _ => throw new InvalidOperationException("setter failed")));
            using var workspace = new VisionBrowserWorkspace(new([original, incoming], [new("Fake", true)],
                _ => provider, () => null));
            var card = workspace.Model.CurrentCard;
            Assert.Throws<InvalidOperationException>(() => workspace.ImportProjectJson(
                """{"Version":1,"Tool":"Incoming","Provider":"Fake","Rois":[]}"""));
            Assert.Same(card, workspace.Model.CurrentCard); Assert.Equal(0, live.DisposeCount);
            Assert.Equal(new[] { "tool", "provider" }, releases);
        });
    }

    [Fact]
    public void BusySession_RejectsImportBeforeConstructingACandidate()
    {
        host.Invoke(() =>
        {
            var creates = 0;
            using var workspace = new VisionBrowserWorkspace(new(
                [new("Tool", "Test", () => { creates++; return (new TestTool(), new Parameters(), null); })], [],
                _ => throw new InvalidOperationException(), () => null));
            var card = workspace.Model.CurrentCard!;
            card.Model.IsBusy = true;
            Assert.Throws<InvalidOperationException>(() => workspace.ImportProjectJson("""{"Tool":"Tool","Rois":[]}"""));
            Assert.Equal(1, creates); Assert.Same(card, workspace.Model.CurrentCard);
            card.Model.IsBusy = false;
        });
    }

    [Fact]
    public void FileReadFailure_UsesInjectedStoreAndPreservesTheCurrentSession()
    {
        host.Invoke(() =>
        {
            var tool = new TestTool(); var store = new FailingStore();
            using var workspace = new VisionBrowserWorkspace(new(
                [new("Tool", "Test", () => (tool, new Parameters(), null))], [],
                _ => throw new InvalidOperationException(), () => null,
                PickProjectPath: _ => "missing.visionproj"), store);
            var card = workspace.Model.CurrentCard;
            workspace.ImportProject();
            Assert.Equal(1, store.Reads); Assert.Same(card, workspace.Model.CurrentCard);
            Assert.Contains("read failed", workspace.Model.ImageInfoText); Assert.Equal(0, tool.DisposeCount);
        });
    }

    private sealed class FailingStore : IVisionProjectFileStore
    {
        public int Reads { get; private set; }
        public string Read(string path) { Reads++; throw new System.IO.IOException("read failed"); }
        public void Write(string path, string json) => throw new InvalidOperationException("not used");
    }
    private sealed class TestProvider(Action release) : Vision.Core.AI.IAIProvider, IDisposable
    {
        public string Name => "Fake";
        public Vision.Core.AI.AICapabilities Capabilities => default;
        public bool IsAvailable => true;
        public Vision.Core.AI.IAIModelSession OpenSession(Vision.Core.AI.AIModelRequest request) => throw new InvalidOperationException("must not load");
        public void CloseSession(Vision.Core.AI.IAIModelSession session) { }
        public void Dispose() => release();
    }
    public sealed class Parameters { public int Count { get; set; } = 2; }
    private sealed class TestTool : IExecutableTool
    {
        public bool ThrowOnValidate { get; init; }
        public Action? OnDispose { get; init; }
        public int DisposeCount { get; private set; }
        public int RunCount { get; private set; }
        public string Name => "Fake";
        public long LastElapsedMs => 0;
        public ValidationResult Validate() => ThrowOnValidate ? throw new InvalidOperationException("validation failed") : new();
        public ToolResult Run(VisionImage? image) { RunCount++; return new() { Success = true }; }
        public void EnsureLoaded() => throw new InvalidOperationException("Import must not load a model");
        public void Dispose() { DisposeCount++; OnDispose?.Invoke(); }
    }
}
