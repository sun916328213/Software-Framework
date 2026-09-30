using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Vision.Tests.Architecture
{
    /// <summary>
    /// P1 验收守卫 —— 《Vision_Architecture_Optimization_Plan v1.2》§9.2「阶段验收与 Tests 对应」
    /// （该条款自 v1.1 起未变，v1.2 的唯一实质差异是 <c>Vision.Controls.Wpf</c> → <c>Vision.Controls</c>）：
    /// <code>
    /// P1 | Solution/项目引用守卫；旧项目只作为迁移源；生产项目不得引用 Vision.Tests。
    /// </code>
    ///
    /// 为什么必须靠测试钉住：本轮的交付物是**项目骨架 + 命名**，没有任何算法行为变化 ——
    /// 也就是说「改坏了」在编译期**完全不报错**（sln 里少登记一个工程、旧项目被新工程引用、
    /// 生产工程反向引用测试工程，全都编译得过）。只有断言才能拦住。
    ///
    /// 约定（与 <c>WPFControl.Tests/ArchitectureDependencyTests</c>、
    /// <c>Vision.Tests/Architecture/RetirementAuditTests</c> 一致）：已知集合写进断言里的字面量清单，
    /// 断言「实际集合 == 登记集合」。骨架新增/退场 ⇒ 测试红，提醒**同步更新登记清单**。
    ///
    /// ⛔ 扫描口径的两条纪律：
    ///   1. 必须跳过 <c>bin</c> / <c>obj</c> / <c>.vs</c> / <c>.workbuddy-ai</c>，
    ///      否则构建产物里的 <c>*_wpftmp.csproj</c> 与备份目录会造成假红；
    ///   2. 每条断言都要配一条**防假绿**断言（扫到的工程数 / 属性数必须大于下限），
    ///      否则路径写错 ⇒ 扫到 0 个 ⇒ 断言恒真 ⇒ 守卫形同不存在。
    /// </summary>
    [Trait("Category", "Architecture")]
    public sealed class SolutionStructureTests
    {
        /// <summary>
        /// P1 交付的目标骨架 —— <c>Vision/</c> 下的工程集合（按目录名 / sln 登记名）。
        ///
        /// 2026-09-29 起改为从 <see cref="VisionLayout"/> 取，本文件不再自持一份清单：
        /// 分层分组（<c>Tools/</c>、<c>AI/</c>）后「工程名 → 目录」是嵌套的，
        /// 再在守卫里各写一遍路径只会制造第二份真源。
        /// </summary>
        private static string[] ExpectedVisionProjects => VisionLayout.ProjectNames;

        /// <summary>
        /// P9 退役清单：这些工程**必须保持不存在**（目录、csproj、sln 登记、全仓引用四者皆无）。
        /// 只作为历史名字登记在这里 —— 谁把它们引回来，<see cref="RetiredProjects_AreGoneFromSolutionDiskAndReferences"/> 就红。
        /// </summary>
        private static readonly string[] RetiredProjects =
        [
            "DPLibrary_Halcon_F48",   // 迁移源（2026-09-29：目录与 csproj 已删除，sln 除名）
        ];

        /// <summary>P1 骨架的最小规模，用作「扫描没失效」的下限（P9：12 - 迁移源 1 = 11）。</summary>
        private const int MinimumVisionProjects = 11;

        // ─────────────────────────────────────────────────────────────────────
        // 1. Vision/ 下的工程集合与物理位置 == 登记的骨架布局
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void VisionProjects_MatchRegisteredLayout()
        {
            // ① 登记清单自洽：每个工程都能在登记的目录里找到同名 csproj。
            //    这条把「VisionLayout 写错目录名」的红信息从下面那条的集合差异里摘出来，
            //    直接指向具体是哪个工程放错了地方。
            foreach (var (name, directory) in VisionLayout.Projects)
            {
                var csproj = VisionLayout.ProjectFile(VisionDirectory, name);
                Assert.True(
                    File.Exists(csproj),
                    $"登记了 {name} → {directory}/，但 {csproj} 不存在。");
            }

            // ② 磁盘实际布局 == 登记布局：多一个工程、少一个工程、放进错的组目录，三者皆红。
            var actual = EnumerateVisionProjectFiles(VisionDirectory)
                .Select(file => $"{Path.GetFileNameWithoutExtension(file)} => "
                                + Path.GetRelativePath(VisionDirectory, Path.GetDirectoryName(file)!)
                                    .Replace('\\', '/'))
                .OrderBy(entry => entry, StringComparer.Ordinal)
                .ToArray();

            // 防假绿：目录都找不到时，下面的 Equal 会因为「空集 vs 11 项」而红，
            // 但那条红信息会指向「少登记」，不指向「路径写错」。这条断言把原因说清楚。
            Assert.True(
                actual.Length >= MinimumVisionProjects,
                $"Vision/ 下只扫到 {actual.Length} 个工程，少于下限 {MinimumVisionProjects} —— 扫描路径疑似失效。");

            var expected = VisionLayout.Projects
                .Select(entry => $"{entry.Name} => {entry.Directory}")
                .OrderBy(entry => entry, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected, actual);
        }

        /// <summary>递归遍历 <c>Vision/</c> 下的 csproj，跳过构建产物与 IDE 目录。</summary>
        /// <param name="root">Vision 模块根目录。</param>
        /// <returns>csproj 绝对路径序列。</returns>
        private static IEnumerable<string> EnumerateVisionProjectFiles(string root)
        {
            var skipDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "obj", "bin", ".vs", ".git", ".workbuddy-ai",
            };

            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var dir = stack.Pop();

                foreach (var sub in Directory.GetDirectories(dir))
                {
                    if (!skipDirs.Contains(Path.GetFileName(sub)))
                        stack.Push(sub);
                }

                foreach (var file in Directory.GetFiles(dir, "*.csproj"))
                {
                    // WPF 构建可能在工程目录短暂生成此临时项目；它不属于解决方案布局。
                    if (!Path.GetFileName(file).EndsWith("_wpftmp.csproj", StringComparison.OrdinalIgnoreCase))
                        yield return file;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2. Vision.sln 登记 == 骨架，且路径真实存在、名称与文件名一致
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void VisionSolution_RegistersExactlyTheSkeleton_AndPathsExist()
        {
            Assert.True(File.Exists(VisionSolution), $"解决方案不存在：{VisionSolution}");

            var entries = ReadSolutionProjects(VisionSolution);

            Assert.True(
                entries.Count >= MinimumVisionProjects,
                $"Vision.sln 只解析出 {entries.Count} 个工程，少于下限 {MinimumVisionProjects} —— 正则或路径疑似失效。");

            // 名称集合 == 骨架（多登记 / 少登记都红）
            Assert.Equal(
                ExpectedVisionProjects.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                entries.Select(entry => entry.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());

            foreach (var entry in entries)
            {
                // ① 登记路径必须真实存在 —— 改名 / 搬目录后最容易漏掉的就是这条，
                //    而 dotnet build <sln> 报的是 MSB3202，容易被当成环境问题。
                Assert.True(File.Exists(entry.AbsolutePath), $"Vision.sln 登记的工程不存在：{entry.AbsolutePath}");

                // ② 登记名必须等于 csproj 文件名 —— 否则 VS 里显示名与磁盘不一致，
                //    且 AssemblyName 会与预期不符（本轮的改名正是靠这条守住）。
                Assert.Equal(Path.GetFileNameWithoutExtension(entry.AbsolutePath), entry.Name);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 3. 迁移源已完全退场（P9 收口）
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// P9 收口：迁移源 <c>DPLibrary_Halcon_F48</c>（与 Legacy 的 <c>Vision</c> 工程）已彻底退场 ——
        /// 目录不存在、csproj 不存在、<c>Vision.sln</c> 未登记、全仓任何工程都不再引用它。
        ///
        /// 原先的「消费者清单冻结」随迁移源删除一并作废：冻结的对象已经不存在，
        /// 继续钉冻结清单只会变成一条恒假的守卫。此处改为从**四**个维度证明它回不来。
        /// </summary>
        [Fact]
        public void RetiredProjects_AreGoneFromSolutionDiskAndReferences()
        {
            // ① Legacy 的 Vision 工程（计划 §P9 先行退场项）
            Assert.False(
                Directory.Exists(Path.Combine(VisionDirectory, "Vision")),
                "Legacy 的 Vision 目录按计划 §P9 应已删除。");

            // ② 退役清单：目录与 csproj 都不在
            foreach (var retired in RetiredProjects)
            {
                var dir = Path.Combine(VisionDirectory, retired);
                Assert.False(Directory.Exists(dir), $"{retired} 目录应已删除（P9 收口）。");
                Assert.False(File.Exists(Path.Combine(dir, retired + ".csproj")), $"{retired}.csproj 应已删除（P9 收口）。");
            }

            // ③ Vision.sln 未登记（除名）
            var solution = File.ReadAllText(VisionSolution);
            foreach (var retired in RetiredProjects)
                Assert.DoesNotContain($"\"{retired}\"", solution, StringComparison.Ordinal);

            // ④ 全仓无任何工程引用（含 FlowNodes / Packing Machine / WPFControl 等模块外工程）
            var projects = EnumerateProjectFiles(RepoRoot).ToArray();
            Assert.True(projects.Length > 50, $"全仓只扫到 {projects.Length} 个 csproj，扫描疑似失效。");

            var offenders = new List<string>();
            foreach (var project in projects)
            {
                var relative = Path.GetRelativePath(RepoRoot, project).Replace('\\', '/');
                foreach (var reference in ReadProjectReferencePaths(project))
                {
                    foreach (var retired in RetiredProjects)
                    {
                        if (reference.Contains(retired, StringComparison.OrdinalIgnoreCase))
                            offenders.Add($"{relative} -> {reference}");
                    }
                }
            }

            Assert.Empty(offenders);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 4. 生产工程不得引用测试工程（计划 §9、§13 检查表）
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ProductionProjects_DoNotReferenceTestProjects()
        {
            var projects = EnumerateProjectFiles(RepoRoot).ToArray();
            Assert.True(projects.Length > 50, $"全仓只扫到 {projects.Length} 个 csproj，扫描疑似失效。");

            var offenders = new List<string>();
            var testProjectCount = 0;

            foreach (var project in projects)
            {
                var name = Path.GetFileNameWithoutExtension(project);
                var isTestProject = name.EndsWith(".Tests", StringComparison.Ordinal) ||
                                    name.EndsWith(".Test", StringComparison.Ordinal);
                if (isTestProject)
                {
                    testProjectCount++;
                    continue;
                }

                foreach (var reference in ReadProjectReferencePaths(project))
                {
                    var referenced = Path.GetFileNameWithoutExtension(reference);
                    if (referenced.EndsWith(".Tests", StringComparison.Ordinal) ||
                        referenced.EndsWith(".Test", StringComparison.Ordinal))
                        offenders.Add(
                            $"{Path.GetRelativePath(RepoRoot, project).Replace('\\', '/')} -> {referenced}");
                }
            }

            // 防假绿：连一个测试工程都没认出来 ⇒ 命名约定判断失效
            Assert.True(testProjectCount > 0, "没有识别出任何测试工程，测试工程命名约定判断疑似失效。");

            Assert.Empty(offenders);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 工具
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Vision 模块目录（<c>&lt;仓库根&gt;/Vision</c>）。</summary>
        private static string VisionDirectory => Path.Combine(RepoRoot, "Vision");

        /// <summary>Vision 模块解决方案。</summary>
        private static string VisionSolution => Path.Combine(VisionDirectory, "Vision.sln");

        /// <summary>仓库根（以 Frame.sln 为标记向上找）。</summary>
        private static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Frame.sln")))
                    dir = dir.Parent;

                return dir?.FullName
                    ?? throw new InvalidOperationException(
                        $"从 {AppContext.BaseDirectory} 向上找不到仓库根（Frame.sln）。");
            }
        }

        /// <summary>sln 里的一条工程登记。</summary>
        /// <param name="Name">登记名（VS 显示名）。</param>
        /// <param name="AbsolutePath">登记路径的绝对形式。</param>
        private readonly record struct SolutionProject(string Name, string AbsolutePath);

        /// <summary>解析 sln 里的工程登记；解决方案文件夹（<c>{2150E333-…}</c>）按路径后缀自动排除。</summary>
        /// <param name="solutionPath">sln 绝对路径。</param>
        /// <returns>工程登记序列。</returns>
        private static IReadOnlyList<SolutionProject> ReadSolutionProjects(string solutionPath)
        {
            var directory = Path.GetDirectoryName(solutionPath)!;
            var regex = new Regex(
                "^Project\\(\"\\{(?<type>[^}]+)\\}\"\\)\\s*=\\s*\"(?<name>[^\"]+)\",\\s*\"(?<path>[^\"]+)\"",
                RegexOptions.CultureInvariant);

            var result = new List<SolutionProject>();
            foreach (var line in File.ReadAllLines(solutionPath))
            {
                var match = regex.Match(line);
                if (!match.Success)
                    continue;

                var relative = match.Groups["path"].Value;
                if (!relative.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                    continue;   // 解决方案文件夹

                result.Add(new SolutionProject(
                    match.Groups["name"].Value,
                    Path.GetFullPath(Path.Combine(directory, relative))));
            }

            return result;
        }

        /// <summary>读取 csproj 里 <c>ProjectReference Include</c> 的原始值（未解析为绝对路径）。</summary>
        /// <param name="projectPath">csproj 绝对路径。</param>
        /// <returns>引用路径序列。</returns>
        private static IEnumerable<string> ReadProjectReferencePaths(string projectPath)
        {
            var regex = new Regex(
                "<ProjectReference\\s+Include=\"(?<path>[^\"]+)\"", RegexOptions.CultureInvariant);

            return regex.Matches(File.ReadAllText(projectPath))
                .Select(match => match.Groups["path"].Value);
        }

        /// <summary>遍历仓库内的 csproj（跳过构建产物、IDE 目录与工具/备份目录）。</summary>
        /// <param name="root">仓库根。</param>
        /// <returns>csproj 绝对路径序列。</returns>
        private static IEnumerable<string> EnumerateProjectFiles(string root)
        {
            var skipDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "obj", "bin", ".vs", ".git", ".codebase-memory", ".workbuddy-ai", "Dll", "NugetPackages",
            };

            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var dir = stack.Pop();

                foreach (var sub in Directory.GetDirectories(dir))
                {
                    if (!skipDirs.Contains(Path.GetFileName(sub)))
                        stack.Push(sub);
                }

                foreach (var file in Directory.GetFiles(dir, "*.csproj"))
                    yield return file;
            }
        }
    }
}
