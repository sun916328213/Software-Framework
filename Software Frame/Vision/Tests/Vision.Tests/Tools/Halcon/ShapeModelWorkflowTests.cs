using Vision.Core.Imaging;
using Vision.Core.Presentation;
using Vision.Tools.Halcon.Matching;
using Xunit;

namespace Vision.Tests.Tools.Halcon;

public sealed class ShapeModelWorkflowTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public void CaptureAndTrainingSettings_DoNotRequireRuntime_AndDisposedToolRejectsEdits()
    {
        var tool = new ShapeMatchTool();
        ITemplateTool editor = tool;
        editor.SetTemplate(VisionImage.FromBuffer(2, 2, PixelFormat.Mono8, [0, 255, 0, 255]));
        Assert.False(editor.HasModel);
        Assert.True(editor.NeedsTraining);
        Assert.True(editor.RequiresTraining);
        var training = Assert.IsType<ShapeTrainingParameters>(editor.TrainingParameters);
        var changed = new List<string?>();
        training.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        training.ScaleMin = 0.8;
        Assert.Equal(new[] { nameof(ShapeTrainingParameters.ScaleMin) }, changed);
        training.ScaleMin = 0;
        Assert.Throws<ArgumentException>(() => editor.Train()); // 必须在 SDK 图像转换/训练前拒绝。
        Assert.False(editor.HasModel);
        Assert.True(editor.NeedsTraining);
        Assert.Throws<InvalidOperationException>(() => editor.SaveModel("unused.vshape"));
        tool.Dispose();
        Assert.Throws<ObjectDisposedException>(() => editor.SetTemplate(VisionImage.FromBuffer(1, 1, PixelFormat.Mono8, [255])));
    }

    [Fact(Skip = "需要 HALCON Runtime/License；在授权离线 SDK 环境验证训练、清理和旧 shm 兼容。")]
    [Trait("Category", "RequiresRuntime")]
    public void TrainPreviewSaveLoadMatch_DirtySettingsBlockRun_AndFailedLoadPreservesModel()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var pixels = new byte[80 * 80];
            for (var y = 25; y < 55; y++)
                for (var x = 20; x < 45; x++) pixels[y * 80 + x] = 255;
            var image = VisionImage.FromBuffer(80, 80, PixelFormat.Mono8, pixels);
            using var tool = new ShapeMatchTool();
            ITemplateTool editor = tool;
            editor.SetTemplate(image);
            editor.Train();
            Assert.True(tool.IsModelTrained);
            Assert.NotEmpty(editor.ModelContours);
            var package = Path.Combine(directory, "model.vshape");
            var legacy = Path.Combine(directory, "model.shm");
            editor.SaveModel(package);
            editor.SaveModel(legacy);
            editor.ClearModel();
            editor.LoadModel(package);
            Assert.NotNull(editor.TemplateImage);
            tool.Parameters.MinScore = 0.3; // 运行参数改变不能要求重训。
            Assert.True(tool.IsModelTrained);
            using (var result = tool.RunImage(image)) { Assert.True(result.Success, result.Message); Assert.NotEmpty(result.Matches); }
            var training = Assert.IsType<ShapeTrainingParameters>(editor.TrainingParameters);
            training.ScaleMin = 0.7;
            Assert.True(editor.NeedsTraining);
            Assert.Contains(tool.Validate().Issues, i => i.Code == "MODEL_NEEDS_TRAINING");
            editor.Train();
            Assert.True(tool.IsModelTrained);
            var broken = Path.Combine(directory, "broken.vshape");
            File.WriteAllText(broken, "{}");
            Assert.Throws<InvalidDataException>(() => editor.LoadModel(broken));
            Assert.True(tool.IsModelTrained);
            editor.LoadModel(legacy);
            Assert.Null(editor.TemplateImage); // shm 只有原生模型，不伪造取样图。
            Assert.NotEmpty(editor.ModelContours);
            using var loaded = tool.RunImage(image);
            Assert.True(loaded.Success, loaded.Message);
            tool.UnloadModel();
            tool.UnloadModel();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
