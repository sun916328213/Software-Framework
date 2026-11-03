using Vision.Controls.Services;
using Vision.Controls.ViewModels;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Presentation;
using Vision.Core.Results;
using Vision.Tools.Halcon.Matching;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class TemplateEditorWorkflowTests(WorkbenchAppFixture host)
{
    [Fact]
    public void CaptureTrainRuntimeChangesCancelAndSwitch_RespectModelStateAndOwnership()
    {
        host.Invoke(() =>
        {
            var tool = new FakeTemplateTool();
            var picks = 0;
            using var workspace = new VisionBrowserWorkspace(new(
                [new("Arbitrary", "Other", () => (tool, new object(), null)),
                 new("Other", "Test", () => (new FakeTemplateTool(), new object(), null))], [],
                _ => throw new InvalidOperationException(), TestBrowserViewModel.CreateInput,
                (_, _) => { picks++; return null; }));
            using var browser = new VisionBrowserViewModel(workspace);
            using var borrower = new VisionBrowserViewModel(workspace);
            Assert.True(browser.SupportsTemplateInput); // 没有 SetTemplate 回调，不按名称决定能力。
            var editor = Assert.IsType<TemplateEditorViewModel>(browser.TemplateEditor);
            Assert.Same(editor, borrower.TemplateEditor);
            Assert.False(editor.TrainCommand.CanExecute(null));
            browser.LoadImage();
            var input = browser.InputImage;
            browser.RoiEditor.Clear(); browser.RoiEditor.AddRectangle(20, 20, 30, 30);
            browser.SetTemplateFromRoi();
            Assert.Same(input, browser.InputImage);
            Assert.NotNull(editor.PreviewImage);
            Assert.True(editor.PreviewImage!.IsFrozen);
            Assert.True(editor.NeedsTraining);
            editor.Train();
            Assert.Equal(1, tool.TrainCount);
            Assert.True(editor.HasModel);
            Assert.False(editor.NeedsTraining);
            browser.CurrentCard!.Run();
            var result = browser.CurrentCard.LastResult;
            editor.Save(); editor.Load(); // 对话框取消不清空模型、执行结果或选择。
            Assert.Equal(2, picks);
            Assert.Same(result, browser.CurrentCard.LastResult);
            browser.ParametersChanged(); // 普通运行参数变更只使运行结果失效。
            Assert.False(editor.NeedsTraining);
            borrower.Dispose();
            Assert.True(editor.IsActive);
            Assert.False(tool.Disposed);
            editor.TrainingParametersChanged();
            Assert.True(editor.NeedsTraining);
            Assert.False(editor.SaveCommand.CanExecute(null));
            tool.FailTraining = true;
            editor.Train();
            Assert.True(editor.HasModel); // 重训失败保留旧模型，但禁止当作新模型运行。
            Assert.True(editor.NeedsTraining);
            Assert.Contains("操作失败", editor.Status);
            browser.SelectedEntry = browser.ToolEntries[1];
            Assert.False(editor.IsActive);
            Assert.True(tool.Disposed);
            Assert.False(editor.TrainCommand.CanExecute(null));
            Assert.False(editor.Capture(input!));
            Assert.Equal(2, tool.TrainCount);
        });
    }

    internal sealed class FakeTemplateTool : IExecutableTool, ITemplateTool
    {
        public ITemplateTool TemplateTool => this;
        public bool Disposed { get; private set; }
        public int TrainCount { get; private set; }
        public bool FailTraining { get; set; }
        public bool AllowLoad { get; set; }
        public int DirtyCount { get; private set; }
        public ShapeTrainingParameters Training { get; } = new();
        public string Name => "Fake model";
        public long LastElapsedMs => 0;
        public bool RequiresTraining => true;
        public bool HasModel { get; private set; }
        public bool NeedsTraining { get; private set; }
        public VisionImage? TemplateImage { get; private set; }
        public IReadOnlyList<RegionContour> ModelContours => [];
        public object? TrainingParameters => Training;
        public string ModelFileFilter => "Fake|*.fake";
        public ValidationResult Validate() => HasModel && !NeedsTraining ? new() : new ValidationResult().AddError("MODEL", "not ready");
        public ToolResult Run(VisionImage? image) => new() { Success = HasModel && !NeedsTraining };
        public void EnsureLoaded() { }
        public void SetTemplate(VisionImage image) { ObjectDisposedException.ThrowIf(Disposed, this); TemplateImage = VisionImageSnapshot.Copy(image); NeedsTraining = true; }
        public void MarkTrainingDirty() { ObjectDisposedException.ThrowIf(Disposed, this); DirtyCount++; NeedsTraining = true; }
        public void Train()
        {
            ObjectDisposedException.ThrowIf(Disposed, this); TrainCount++;
            if (FailTraining) throw new InvalidOperationException("expected training failure");
            HasModel = true; NeedsTraining = false;
        }
        public void SaveModel(string path) => throw new InvalidOperationException("取消对话框不能保存。");
        public void LoadModel(string path)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            if (!AllowLoad) throw new InvalidOperationException("取消对话框不能加载。");
            Training.ScaleMin = 0.75;
            Training.ScaleMax = 1.25;
            HasModel = true; NeedsTraining = false;
        }
        public void ClearModel() { HasModel = false; NeedsTraining = false; TemplateImage = null; }
        public void Dispose() { Disposed = true; ClearModel(); }
    }
}
