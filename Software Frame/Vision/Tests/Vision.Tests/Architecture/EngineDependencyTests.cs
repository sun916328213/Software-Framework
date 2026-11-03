using System.Xml.Linq;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Architecture;

[Trait("Category", "Architecture")]
public sealed class EngineDependencyTests
{
    [Fact]
    public void SharedRuntime_ReferencesOnlyCore_AndExposesNoSecondBusinessInterface()
    {
        var project = XDocument.Load(VisionLayout.ProjectFile(VisionDirectory, "Vision.Tools"));
        Assert.Equal(new[] { "../../Vision.Core/Vision.Core.csproj" }, project.Descendants("ProjectReference").Select(element => (string?)element.Attribute("Include")));
        Assert.Empty(project.Descendants("PackageReference"));
        var assembly = typeof(VisionEngineFactory).Assembly;
        Assert.DoesNotContain(assembly.GetExportedTypes(), type => type.IsInterface);
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), name =>
            name.Name!.StartsWith("OpenCvSharp", StringComparison.Ordinal) || name.Name.Contains("Halcon", StringComparison.OrdinalIgnoreCase) ||
            name.Name.Contains("Controls", StringComparison.Ordinal) || name.Name.Contains("FlowNode", StringComparison.Ordinal));
        Assert.Equal("Vision.Core", Assert.Single(assembly.GetReferencedAssemblies(), name => name.Name!.StartsWith("Vision.", StringComparison.Ordinal)).Name);
    }

    private static string VisionDirectory
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "Vision.Core", "Vision.Core.csproj"))) return directory.FullName;
            throw new DirectoryNotFoundException("Cannot locate Vision module.");
        }
    }
}
