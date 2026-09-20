using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Extension.Tests
{
    public class ArchitectureContractTests
    {
        private static readonly IReadOnlyDictionary<string, string[]> AllowedDependencies =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Core"] = Array.Empty<string>(),
                ["Automation"] = Array.Empty<string>(),
                ["IO"] = Array.Empty<string>(),
                ["Platform"] = Array.Empty<string>(),
                ["Patterns"] = new[] { "Core" },
                ["UI"] = new[] { "Core" },
                ["Security"] = Array.Empty<string>(),
                ["Imaging"] = new[] { "IO" },
                ["Integration"] = new[] { "Imaging" },
            };

        private static readonly Regex InternalReference = new Regex(
            @"\bExtension\.([A-Za-z_]\w*)",
            RegexOptions.Compiled);

        private static string SourceRoot([CallerFilePath] string thisFile = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "Extension"));

        private static IEnumerable<string> Sources() => Directory
            .EnumerateFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        private static IEnumerable<string> ReferencedModules(string text)
        {
            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.TrimStart();
                if (line.StartsWith("//", StringComparison.Ordinal)) continue;
                foreach (Match match in InternalReference.Matches(rawLine))
                    yield return match.Groups[1].Value;
            }
        }

        [Fact]
        public void 顶层模块必须是封闭集合()
        {
            var actual = Sources()
                .Select(path => Path.GetRelativePath(SourceRoot(), path).Split(Path.DirectorySeparatorChar)[0])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            var expected = AllowedDependencies.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void 模块引用只能走显式允许边()
        {
            var violations = new List<string>();
            foreach (var file in Sources())
            {
                var relative = Path.GetRelativePath(SourceRoot(), file);
                var source = relative.Split(Path.DirectorySeparatorChar)[0];
                var allowed = AllowedDependencies[source];
                foreach (var target in ReferencedModules(File.ReadAllText(file)).Distinct(StringComparer.Ordinal))
                {
                    if (target == source || allowed.Contains(target, StringComparer.Ordinal)) continue;
                    violations.Add($"{relative}: {source} -> {target}");
                }
            }
            Assert.True(violations.Count == 0,
                "发现未授权模块依赖：\n  " + string.Join("\n  ", violations));
        }

        [Fact]
        public void 完全限定名不能绕过模块守卫()
        {
            const string bodyOnly = "var value = Extension.Integration.Mapster.MappingConfig.InitializeAllMappings();";
            Assert.Contains("Integration", ReferencedModules(bodyOnly));
            Assert.Empty(ReferencedModules("// Extension.Integration.Mapster.MappingConfig"));
        }

        [Fact]
        public void 模块依赖图必须无环()
        {
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var module in AllowedDependencies.Keys)
                Visit(module, visiting, visited, new List<string>());
        }

        private static void Visit(string module, HashSet<string> visiting, HashSet<string> visited, List<string> path)
        {
            if (visited.Contains(module)) return;
            if (!visiting.Add(module))
                Assert.Fail("模块依赖成环：" + string.Join(" -> ", path.Concat(new[] { module })));

            path.Add(module);
            foreach (var dependency in AllowedDependencies[module]) Visit(dependency, visiting, visited, path);
            path.RemoveAt(path.Count - 1);
            visiting.Remove(module);
            visited.Add(module);
        }
    }
}