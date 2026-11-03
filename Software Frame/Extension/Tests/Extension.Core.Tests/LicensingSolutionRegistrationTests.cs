using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Extension.Core.Tests;

public sealed class LicensingSolutionRegistrationTests
{
    [Fact]
    public void RootSolutionShowsEveryLicensingProjectUnderItsModuleFolder()
    {
        string root = Root();
        string source = File.ReadAllText(Path.Combine(root, "Software Frame.sln"));
        var entries = Regex.Matches(source,
                "Project\\(\"\\{(?<kind>[^}]+)\\}\"\\) = \"(?<name>[^\"]+)\", \"(?<path>[^\"]+)\", \"\\{(?<id>[^}]+)\\}\"")
            .Select(match => new
            {
                Kind = match.Groups["kind"].Value, Name = match.Groups["name"].Value,
                Path = match.Groups["path"].Value.Replace('\\', '/'), Id = match.Groups["id"].Value
            }).ToArray();
        var folder = Assert.Single(entries, entry => entry.Name.EndsWith("Licensing", StringComparison.Ordinal) &&
            entry.Kind == "2150E333-8FDC-42A3-9474-1A3956D46DE8");
        string nested = Regex.Match(source, @"GlobalSection\(NestedProjects\)[^\r\n]*\r?\n(?<body>[\s\S]*?)EndGlobalSection")
            .Groups["body"].Value;
        var parents = Regex.Matches(nested, @"\{(?<child>[^}]+)\}\s*=\s*\{(?<parent>[^}]+)\}")
            .ToDictionary(match => match.Groups["child"].Value, match => match.Groups["parent"].Value,
                StringComparer.OrdinalIgnoreCase);
        var known = entries.Select(entry => entry.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var folders = entries.Where(entry => entry.Kind == "2150E333-8FDC-42A3-9474-1A3956D46DE8")
            .Select(entry => entry.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in parents)
        {
            Assert.Contains(pair.Key, known);
            Assert.Contains(pair.Value, folders);
        }
        foreach (string project in new[]
        {
            "Licensing.Core/Licensing.Core.csproj", "Licensing.Authorization/Licensing.Authorization.csproj",
            "Licensing.Activation/Licensing.Activation.csproj",
            "Licensing.Client/Licensing.Client.csproj", "Licensing.Workbench/Licensing.Workbench.csproj",
            "Tests/Licensing.Tests/Licensing.Tests.csproj", "Tests/Licensing.Wpf.Tests/Licensing.Wpf.Tests.csproj"
        })
        {
            string path = "Software Frame/Licensing/" + project;
            var entry = Assert.Single(entries, item => item.Path == path);
            Assert.True(File.Exists(Path.Combine(root, path)), path);
            string cursor = entry.Id;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (!string.Equals(cursor, folder.Id, StringComparison.OrdinalIgnoreCase))
            {
                Assert.True(seen.Add(cursor), "Solution folder cycle: " + path);
                Assert.True(parents.TryGetValue(cursor, out var parent), "Not grouped under Licensing: " + path);
                cursor = parent!;
            }
        }
    }

    private static string Root([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "..", ".."));
}
