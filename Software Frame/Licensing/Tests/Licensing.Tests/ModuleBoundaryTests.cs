using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace Licensing.Tests;

public sealed class ModuleBoundaryTests
{
    [Fact]
    public void LicensingProjectsAndExplicitResourcesRemainInsideStandaloneModule()
    {
        string root = ModuleRoot();
        string prefix = root + Path.DirectorySeparatorChar;
        var projects = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar)
                .Any(part => part is "bin" or "obj")).ToArray();
        Assert.True(projects.Length >= 7);
        foreach (string project in projects)
        {
            var document = XDocument.Load(project);
            foreach (var item in document.Descendants().Where(x => x.Name.LocalName is "ProjectReference" or "Page"))
            {
                var include = item.Attribute("Include")?.Value;
                if (include == null) continue;
                string resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, include.Replace('\\', Path.DirectorySeparatorChar)));
                Assert.True(resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase),
                    $"Module-external dependency: {project} -> {include}");
                Assert.True(File.Exists(resolved), resolved);
            }
        }
    }

    private static string ModuleRoot([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));
}
