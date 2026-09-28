using DPLibrary_Halcon_F48.Tools.AI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Vision.Tests.Invi
{
    /// <summary>
    /// P10-1 / P10-5：旧 InvI 栈退场守卫。
    /// 旧实现（InvIHelpr1 / InvIClassTool / InvISegmentationTool / Vision.InvI.InvIToolMgr）
    /// 已按 P10-2（DPLibrary_Halcon_F48）与 P10-5（Vision/Vision 历史副本）删除；
    /// 这些测试保证它不会被后续改动「引回来」。
    ///
    /// 注意：源码扫描前先剥注释——本项目允许在文档注释里写「旧 X 已退场」，
    /// 那不是引用，不该判红（核对脚本误报三类的第 ② 类）。
    /// </summary>
    public class InviRetirementTests
    {
        private static readonly string[] LegacyTypeNames =
        {
            "InvIHelpr1", "InvIClassTool", "InvISegmentationTool", "InvIToolMgr",
        };

        private static readonly string[] LegacySourceFiles =
        {
            "InvIHelpr1.cs", "InvIClassTool.cs", "InvISegmentationTool.cs", "InvIToolMgr.cs",
        };

        /// <summary>Vision/Vision（历史入口）里那份逐字相同的副本用的是 InvIHelpr 这个名字。</summary>
        private static readonly string[] LegacyHistoryFiles =
        {
            "InvIHelpr.cs", "InvIClassTool.cs", "InvISegmentationTool.cs", "InvIToolMgr.cs",
        };

        // ---------- 1. 编译产物层面：类型已退场 ----------

        [Fact]
        public void DPLibrary_Halcon_F48_NoLongerContainsLegacyInviTypes()
        {
            var names = typeof(AITool).Assembly.GetTypes()
                .Select(t => t.Name)
                .Concat(typeof(AITool).Assembly.GetTypes().Select(t => t.FullName ?? string.Empty))
                .ToArray();

            foreach (var legacy in LegacyTypeNames)
                Assert.DoesNotContain(names, n => n.Contains(legacy, StringComparison.Ordinal));
        }

        [Fact]
        public void LegacyVisionInvINamespaceIsGone()
        {
            var namespaces = typeof(AITool).Assembly.GetTypes()
                .Select(t => t.Namespace ?? string.Empty)
                .Distinct()
                .ToArray();

            Assert.DoesNotContain(namespaces, ns => ns.Equals("Vision.InvI", StringComparison.Ordinal));
        }

        // ---------- 2. 源文件层面：旧文件已物理删除 ----------

        [Fact]
        public void LegacySourceFilesAreDeleted()
        {
            var inviDir = Path.Combine(RepoRoot(), "Vision", "DPLibrary_Halcon_F48", "Tools", "AI", "Invi");

            Assert.True(File.Exists(Path.Combine(inviDir, "InviHelper.cs")), "InviHelper.cs 必须保留（现行正式实现）");
            Assert.True(File.Exists(Path.Combine(inviDir, "InviType.cs")), "InviType.cs 必须保留（现行结果模型）");

            foreach (var file in LegacySourceFiles)
                Assert.False(File.Exists(Path.Combine(inviDir, file)), $"{file} 应已删除（P10-2）");

            var historyDir = Path.Combine(RepoRoot(), "Vision", "Vision", "AI", "Invi");
            foreach (var file in LegacyHistoryFiles)
                Assert.False(File.Exists(Path.Combine(historyDir, file)), $"{file} 应已删除（P10-5）");
        }

        // ---------- 3. 正式入口唯一 ----------

        [Fact]
        public void AIToolConfigOnlyUsesInviHelper()
        {
            var source = StripComments(File.ReadAllText(
                Path.Combine(RepoRoot(), "Vision", "DPLibrary_Halcon_F48", "Tools", "AI", "AITool.cs")));

            Assert.Contains("InviHelper", source);
            foreach (var legacy in LegacyTypeNames)
                Assert.DoesNotContain(legacy, source, StringComparison.Ordinal);
        }

        // ---------- 4. 全仓源码不再引用旧类型（含 Frame.sln 项目） ----------

        [Fact]
        public void NoSourceFileReferencesLegacyInviTypes()
        {
            var offenders = new List<string>();

            foreach (var file in EnumerateSourceFiles(RepoRoot()))
            {
                var text = StripComments(File.ReadAllText(file));
                if (LegacyTypeNames.Any(legacy => text.Contains(legacy, StringComparison.Ordinal)))
                    offenders.Add(Path.GetRelativePath(RepoRoot(), file));
            }

            Assert.Empty(offenders);
        }

        // ---------- 5. 旧 InvI.dll 协议不再被引用 ----------

        [Fact]
        public void LegacyInviDllProtocolIsGone()
        {
            var offenders = new List<string>();

            foreach (var file in EnumerateSourceFiles(RepoRoot()))
            {
                var text = StripComments(File.ReadAllText(file));
                if (text.Contains("\"InvI.dll\"", StringComparison.Ordinal))
                    offenders.Add(Path.GetRelativePath(RepoRoot(), file));
            }

            Assert.Empty(offenders);

            // 现行 native DLL 名只在适配层出现
            var adapter = StripComments(File.ReadAllText(Path.Combine(
                RepoRoot(), "Vision", "DPLibrary_Halcon_F48", "Tools", "AI", "Invi", "DynamicDllInviNativeApi.cs")));
            Assert.Contains("INVI API.dll", adapter);
        }

        // ---------- 工具 ----------

        /// <summary>仓库根（以 Frame.sln 为标记向上找）。</summary>
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Frame.sln")))
                dir = dir.Parent;

            return dir?.FullName
                ?? throw new InvalidOperationException($"从 {AppContext.BaseDirectory} 向上找不到仓库根（Frame.sln）。");
        }

        /// <summary>遍历仓库内的 .cs（跳过构建产物、工具目录与本测试工程自身）。</summary>
        private static IEnumerable<string> EnumerateSourceFiles(string root)
        {
            var skipDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "obj", "bin", ".vs", ".git", ".codebase-memory", ".workbuddy-ai", "Dll", "NugetPackages",
            };
            var selfDir = Path.GetFullPath(Path.Combine(root, "Vision", "Vision.Tests"));

            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var dir = stack.Pop();

                foreach (var sub in Directory.GetDirectories(dir))
                {
                    if (skipDirs.Contains(Path.GetFileName(sub)))
                        continue;
                    if (string.Equals(Path.GetFullPath(sub), selfDir, StringComparison.OrdinalIgnoreCase))
                        continue;
                    stack.Push(sub);
                }

                foreach (var file in Directory.GetFiles(dir, "*.cs"))
                    yield return file;
            }
        }

        /// <summary>
        /// 剥掉注释（行注释 / 文档注释 / 块注释），保留字符串字面量——
        /// 字符串里的类型名可能是反射或序列化的真实引用，必须照样判红。
        /// </summary>
        private static string StripComments(string source)
        {
            var sb = new StringBuilder(source.Length);
            int i = 0;

            while (i < source.Length)
            {
                char c = source[i];

                if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    while (i < source.Length && source[i] != '\n')
                        i++;
                    continue;
                }

                if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/'))
                        i++;
                    i = Math.Min(i + 2, source.Length);
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    sb.Append(c);
                    i++;
                    while (i < source.Length)
                    {
                        sb.Append(source[i]);
                        if (source[i] == '\\' && i + 1 < source.Length)
                        {
                            sb.Append(source[i + 1]);
                            i += 2;
                            continue;
                        }
                        if (source[i] == quote)
                        {
                            i++;
                            break;
                        }
                        i++;
                    }
                    continue;
                }

                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }
    }
}
