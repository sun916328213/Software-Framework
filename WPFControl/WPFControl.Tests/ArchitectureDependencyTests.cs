using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace WPFControl.Tests
{
    /// <summary>
    /// 架构守卫：钉住 WPFControl 的分层方向与 csproj 的直接引用，防止已登记的架构债务继续扩大。
    ///
    /// 背景（见仓库根 <c>WPFControl-架构分析与优化方向.md</c> 3.1 / 3.7 / 3.10 / 3.11）：
    /// WPFControl 是**单个程序集**，分层越界**不会编译报错**，也没有任何守卫拦着 ⇒ 只能靠测试钉住。
    ///
    /// 约定（与 FlowEditor.Test/ArchitectureDependencyTests 一致）：已知债务写进断言里的字面量清单，
    /// 断言「实际集合 == 登记集合」。新增一处越界 → 测试红（这是本类存在的意义）；
    /// 偿还一处债务 → 测试同样红，提醒**同步删掉登记条目**，让清单不腐烂。
    ///
    /// ⚠️ 登记粒度是「**文件 :: 依赖**」对，不是文件 —— 否则一个已在清单里的文件再叠加一条
    /// 越界依赖不会被发现（本类第一版就是这个漏洞，靠变异验证抓出来的）。
    /// </summary>
    public sealed class ArchitectureDependencyTests
    {
        /// <summary>WPFControl 主工程目录（WPFControl.csproj 所在目录）。</summary>
        private static readonly string ProjectDirectory = FindProjectDirectory();

        /// <summary>
        /// Models 层（最底层）不得依赖 Services / ViewModels / Views（架构文档 3.11）。
        /// 正则同时匹配 <c>using</c> 与完全限定名 —— 只扫 <c>using</c> 会被完全限定名绕过。
        /// </summary>
        [Fact]
        public void Models_DoNotDependOnUpperLayers()
        {
            var actual = FindOffenders(
                ["ProgramControl/Models", "CustomControl/Models"],
                @"WPFControl\.(ProgramControl|CustomControl)\.(Services|ViewModels|Views)\b");

            Assert.Equal(
                [
                    // 3.11：Model 直接 using 了 View，依赖方向整个倒过来（尚未偿还）
                    "ProgramControl/Models/Shell/DisplayModel.cs :: WPFControl.CustomControl.Views"
                ],
                actual);
        }

        /// <summary>
        /// CustomControl 名义上是「可复用层」，不得反向依赖上层业务模块 ProgramControl（架构文档 3.7）。
        /// </summary>
        [Fact]
        public void CustomControl_DoNotDependOnProgramControl()
        {
            var actual = FindOffenders(
                ["CustomControl"],
                @"WPFControl\.ProgramControl(\.\w+)*");

            Assert.Equal(
                [
                    // 3.7：二者都只 using 了 ...ProgramControl.Models（尚未偿还）
                    "CustomControl/Models/PrintModel.cs :: WPFControl.ProgramControl.Models",
                    "CustomControl/Views/User/ModifyUserView.xaml.cs :: WPFControl.ProgramControl.Models"
                ],
                actual);
        }

        /// <summary>
        /// csproj 必须显式声明源码真正用到的 8 个工程（架构文档 3.1 / 6.6）。
        /// 此前它们只存在于传递闭包里（经 FlowNodes / Extension / DataManager），
        /// 上游一动就可能静默打断 WPFControl 的编译。本测试防止这条修复被回退。
        /// </summary>
        [Fact]
        public void ProjectFile_DeclaresTheDirectlyUsedProjects()
        {
            var project = File.ReadAllText(Path.Combine(ProjectDirectory, "WPFControl.csproj"));

            foreach (var reference in new[]
                     {
                         @"..\..\Extension\Extension\Extension.csproj",
                         @"..\..\ScriptEditor\ScriptEditor\ScriptEditor.csproj",
                         @"..\..\Secs\Secs\Secs.csproj",
                         @"..\..\Device\Device\Device.csproj",
                         @"..\..\Vision\Ctr_WPF_F48\Ctr_WPF_F48.csproj",
                         @"..\..\Vision\DPLibrary_Halcon_F48\DPLibrary_Halcon_F48.csproj",
                         @"..\..\FlowEditor\FlowEditor\FlowEditor.csproj",
                         @"..\..\WPFCustomControls\WPFCustomControls\WPFCustomControls.csproj"
                     })
            {
                Assert.Contains(reference, project, StringComparison.Ordinal);
            }

            // 3.10：LogR 三个目录已不存在，不应再有指向它们的 Remove 死配置
            Assert.DoesNotMatch(@"Remove=""[^""]*LogR", project);
        }

        /// <summary>
        /// WPFControl 的每个直接依赖都必须真实存在，且**不得反过来引用 WPFControl**（防循环引用）。
        /// 补 ProjectReference 时最容易踩的就是这条。
        /// </summary>
        [Fact]
        public void DirectDependencies_ExistAndDoNotReferenceWpfControl()
        {
            var project = File.ReadAllText(Path.Combine(ProjectDirectory, "WPFControl.csproj"));
            var references = Regex
                .Matches(project, @"ProjectReference Include=""(?<path>[^""]+\.csproj)""")
                .Select(match => Path.GetFullPath(match.Groups["path"].Value, ProjectDirectory))
                .ToArray();

            Assert.NotEmpty(references);
            foreach (var reference in references)
            {
                Assert.True(File.Exists(reference), $"引用的工程不存在：{reference}");
                Assert.DoesNotContain(
                    "WPFControl.csproj",
                    File.ReadAllText(reference),
                    StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// WPFControl 的下游宿主 <c>Packing Machine.csproj</c> 必须显式声明它实际使用的 11 个工程
        /// （架构文档 6.4）。它此前只声明 WPFControl，其余 11 个全靠传递闭包
        /// （WPFControl → FlowNodes）送来 ⇒ 上游一清理就会静默打断宿主的编译。
        /// 本测试防止这条修复被回退。
        /// ⚠️ 2026-09-23：`..\..\Communication\Communication\Communication.csproj` 登记条目**已删除** ——
        /// 该旧通信工程已按《Software-Framework_Communication_Device重构删除实施说明》整体删除，
        /// 宿主也不再使用其类型（PLC 调用点改为显式 `NotSupportedException`），
        /// 故按本类约定「偿还一处债务即同步删登记条目」收敛清单。
        /// ⚠️ 2026-09-23 追加 <c>FlowNodes</c>：它**不是编译依赖，而是运行时依赖** ——
        /// <c>App.xaml.cs</c> 里有 <c>Assembly.Load("FlowNodes")</c>，只要求 FlowNodes.dll
        /// 出现在输出目录 / deps.json 里，编译期完全看不见 ⇒ 特别容易被当成"冗余"清掉。
        /// 实测：摘掉上游那条边后宿主**编译仍 0 错误**，但输出目录里 FlowNodes.dll 消失、
        /// deps.json 的 201 个库里也没有它 ⇒ 那行 Assembly.Load 会在运行时抛 FileNotFoundException。
        /// </summary>
        [Fact]
        public void HostProject_DeclaresItsDirectlyUsedProjects()
        {
            var solutionDirectory = Directory.GetParent(ProjectDirectory)?.Parent?.FullName
                ?? throw new DirectoryNotFoundException("无法定位仓库根目录。");
            var hostProject = Path.Combine(
                solutionDirectory,
                "Packing Machine",
                "Packing Machine",
                "Packing Machine.csproj");
            Assert.True(File.Exists(hostProject), $"宿主工程不存在：{hostProject}");

            var project = File.ReadAllText(hostProject);
            var hostDirectory = Path.GetDirectoryName(hostProject)!;
            var references = new[]
            {
                @"..\..\Extension\Extension\Extension.csproj",
                @"..\..\DataManager\DataManager\DataManager.csproj",
                @"..\..\DataManager\DataManager.Controls\DataManager.Controls.csproj",
                @"..\..\Device\Device\Device.csproj",
                @"..\..\Secs\Secs\Secs.csproj",
                @"..\..\ScriptEditor\ScriptEditor\ScriptEditor.csproj",
                @"..\..\FlowEditor\FlowEditor\FlowEditor.csproj",
                @"..\..\Vision\DPLibrary_Base_F48\DPLibrary_Base_F48.csproj",
                @"..\..\Vision\DPLibrary_Halcon_F48\DPLibrary_Halcon_F48.csproj",
                @"..\..\WPFCustomControls\WPFCustomControls\WPFCustomControls.csproj",
                @"..\..\FlowNodes\FlowNodes\FlowNodes.csproj"
            };

            foreach (var reference in references)
                Assert.Contains(reference, project, StringComparison.Ordinal);

            foreach (var reference in references)
            {
                var path = Path.GetFullPath(reference, hostDirectory);
                Assert.True(File.Exists(path), $"引用的工程不存在：{path}");
                Assert.DoesNotContain(
                    "Packing Machine.csproj",
                    File.ReadAllText(path),
                    StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// 宿主工程里 <c>Assembly.Load("X")</c> 的目标程序集必须**显式声明** ProjectReference。
        /// 这是**运行时契约**：编译期完全看不见，只有 X.dll 真出现在输出目录 / deps.json 里
        /// 才能成功。靠上游传递（借来的依赖）会在上游清理时静默失效 ——
        /// 而且失效点只在**运行时**暴露（编译仍 0 错误），最难排查。
        /// </summary>
        [Fact]
        public void HostProject_RuntimeLoadedAssemblies_AreDeclared()
        {
            var solutionDirectory = Directory.GetParent(ProjectDirectory)?.Parent?.FullName
                ?? throw new DirectoryNotFoundException("无法定位仓库根目录。");
            var hostDirectory = Path.Combine(
                solutionDirectory,
                "Packing Machine",
                "Packing Machine");
            var hostProject = Path.Combine(hostDirectory, "Packing Machine.csproj");
            var hostSource = Path.Combine(hostDirectory, "App.xaml.cs");
            Assert.True(File.Exists(hostProject), $"宿主工程不存在：{hostProject}");
            Assert.True(File.Exists(hostSource), $"宿主入口不存在：{hostSource}");

            // 只取字面量名字；排除 "X.dll" 形式（那种按路径加载，走的是另一条契约）
            var loaded = new Regex(
                    @"Assembly\.Load(?:From|File)?\s*\(\s*""(?<name>[^""]+)""",
                    RegexOptions.CultureInvariant)
                .Matches(File.ReadAllText(hostSource))
                .Select(match => match.Groups["name"].Value)
                .Where(name => !name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            // 扫不到 = 正则失效 ⇒ 直接红，避免"没有断言"式的假绿
            Assert.NotEmpty(loaded);

            var project = File.ReadAllText(hostProject);
            foreach (var name in loaded)
                Assert.Contains($@"{name}.csproj", project, StringComparison.Ordinal);
        }

        /// <summary>扫描指定目录下命中禁止模式的「文件 :: 依赖」对。</summary>
        /// <param name="relativeDirectories">相对 WPFControl 工程目录的扫描目录。</param>
        /// <param name="pattern">用于识别越界依赖的正则（同时覆盖 using 与完全限定名）。</param>
        /// <returns>按序排序、使用正斜杠的「项目相对路径 :: 命中文本」条目。</returns>
        private static string[] FindOffenders(
            IReadOnlyList<string> relativeDirectories,
            string pattern)
        {
            var regex = new Regex(pattern, RegexOptions.CultureInvariant);
            return relativeDirectories
                .SelectMany(relative => Directory.EnumerateFiles(
                    Path.Combine(ProjectDirectory, relative),
                    "*.cs",
                    SearchOption.AllDirectories))
                .Where(path => !IsGeneratedPath(path))
                .SelectMany(path => regex
                    .Matches(File.ReadAllText(path))
                    .Select(match => match.Value)
                    .Distinct(StringComparer.Ordinal)
                    .Select(match =>
                        $"{Path.GetRelativePath(ProjectDirectory, path).Replace('\\', '/')} :: {match}"))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(entry => entry, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>判断文件是否位于编译生成目录。</summary>
        /// <param name="path">待检查的绝对文件路径。</param>
        /// <returns>位于 bin 或 obj 目录时返回 true。</returns>
        private static bool IsGeneratedPath(string path)
        {
            var relative = Path.GetRelativePath(ProjectDirectory, path).Replace('\\', '/');
            return relative.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) ||
                   relative.StartsWith("obj/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>从测试输出目录向上查找 WPFControl 主工程目录。</summary>
        /// <returns>WPFControl.csproj 所在目录。</returns>
        /// <exception cref="DirectoryNotFoundException">无法定位 WPFControl.csproj 时抛出。</exception>
        private static string FindProjectDirectory()
        {
            for (var current = new DirectoryInfo(AppContext.BaseDirectory);
                 current != null;
                 current = current.Parent)
            {
                var candidate = Path.Combine(current.FullName, "WPFControl", "WPFControl.csproj");
                if (File.Exists(candidate))
                    return Path.GetDirectoryName(candidate)!;
            }

            throw new DirectoryNotFoundException("无法从测试输出目录定位 WPFControl.csproj。");
        }
    }
}
