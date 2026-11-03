using System.IO;
using System.Text;
using Vision.Controls.Services;

namespace Vision.Workbench.Tests.Controls;

[Trait("Category", "Regression")]
public sealed class VisionProjectFileStoreTests
{
    [Fact]
    public void Write_ReplacesCompleteFileAndReadsUnicode_WithoutLeavingTemporaryFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"vision-project-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "sample.visionproj");
            var store = new VisionProjectFileStore();
            File.WriteAllText(path, "old");
            store.Write(path, "{\"Tool\":\"中文工具\"}");
            Assert.Equal("{\"Tool\":\"中文工具\"}", store.Read(path));
            Assert.Equal(new[] { path }, Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void EncodingFailure_DoesNotTruncateExistingFile_AndCleansTemporaryFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"vision-project-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "sample.visionproj");
            File.WriteAllText(path, "original");
            Assert.Throws<EncoderFallbackException>(() => new VisionProjectFileStore().Write(path, "\ud800"));
            Assert.Equal("original", File.ReadAllText(path));
            Assert.Equal(new[] { path }, Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, true); }
    }
}
