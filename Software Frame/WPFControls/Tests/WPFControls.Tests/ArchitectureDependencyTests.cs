// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ArchitectureDependencyTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、System、System.Collections.Generic、System.IO、System.Linq、System.Text.RegularExpressions。导入不代表所有类型都会在运行时加载。
using WPFControls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace WPFControls.Tests
{
    /// <summary>
    /// 架构守卫：钉住 WPFControls 的分层方向与 csproj 的直接引用，防止已登记的架构债务继续扩大。
    ///
    /// 背景（见仓库根 <c>WPFControls-架构分析与优化方向.md</c> 3.1 / 3.7 / 3.10 / 3.11）：
    /// WPFControls 是**单个程序集**，分层越界**不会编译报错**，也没有任何守卫拦着 ⇒ 只能靠测试钉住。
    ///
    /// 约定（与 FlowEditor.Workbench.Tests/ArchitectureDependencyTests 一致）：已知债务写进断言里的字面量清单，
    /// 断言「实际集合 == 登记集合」。新增一处越界 → 测试红（这是本类存在的意义）；
    /// 偿还一处债务 → 测试同样红，提醒**同步删掉登记条目**，让清单不腐烂。
    ///
    /// ⚠️ 登记粒度是「**文件 :: 依赖**」对，不是文件 —— 否则一个已在清单里的文件再叠加一条
    /// 越界依赖不会被发现（本类第一版就是这个漏洞，靠变异验证抓出来的）。
    /// </summary>
    public sealed class ArchitectureDependencyTests
    {
        /// <summary>WPFControls 主工程目录（WPFControls.csproj 所在目录）。</summary>
        private static readonly string ProjectDirectory = FindProjectDirectory();

        /// <summary>
        /// Models 层（最底层）不得依赖 Services / ViewModels / Views（架构文档 3.11）。
        /// 正则同时匹配 <c>using</c> 与完全限定名 —— 只扫 <c>using</c> 会被完全限定名绕过。
        /// </summary>
        [Fact]
        public void Models_DoNotDependOnUpperLayers()
        {
            var actual = FindOffenders(
                Directory.EnumerateDirectories(Path.Combine(ProjectDirectory, "Features"), "Models", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(ProjectDirectory, path)).ToArray(),
                @"WPFControls\.(ProgramControl|CustomControl)\.(Services|ViewModels|Views)\b");

            Assert.Equal(
                [
                    // 2026-09-29：Display 显示链改用 HObject 直显（DPLibrary 删除），
                    // DisplayModel 不再 using Views，3.11 债务偿还，登记清零。
                ],
                actual);
        }

        /// <summary>
        /// CustomControl 名义上是「可复用层」，不得反向依赖上层业务模块 ProgramControl（架构文档 3.7）。
        /// </summary>
        [Fact]
        public void CustomControl_DoNotDependOnProgramControl()
        {
            var controlsDirectory = Path.Combine(ProjectDirectory, "Shared");
            Assert.False(File.Exists(Path.Combine(controlsDirectory, "WPFControls.Controls.csproj")));
            foreach (var file in Directory.EnumerateFiles(controlsDirectory, "*", SearchOption.AllDirectories)
                         .Where(path => path.EndsWith(".cs") || path.EndsWith(".xaml")))
                Assert.DoesNotContain("WPFControls.ProgramControl", File.ReadAllText(file), StringComparison.Ordinal);
        }

        /// <summary>
        /// csproj 必须显式声明源码真正用到的项目；SECS/GEM 只依赖 Core 契约。
        /// 此前它们只存在于传递闭包里（经 FlowNodes / Extension / DataManager），
        /// 上游一动就可能静默打断 WPFControls 的编译。本测试防止这条修复被回退。
        /// </summary>
        [Fact]
        public void ProjectFile_DeclaresTheDirectlyUsedProjects()
        {
            var project = File.ReadAllText(Path.Combine(ProjectDirectory, "WPFControls.csproj"));

            foreach (var reference in new[]
                     {
                         @"..\..\Extension\Extension\Extension.csproj",
                         @"..\..\ScriptEditor\ScriptEditor.Controls\ScriptEditor.Controls.csproj",
                         @"..\..\SecsGem\SecsGem.Core\SecsGem.Core.csproj",
                         // 2026-09-29：兼容门面 DataManager/DataManager 已删除，改为直连 Core + Runtime。
                         @"..\..\DataManager\DataManager.Core\DataManager.Core.csproj",
                         @"..\..\DataManager\DataManager.Runtime\DataManager.Runtime.csproj",
                         @"..\..\Device\Device.Core\Device.Core.csproj",
                         @"..\..\Vision\Vision.Controls\Vision.Controls.csproj",
                         @"..\..\FlowEditor\FlowEditor.Controls\FlowEditor.Controls.csproj",
                         @"..\..\WPFCustomControls\WPFCustomControls\WPFCustomControls.csproj"
                     })
            {
                Assert.Contains(reference, project, StringComparison.Ordinal);
            }

            Assert.Contains(@"..\..\SecsGem\SecsGem.Runtime\SecsGem.Runtime.csproj", project, StringComparison.Ordinal);

            // 3.10：LogR 三个目录已不存在，不应再有指向它们的 Remove 死配置
            Assert.DoesNotMatch(@"Remove=""[^""]*LogR", project);
        }

        /// <summary>
        /// 测试相关入口 FlowNodes_And_Application_Business_Use_Core_Instead_Of_Runtime_Types；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 GetParent、ReadAllText、Combine、Contains、DoesNotContain；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void FlowNodes_And_Application_Business_Use_Core_Instead_Of_Runtime_Types()
        {
            var root = Directory.GetParent(ProjectDirectory)?.Parent?.FullName
                ?? throw new DirectoryNotFoundException("无法定位仓库根目录。");
            var flowNodes = File.ReadAllText(Path.Combine(root, "FlowNodes", "FlowNodes", "FlowNodes.csproj"));
            Assert.Contains(@"..\..\SecsGem\SecsGem.Core\SecsGem.Core.csproj", flowNodes, StringComparison.Ordinal);
            Assert.DoesNotContain(@"..\..\SecsGem\SecsGem.Runtime\SecsGem.Runtime.csproj", flowNodes, StringComparison.Ordinal);

            var businessDirectory = Path.Combine(root, "..", "MachineApp", "MachineApp", "Business");
            foreach (var file in Directory.EnumerateFiles(businessDirectory, "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(file);
                Assert.DoesNotMatch(@"\bSecsGemRuntime\b", source);
                Assert.DoesNotContain("Secs4Net", source, StringComparison.Ordinal);
                Assert.DoesNotContain("SecsGem.Runtime", source, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// WPFControls 的每个直接依赖都必须真实存在，且**不得反过来引用 WPFControls**（防循环引用）。
        /// 补 ProjectReference 时最容易踩的就是这条。
        /// </summary>
        [Fact]
        public void DirectDependencies_ExistAndDoNotReferenceWpfControl()
        {
            var project = File.ReadAllText(Path.Combine(ProjectDirectory, "WPFControls.csproj"));
            var references = Regex
                .Matches(project, @"ProjectReference Include=""(?<path>[^""]+\.csproj)""")
                .Select(match => Path.GetFullPath(match.Groups["path"].Value, ProjectDirectory))
                .ToArray();

            Assert.NotEmpty(references);
            foreach (var reference in references)
            {
                Assert.True(File.Exists(reference), $"引用的工程不存在：{reference}");
                Assert.DoesNotContain(
                    "WPFControls.csproj",
                    File.ReadAllText(reference),
                    StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// WPFControls 的下游宿主 <c>MachineApp.csproj</c> 必须显式声明它实际使用的工程
        /// （架构文档 6.4）。它此前只声明 WPFControls，其余工程全靠传递闭包
        /// （WPFControls → FlowNodes）送来 ⇒ 上游一清理就会静默打断宿主的编译。
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
            var frameworkRoot = Directory.GetParent(ProjectDirectory)!.Parent!.FullName;
            var hostProject = Path.Combine(frameworkRoot, "..", "MachineApp", "MachineApp", "MachineApp.csproj");
            var document = System.Xml.Linq.XDocument.Load(hostProject);
            Assert.Equal(@"..\..\Software Frame\WPFControls\WPFControls\WPFControls.csproj",
                Assert.Single(document.Descendants("ProjectReference")).Attribute("Include")!.Value);
            Assert.DoesNotContain("MachineApp.csproj", File.ReadAllText(Path.Combine(ProjectDirectory, "WPFControls.csproj")));
            Assert.False(typeof(WPFControls.ProgramControl.Models.ISQLDataBase).IsAssignableFrom(typeof(WPFControls.ProgramControl.Models.CapacityStatisticsRow)));
        }

        /// <summary>
        /// 宿主显式组合共享节点目录和通信模块，并直接声明 FlowNodes/Communication.Controls。
        /// 如今节点来源由目录提供，不再要求宿主保留旧 Assembly.Load 字面量；
        /// 将来新增的动态加载目标仍必须声明直接引用。实际输出/deps 由集成入口验证。
        /// </summary>
        [Fact]
        public void HostProject_NodeCatalogAndRuntimeAssemblies_AreDeclared()
        {
            var frameworkRoot = Directory.GetParent(ProjectDirectory)!.Parent!.FullName;
            var host = File.ReadAllText(Path.Combine(frameworkRoot, "..", "MachineApp", "MachineApp", "App.xaml.cs"));
            var module = File.ReadAllText(Path.Combine(ProjectDirectory, "WPFControlsModule.cs"));
            var composition = File.ReadAllText(Path.Combine(ProjectDirectory, "WPFControlsComposition.cs"));
            Assert.Contains("WPFControlsComposition.ConfigureModules<SqlConfigEntity, ExcelConfigEntity>(moduleCatalog)", host);
            Assert.Contains("WPFControlsComposition.RegisterServices(containerRegistry)", module);
            Assert.Contains("CreateNodeCatalog()", composition);
            Assert.Contains("RegisterSingleton<FlowNodes.Device.ICameraProvider, ProductionFlowCameraProvider>", host);
            Assert.Contains("catalog.AddModule<Communication.Controls.CommunicationModule>()", composition);
            Assert.DoesNotContain("RequestNavigate", composition);
            Assert.DoesNotContain("Connect(", composition);
        }

        /// <summary>扫描指定目录下命中禁止模式的「文件 :: 依赖」对。</summary>
        /// <param name="relativeDirectories">相对 WPFControls 工程目录的扫描目录。</param>
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

        /// <summary>从测试输出目录向上查找 WPFControls 主工程目录。</summary>
        /// <returns>WPFControls.csproj 所在目录。</returns>
        /// <exception cref="DirectoryNotFoundException">无法定位 WPFControls.csproj 时抛出。</exception>
        private static string FindProjectDirectory()
        {
            for (var current = new DirectoryInfo(AppContext.BaseDirectory);
                 current != null;
                 current = current.Parent)
            {
                var candidate = Path.Combine(current.FullName, "WPFControls", "WPFControls.csproj");
                if (File.Exists(candidate))
                    return Path.GetDirectoryName(candidate)!;
            }

            throw new DirectoryNotFoundException("无法从测试输出目录定位 WPFControls.csproj。");
        }
    }
}
