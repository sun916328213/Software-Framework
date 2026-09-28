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
    /// <c>Vision.Tests/Invi/InviRetirementTests</c> 一致）：已知集合写进断言里的字面量清单，
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
        /// 最后两项是**迁移源**，P1 明确不删（计划 §P1）：
        ///   · <c>DPLibrary_Halcon_F48</c> —— 「暂不一次性删除，作为迁移源逐步拆出」；
        ///   · <c>Vision</c>             —— §4 命名评审结论「迁移后退场」，P1 只标记 Legacy。
        /// </summary>
        private static readonly string[] ExpectedVisionProjects =
        [
            "Vision.Core",            // ← DPLibrary_Base_F48（P1 改名）
            "Vision.Tools.Halcon",    // ← P1 新建空壳
            "Vision.Tools.OpenCv",    // ← P1 新建空壳
            "Vision.Tools.AI",        // ← P1 新建空壳
            "Vision.AI.Halcon",       // ← P1 新建空壳
            "Vision.AI.Aiditron",     // ← P1 新建空壳
            "Vision.AI.Invi",         // ← P1 新建空壳
            "Vision.Controls",        // ← Ctr_WPF_F48（P1 改名 Vision.Controls.Wpf；P2 按 v1.2 §4 再收口为 Vision.Controls）
            "Vision.Workbench",       // ← VisionDemo（P1 改名）
            "Vision.Tests",           // 保留
            "DPLibrary_Halcon_F48",   // 迁移源（P1 不删）
            "Vision",                 // Legacy（P1 不删）
        ];

        /// <summary>旧项目：只允许作为迁移来源，不得被新目标工程引用（计划 §P1 / §13 检查表）。</summary>
        private static readonly string[] LegacyProjects =
        [
            "DPLibrary_Halcon_F48",
            "Vision",
        ];

        /// <summary>
        /// P1 的**新目标工程**（新建的 6 个空壳 + 新公共层 Core）。
        /// ⛔ 刻意不含 <c>Vision.Controls</c> / <c>Vision.Workbench</c> ——
        /// 它们是被改名的既有工程，本来就带着指向迁移源的历史引用（见
        /// <see cref="MigrationSource_ConsumerSetIsFrozen"/> 的冻结清单），
        /// 那不是 P1 引入的越界。
        /// </summary>
        private static readonly string[] NewTargetProjects =
        [
            "Vision.Core",
            "Vision.Tools.Halcon",
            "Vision.Tools.OpenCv",
            "Vision.Tools.AI",
            "Vision.AI.Halcon",
            "Vision.AI.Aiditron",
            "Vision.AI.Invi",
        ];

        /// <summary>
        /// 迁移源 <c>DPLibrary_Halcon_F48</c> 的消费者清单 —— **冻结**。
        /// 新增一个消费者 ⇒ 红（「只作为迁移源」的边界被突破）；
        /// 迁走一个 ⇒ 红（提醒同步删登记条目，清单不腐烂）。
        /// </summary>
        private static readonly string[] MigrationSourceConsumers =
        [
            "FlowNodes/FlowNodes/FlowNodes.csproj",
            "Packing Machine/Packing Machine/Packing Machine.csproj",
            "Vision/Vision.Controls/Vision.Controls.csproj",
            "Vision/Vision.Tests/Vision.Tests.csproj",
            "WPFControl/WPFControl/WPFControl.csproj",
        ];

        /// <summary>P1 骨架的最小规模，用作「扫描没失效」的下限。</summary>
        private const int MinimumVisionProjects = 12;

        // ─────────────────────────────────────────────────────────────────────
        // 1. Vision/ 目录下的工程集合 == P1 目标骨架
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void VisionDirectory_Projects_MatchExpectedSkeleton()
        {
            var actual = Directory
                .EnumerateDirectories(VisionDirectory)
                .Where(dir => File.Exists(Path.Combine(dir, Path.GetFileName(dir) + ".csproj")))
                .Select(dir => Path.GetFileName(dir))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            // 防假绿：目录都找不到时，下面的 Equal 会因为「空集 vs 12 项」而红，
            // 但那条红信息会指向「少登记」，不指向「路径写错」。这条断言把原因说清楚。
            Assert.True(
                actual.Length >= MinimumVisionProjects,
                $"Vision/ 下只扫到 {actual.Length} 个工程目录，少于下限 {MinimumVisionProjects} —— 扫描路径疑似失效。");

            Assert.Equal(
                ExpectedVisionProjects.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                actual);
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
        // 3. 旧项目只作为迁移源
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 迁移源与 Legacy 工程**必须还在**（P1 明确不删），且消费者清单被冻结。
        /// </summary>
        [Fact]
        public void MigrationSource_ConsumerSetIsFrozen()
        {
            var migrationSource = Path.Combine(
                VisionDirectory, "DPLibrary_Halcon_F48", "DPLibrary_Halcon_F48.csproj");
            Assert.True(File.Exists(migrationSource), $"迁移源必须保留（计划 §P1）：{migrationSource}");

            var legacy = Path.Combine(VisionDirectory, "Vision", "Vision.csproj");
            Assert.True(File.Exists(legacy), $"Legacy 工程 P1 只标记不删（计划 §P1）：{legacy}");

            var actual = EnumerateProjectFiles(RepoRoot)
                .Where(path => !string.Equals(
                    Path.GetFullPath(path), Path.GetFullPath(migrationSource), StringComparison.OrdinalIgnoreCase))
                .Where(path => ReferencesProject(path, migrationSource))
                .Select(path => Path.GetRelativePath(RepoRoot, path).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(
                MigrationSourceConsumers.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
                actual);
        }

        /// <summary>
        /// 新建的目标工程不得引用任何旧项目；Legacy 的 <c>Vision</c> 工程不得被**任何**工程引用。
        /// </summary>
        [Fact]
        public void NewTargetProjects_DoNotReferenceLegacyProjects()
        {
            var legacyPaths = LegacyProjects
                .Select(name => Path.Combine(VisionDirectory, name, name + ".csproj"))
                .ToArray();

            var offenders = new List<string>();

            foreach (var project in EnumerateProjectFiles(RepoRoot))
            {
                var relative = Path.GetRelativePath(RepoRoot, project).Replace('\\', '/');
                var projectName = Path.GetFileNameWithoutExtension(project);

                foreach (var legacyPath in legacyPaths)
                {
                    if (!ReferencesProject(project, legacyPath))
                        continue;

                    // ① 新目标工程引旧项目 ⇒ 越界（计划 §P1 / §13）
                    if (NewTargetProjects.Contains(projectName, StringComparer.Ordinal))
                        offenders.Add($"{relative} -> {Path.GetFileName(legacyPath)}（新目标工程不得引用旧项目）");

                    // ② 任何工程引 Legacy 的 Vision 工程 ⇒ 越界（它要退场）
                    if (string.Equals(
                            Path.GetFileNameWithoutExtension(legacyPath), "Vision", StringComparison.Ordinal))
                        offenders.Add($"{relative} -> Vision.csproj（Legacy 工程不得被引用）");
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

        /// <summary>判断某个 csproj 是否引用了目标工程（按绝对路径比较，避免相对路径写法差异）。</summary>
        /// <param name="projectPath">被检查的 csproj。</param>
        /// <param name="targetProjectPath">目标工程 csproj。</param>
        /// <returns>引用时返回 true。</returns>
        private static bool ReferencesProject(string projectPath, string targetProjectPath)
        {
            var directory = Path.GetDirectoryName(projectPath)!;
            var target = Path.GetFullPath(targetProjectPath);

            return ReadProjectReferencePaths(projectPath).Any(reference =>
                string.Equals(
                    Path.GetFullPath(Path.Combine(directory, reference)),
                    target,
                    StringComparison.OrdinalIgnoreCase));
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
