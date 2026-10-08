// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ArchitectureContractTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.IO、System.Linq、System.Runtime.CompilerServices、System.Text.RegularExpressions。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// ArchitectureContractTests 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// </summary>
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

        /// <summary>
        /// 测试相关入口 SourceRoot；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 GetFullPath、Combine、GetDirectoryName；具体条件和先后次序见方法体。
        /// </summary>
        private static string SourceRoot([CallerFilePath] string thisFile = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "Extension"));

        /// <summary>
        /// 测试相关入口 CoreSourceRoot；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 GetFullPath、Combine、SourceRoot；具体条件和先后次序见方法体。
        /// </summary>
        private static string CoreSourceRoot() => Path.GetFullPath(Path.Combine(SourceRoot(), "..", "Extension.Core"));

        /// <summary>
        /// 测试相关入口 RootFor；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 StartsWith、CoreSourceRoot、SourceRoot；具体条件和先后次序见方法体。
        /// </summary>
        private static string RootFor(string file) => file.StartsWith(CoreSourceRoot() + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase) ? CoreSourceRoot() : SourceRoot();

        /// <summary>
        /// 测试相关入口 Sources；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Where、SelectMany、SourceRoot、CoreSourceRoot、EnumerateFiles；具体条件和先后次序见方法体。
        /// </summary>
        private static IEnumerable<string> Sources() => new[] { SourceRoot(), CoreSourceRoot() }
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        /// <summary>
        /// 测试相关入口 ReferencedModules；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Split、TrimStart、StartsWith、Matches；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 测试相关入口 顶层模块必须是封闭集合；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToArray、OrderBy、Distinct、Select、Sources；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void 顶层模块必须是封闭集合()
        {
            var actual = Sources()
                .Select(path => Path.GetRelativePath(RootFor(path), path).Split(Path.DirectorySeparatorChar)[0])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            var expected = AllowedDependencies.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// 测试相关入口 模块引用只能走显式允许边；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Sources、GetRelativePath、RootFor、Split、Distinct；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void 模块引用只能走显式允许边()
        {
            var violations = new List<string>();
            foreach (var file in Sources())
            {
                var relative = Path.GetRelativePath(RootFor(file), file);
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

        /// <summary>
        /// 测试相关入口 完全限定名不能绕过模块守卫；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Contains、ReferencedModules、Empty；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void 完全限定名不能绕过模块守卫()
        {
            const string bodyOnly = "var value = Extension.Integration.Mapster.MappingConfig.InitializeAllMappings();";
            Assert.Contains("Integration", ReferencedModules(bodyOnly));
            Assert.Empty(ReferencedModules("// Extension.Integration.Mapster.MappingConfig"));
        }

        /// <summary>
        /// 测试相关入口 模块依赖图必须无环；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Visit；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void 模块依赖图必须无环()
        {
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var module in AllowedDependencies.Keys)
                Visit(module, visiting, visited, new List<string>());
        }

        /// <summary>
        /// 测试相关入口 Visit；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Contains、Add、Fail、Join、Concat；具体条件和先后次序见方法体。
        /// </summary>
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
