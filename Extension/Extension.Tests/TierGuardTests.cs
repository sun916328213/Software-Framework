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
    /// 分级守卫：把「目录 → 允许的依赖」这条准入线变成会红的测试。
    /// </summary>
    /// <remarks>
    /// 分级依据是**逐文件实测**（解析每个文件的真实 <c>using</c>），规则与数据见
    /// <c>Extension/FILE-TIERING.md</c> §1–§3。
    ///
    /// 两条互相正交的规则，分别由两组用例守住：
    /// <list type="number">
    /// <item><b>准入线</b>（<see cref="Allowed"/>）——目录声明它能用哪些外部依赖，
    /// 未登记的目录一律只允许 BCL。这是"这个文件能不能放进来"。</item>
    /// <item><b>依赖方向</b>（<see cref="TierOf"/>）——低层目录不得引用高层目录。
    /// 这是"它该放哪一格"。</item>
    /// </list>
    ///
    /// 本测试**不新建工程**，落在已有的 <c>Extension.Tests</c> 里。
    /// </remarks>
    public class TierGuardTests
    {
        /// <summary>
        /// 目录（相对 <c>Extension/Extension/</c>，正斜杠）→ 该目录允许出现的非 BCL 命名空间**前缀**。
        /// 未登记的目录 = 只允许 BCL。
        /// </summary>
        /// <remarks>
        /// 值必须逐字取自 <see cref="GuardedPrefixes"/>（由 <see cref="允许清单本身必须是良构的_目录存在且登记项都真的会被检查"/>
        /// 强制），否则等于登记了一个永远不会被检查的条目。
        /// </remarks>
        private static readonly Dictionary<string, string[]> Allowed =
            new Dictionary<string, string[]>
            {
                ["UI/Controls"] = new[] { "System.Windows", "Microsoft.Win32" },
                ["UI/Theme"] = new[] { "MaterialDesignThemes", "MaterialDesignColors", "System.Windows" },
                ["Imaging/Halcon"] = new[] { "HalconDotNet", "OpenCvSharp", "System.Drawing", "System.Windows" },
                ["Imaging/OpenCv"] = new[] { "HalconDotNet", "OpenCvSharp" },
                ["Integration/Cloning"] = new[] { "FastDeepCloner" },
                ["Integration/Mapster"] = new[] { "Mapster" },
                ["Integration/MiniExcel"] = new[] { "MiniExcelLibs" },
                ["Integration/Prism"] = new[] { "Prism", "System.Windows" },
            };
        /// <summary>
        /// 受监管的命名空间**前缀**。出现在 <c>using</c> 里且不被本目录 <see cref="Allowed"/> 覆盖即失败。
        /// </summary>
        /// <remarks>
        /// ⚠️ 必须做**前缀**匹配，不能取 <c>ns.Split('.')[0]</c> 比根段 —— 后者会让
        /// <c>System.Windows</c> / <c>System.Drawing</c> / <c>Microsoft.Win32</c> 永远匹配不上
        /// （它们的根段是 <c>System</c> / <c>Microsoft</c>），检查静默失效。
        /// <see cref="匹配器不是死的_清空允许清单后必须报出违规"/> 专门守这一点。
        ///
        /// 注：<c>System.Windows</c> 前缀同时覆盖 <c>System.Windows.Forms</c>（WinForms）。
        /// 实测 Extension 树内 WinForms 使用为 0；若将来要单独禁用 WinForms，需在此加更长的前缀。
        /// </remarks>
        private static readonly string[] GuardedPrefixes =
        {
            // T3 vendor SDK
            "HalconDotNet", "OpenCvSharp", "Cognex",
            // T1 UI / 宿主
            "System.Windows", "System.Drawing", "Microsoft.Win32",
            // T2 第三方包
            "MaterialDesignThemes", "MaterialDesignColors",
            "Prism", "Mapster", "MiniExcelLibs", "FastDeepCloner",
        };

        /// <summary>
        /// 名字**看起来**像宿主依赖、实际属于 BCL 的命名空间。逐条给理由，不接受的不要往里加。
        /// </summary>
        /// <remarks>
        /// 没有这张表就只能靠"给目录登记一个其实不需要的依赖"来消音，
        /// 那会让准入线比真实依赖**松**——这正是 2026-09-18 之前 `Core/Platform` 的状态。
        /// </remarks>
        private static readonly string[] BclExemptions =
        {
            // SafeHandle / SafeHandleZeroOrMinusOneIsInvalid / SafeWaitHandle 定义在 System.Runtime.dll，
            // 是 BCL；只有 `Microsoft.Win32.OpenFileDialog` / `SaveFileDialog` 那一族才需要宿主程序集。
            // 两者前缀相同，必须按**完整命名空间**区分。
            "Microsoft.Win32.SafeHandles",
        };

        /// <summary>
        /// 依赖方向：目录（或其父目录）的层级。**低层不得引用高层**。
        /// </summary>
        /// <remarks>
        /// 层级按实测的**外部依赖级别**定：T0 只用 BCL，T1 加 UI/宿主，T2 加第三方包，T3 加 vendor SDK。
        /// <para>
        /// ⛔ 2026-09-18 机械复核修正（FILE-TIERING.md §6.L，序 12i）：原注释写「实测内部引用只有 7 条边
        /// 且全部向下（见 §6.I）」，**两处都错** —— ① 实测是 **4** 条边（向下 3 + 同层 1），
        /// 不是 7；② §6.I 讲的是「C# 名称解析三条隐式通路」，与本表无关。
        /// </para>
        /// <para>
        /// ⚠️ **本表只能保证"层级号单调"，不保证"只有 N 条边"也不保证"全部向下"**：
        /// 4 个 T0 目录（<c>Contracts</c>/<c>Core</c>/<c>Patterns</c>/<c>Distribution</c>）之间
        /// **任意方向、任意数量**的引用都是绿的（只比 <c>toTier &gt; fromTier</c>）。
        /// 那类边的数量必须靠脚本重推：<c>.workbuddy-ai/tools/audit_dependency_edges.py</c>。
        /// </para>
        /// </remarks>
        private static readonly Dictionary<string, int> TierOf =
            new Dictionary<string, int>
            {
                ["Core"] = 0,
                ["Automation"] = 0,
                ["IO"] = 1,
                ["Platform"] = 1,
                ["Patterns"] = 1,
                ["UI"] = 1,
                ["Security"] = 1,
                ["Imaging"] = 2,
                ["Integration"] = 3,
            };
        /// <summary>实测基线：80 个 .cs。用于防止「路径解析错 → 扫到 0 个文件 → 假绿」。</summary>
        private const int ExpectedFileCountFloor = 80;

        private static readonly Regex UsingRegex =
            new Regex(@"^\s*using\s+(?:static\s+)?([\w\.]+)\s*;", RegexOptions.Multiline);

        private static readonly Regex NamespaceRegex =
            new Regex(@"^\s*namespace\s+([\w\.]+)\s*[;{]", RegexOptions.Multiline);

        /// <summary>
        /// 受监管命名空间的**出现**（不是"行首 using"）。前缀表必须与 <see cref="GuardedPrefixes"/> 一致，
        /// 由 <see cref="扫描器的前缀表必须与受监管前缀表一致"/> 强制。
        /// </summary>
        /// <remarks>
        /// 匹配到的是**完整点分序列**（如 <c>System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke</c>），
        /// 这样调用方用前缀匹配即可，不需要先截断。
        /// </remarks>
        private static readonly Regex ReferenceRegex = new Regex(
            @"\b(HalconDotNet|OpenCvSharp|Cognex|System\.Windows|System\.Drawing|MaterialDesignThemes|MaterialDesignColors|Prism|Mapster|MiniExcelLibs|FastDeepCloner|Microsoft\.Win32)(?:\.[A-Za-z_]\w*)*",
            RegexOptions.Compiled);

        /// <summary>
        /// 从源码文本里抽出**受监管命名空间的所有出现**（`using` 行 + 正文里的完全限定名），
        /// 跳过注释行（含 XML doc）。
        /// </summary>
        /// <remarks>
        /// ⛔ 这是 2026-09-18 修掉的**守卫盲区**：旧实现只扫 <c>^\s*using</c>，于是
        /// `Core/Platform/DelayEx.cs` 里写成完全限定名的
        /// <c>System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(...)</c> 完全看不见 ——
        /// 一个真实的 T1 依赖因此长期住在 T0 目录里，而守卫一直是绿的。
        ///
        /// 局限（已知、实测无害）：行尾注释 <c>code; // System.Windows.Foo</c> 仍会被扫到，
        /// 本仓实测 0 处；字符串字面量同理。
        /// </remarks>
        private static IEnumerable<string> ExtractReferences(string text)
        {
            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.TrimStart();
                if (line.StartsWith("//", StringComparison.Ordinal))
                    continue; // 注释 / XML doc 里的是说明文字，不是依赖

                foreach (Match m in ReferenceRegex.Matches(rawLine))
                    yield return m.Value;
            }
        }

        /// <summary>
        /// 库内命名空间的**出现**（<c>using</c> 行 + 正文里的完全限定名）。供「依赖方向」检查使用。
        /// </summary>
        /// <remarks>
        /// ⛔ 2026-09-18 机械复核补（FILE-TIERING.md §6.L，序 12i）：
        /// 方向测试此前**只遍历 <see cref="UsingRegex"/>**，于是「第三盲区」（§3.3(2)）在
        /// **准入**测试上修掉了、在**方向**测试上一直漏着 —— 一个 T0 文件在正文里写
        /// <c>Extension.Runtime.Foo.Bar()</c>，方向测试**完全看不见**。
        /// 本仓实测该盲区**存在但未触发**（0 处只在正文里的跨目录引用）。
        /// </remarks>
        private static readonly Regex InternalRefRegex = new Regex(
            @"\bExtension(?:\.[A-Za-z_]\w*)+", RegexOptions.Compiled);

        /// <summary>抽出库内命名空间的**所有出现**，跳过注释行（含 XML doc）。</summary>
        private static IEnumerable<string> ExtractInternalReferences(string text)
        {
            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.TrimStart();
                if (line.StartsWith("//", StringComparison.Ordinal))
                    continue; // 注释 / XML doc 里的是说明文字，不是依赖

                foreach (Match m in InternalRefRegex.Matches(rawLine))
                    yield return m.Value;
            }
        }

        /// <summary>
        /// 源码根目录。用 <c>[CallerFilePath]</c> 在**编译期**固化绝对路径，
        /// 不靠 <c>AppContext.BaseDirectory</c> 上跳 —— 本仓曾因上跳层数错位坑过 7 个测试。
        /// </summary>
        private static string SourceRoot([CallerFilePath] string thisFile = "")
        {
            var testsDir = Path.GetDirectoryName(thisFile);
            Assert.False(string.IsNullOrEmpty(testsDir), "无法从 CallerFilePath 解析出测试目录");
            return Path.GetFullPath(Path.Combine(testsDir!, "..", "Extension"));
        }

        private static IEnumerable<string> EnumerateSources()
        {
            var root = SourceRoot();
            Assert.True(Directory.Exists(root), $"源码目录不存在：{root}");

            return Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(p =>
                {
                    var rel = Path.GetRelativePath(root, p).Replace('\\', '/');
                    return !rel.StartsWith("bin/", StringComparison.OrdinalIgnoreCase)
                        && !rel.StartsWith("obj/", StringComparison.OrdinalIgnoreCase);
                });
        }

        private static string RelDir(string root, string file)
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            var slash = rel.LastIndexOf('/');
            return slash < 0 ? "" : rel.Substring(0, slash);
        }

        /// <summary>该命名空间是否受监管（前缀匹配，见 <see cref="GuardedPrefixes"/>；BCL 例外见 <see cref="BclExemptions"/>）。</summary>
        private static bool IsGuarded(string ns)
        {
            foreach (var x in BclExemptions)
            {
                if (ns.Equals(x, StringComparison.Ordinal)
                    || ns.StartsWith(x + ".", StringComparison.Ordinal))
                    return false; // 名字像宿主依赖，实际是 BCL
            }

            foreach (var p in GuardedPrefixes)
            {
                if (ns.Equals(p, StringComparison.Ordinal)
                    || ns.StartsWith(p + ".", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 允许清单是否放行该命名空间。**同样按前缀匹配** ——
        /// 两侧语义必须一致：登记 <c>System.Windows</c> 就该放行
        /// <c>System.Windows.Controls</c> / <c>.Media</c> / <c>.Interop</c> / <c>.Threading</c>，
        /// 否则每加一个 WPF 子命名空间都要改清单，守卫会退化成噪声源。
        /// </summary>
        private static bool AllowedBy(string[] allow, string ns)
        {
            foreach (var a in allow)
            {
                if (ns.Equals(a, StringComparison.Ordinal)
                    || ns.StartsWith(a + ".", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 取该目录生效的允许清单：**最长前缀匹配**（子目录规则比父目录规则更具体）。
        /// </summary>
        private static string[] AllowListFor(IReadOnlyDictionary<string, string[]> allowed, string relDir)
        {
            var best = "";
            string[] list = null;
            foreach (var kv in allowed)
            {
                if (relDir.Equals(kv.Key, StringComparison.OrdinalIgnoreCase)
                    || relDir.StartsWith(kv.Key + "/", StringComparison.OrdinalIgnoreCase))
                {
                    if (kv.Key.Length > best.Length)
                    {
                        best = kv.Key;
                        list = kv.Value;
                    }
                }
            }
            return list ?? Array.Empty<string>();
        }

        /// <summary>取该目录的层级：**最长前缀匹配**，未登记则视为 T0。</summary>
        private static int TierFor(string relDir)
        {
            var best = -1;
            var tier = 0;
            foreach (var kv in TierOf)
            {
                if (relDir.Equals(kv.Key, StringComparison.OrdinalIgnoreCase)
                    || relDir.StartsWith(kv.Key + "/", StringComparison.OrdinalIgnoreCase))
                {
                    if (kv.Key.Length > best)
                    {
                        best = kv.Key.Length;
                        tier = kv.Value;
                    }
                }
            }
            return tier;
        }

        /// <summary>按给定允许清单找出准入线违规。抽出来是为了让"匹配器是否真的生效"可被单独验证。</summary>
        private static List<string> FindAdmissionViolations(IReadOnlyDictionary<string, string[]> allowed)
        {
            var root = SourceRoot();
            var violations = new List<string>();

            foreach (var file in EnumerateSources())
            {
                var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                var relDir = RelDir(root, file);
                var allow = AllowListFor(allowed, relDir);
                var text = File.ReadAllText(file);

                foreach (var ns in ExtractReferences(text))
                {
                    if (!IsGuarded(ns))
                        continue; // BCL，放行

                    if (AllowedBy(allow, ns))
                        continue; // 本目录已登记

                    violations.Add($"{rel}  →  {ns}   （目录 {relDir} 未登记）");
                }
            }

            return violations;
        }

        [Fact]
        public void 守卫本身是有效的_不能因为扫到0个文件而假绿()
        {
            var files = EnumerateSources().ToList();
            Assert.True(
                files.Count >= ExpectedFileCountFloor,
                $"只扫到 {files.Count} 个 .cs（基线 ≥{ExpectedFileCountFloor}）—— " +
                $"路径解析可能出错，此时本守卫会「假绿」，必须先修守卫本身。root={SourceRoot()}");
        }

        [Fact]
        public void 匹配器不是死的_清空允许清单后必须报出违规()
        {
            // 这是「守卫会不会假绿」的第二道保险。
            // 若 GuardedPrefixes 退回成 ns.Split('.')[0] 的根段比较，
            // System.Windows / Microsoft.Win32 / System.Drawing 会永远匹配不上，
            // 把允许清单清空也不会报错 —— 本用例会立刻变红。
            var violations = FindAdmissionViolations(new Dictionary<string, string[]>());
            Assert.True(
                violations.Count >= 10,
                $"清空允许清单后只报出 {violations.Count} 处违规（基线 ≥10）—— " +
                "匹配器疑似失效（根段比较 / 清单被清空 / 扫描不到文件）。");
        }

        [Fact]
        public void 扫描器的前缀表必须与受监管前缀表一致()
        {
            // ReferenceRegex 里的前缀是**手写**的，很容易和 GuardedPrefixes 走散。
            // 少一个前缀 = 该依赖永远不会被检查（静默失效），本用例会立刻变红。
            foreach (var p in GuardedPrefixes)
            {
                var refs = ExtractReferences(p + ".Probe.Type").ToList();
                Assert.True(
                    refs.Any(r => r.StartsWith(p, StringComparison.Ordinal)),
                    $"扫描器认不出前缀 `{p}` —— ReferenceRegex 与 GuardedPrefixes 走散了。");
            }
        }

        [Fact]
        public void 完全限定名写法也必须被扫到_否则守卫有盲区()
        {
            // 旧实现只扫 `^\s*using`，下面这行**正文里的完全限定名**就完全看不见。
            // 本用例把"能看见"钉住，防止有人把扫描器改回 using-only。
            const string inlineFqn = "var d = System.Windows.Threading.Dispatcher.CurrentDispatcher;";
            var inlineRefs = ExtractReferences(inlineFqn).ToList();
            Assert.True(
                inlineRefs.Any(r => r.StartsWith("System.Windows.Threading.Dispatcher", StringComparison.Ordinal)),
                "扫描器没认出正文里的完全限定名 —— 这类依赖会被漏掉。实际扫到：" + string.Join(" | ", inlineRefs));

            // using 行走同一条正则、不锚定行首 ⇒ 顺带免疫 BOM 首行（本仓多数文件带 BOM）。
            Assert.Contains("System.Windows.Threading", ExtractReferences("using System.Windows.Threading;"));

            // 注释 / XML doc 里的是说明文字，不算依赖。
            Assert.Empty(ExtractReferences("// System.Windows.Threading.Dispatcher"));
            Assert.Empty(ExtractReferences("        /// <c>System.Drawing</c>"));
        }

        [Fact]
        public void 真实代码里确实存在完全限定名引用_这个盲区不是纸上假设()
        {
            // 钉住事实：`Ui/Controls/DelayEx.cs` 的宿主依赖**只出现在正文里**，
            // 它的 using 块里根本没有 System.Windows —— 所以只扫 using 的守卫对这个文件是瞎的。
            var root = SourceRoot();
            var delayEx = Path.Combine(root, "UI", "Controls", "DelayEx.cs");
            Assert.True(File.Exists(delayEx), $"文件不存在：{delayEx}（分级重排后应在此，见 FILE-TIERING.md §2）");

            var text = File.ReadAllText(delayEx);
            var refs = ExtractReferences(text).ToList();

            Assert.True(
                refs.Any(r => r.StartsWith("System.Windows.Threading.Dispatcher", StringComparison.Ordinal)),
                "DelayEx.cs 里没扫到 WPF 依赖。实际扫到：" + string.Join(" | ", refs));

            Assert.DoesNotContain(
                UsingRegex.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value),
                ns => ns.StartsWith("System.Windows", StringComparison.Ordinal));
        }

        [Fact]
        public void 每个文件都必须有namespace_不得落在全局命名空间()
        {
            var root = SourceRoot();
            var bad = new List<string>();

            foreach (var file in EnumerateSources())
            {
                var text = File.ReadAllText(file);
                if (!NamespaceRegex.IsMatch(text))
                    bad.Add(Path.GetRelativePath(root, file).Replace('\\', '/'));
            }

            Assert.True(bad.Count == 0,
                "以下文件没有 namespace（会污染全局命名空间）：\n  " + string.Join("\n  ", bad));
        }

        [Fact]
        public void namespace必须与目录一致_目录即命名空间()
        {
            // ⛔ 这条用例是 2026-09-18 补的，因为发现了一个**真实缺口**：
            // §3 声称"目录 ↔ 命名空间 1:1"，但此前**没有任何用例守它** ——
            // 于是重排时漏改了一个文件的 namespace（`Core/Platform/DelayEx.cs` 留在了
            // 旧的 `Extension.Core.SystemEx`），而下游 `using` 也一起没改 ⇒ 两边一致 ⇒
            // **编译通过、守卫全绿，谁都没发现**；直到把该文件移走时才在 `git diff` 里暴露。
            // ⇒ 把"目录 = 命名空间"变成会红的事实。
            //
            var root = SourceRoot();
            var bad = new List<string>();

            foreach (var file in EnumerateSources())
            {
                var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                var dir = RelDir(root, file);
                var expected = dir.Length == 0 ? "Extension" : "Extension." + dir.Replace('/', '.');

                var text = File.ReadAllText(file);
                var m = NamespaceRegex.Match(text);
                if (!m.Success)
                {
                    bad.Add($"{rel}  →  没有 namespace（期望 {expected}）");
                    continue;
                }

                var actual = m.Groups[1].Value;
                if (!actual.Equals(expected, StringComparison.Ordinal))
                    bad.Add($"{rel}  →  namespace {actual}，期望 {expected}");
            }

            Assert.True(bad.Count == 0,
                "以下文件的 namespace 与所在目录不一致（§3 要求「目录 ↔ 命名空间 1:1」）：\n  "
                + string.Join("\n  ", bad)
                + "\n\n处置：把 namespace 改成与目录一致，并同步改下游 using。"
                + "（漏改一个文件时，若下游 using 也一起没改，编译和守卫都不会报错 —— 本仓踩过。）");
        }

        [Fact]
        public void 目录不得越级引用_准入线()
        {
            var violations = FindAdmissionViolations(Allowed);

            Assert.True(violations.Count == 0,
                "以下文件越级引用了未登记的依赖（见 Extension/FILE-TIERING.md §3）：\n  "
                + string.Join("\n  ", violations)
                + "\n\n处置：① 把该依赖移到已登记它的目录（T3/T2/T1）；或 ② 在 TierGuardTests.Allowed 里为该目录登记并同步更新 FILE-TIERING.md。");
        }

        [Fact]
        public void 低层目录不得引用高层目录_依赖方向()
        {
            var root = SourceRoot();
            var violations = new List<string>();

            foreach (var file in EnumerateSources())
            {
                var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                var relDir = RelDir(root, file);
                var fromTier = TierFor(relDir);
                var text = File.ReadAllText(file);

                var seen = new HashSet<string>(StringComparer.Ordinal);
                var internalRefs = UsingRegex.Matches(text)
                    .Select(m => m.Groups[1].Value)
                    .Concat(ExtractInternalReferences(text));

                foreach (var ns in internalRefs)
                {
                    if (!ns.Equals("Extension", StringComparison.Ordinal)
                        && !ns.StartsWith("Extension.", StringComparison.Ordinal))
                        continue; // 只看库内引用

                    if (!seen.Add(ns))
                        continue; // using 行与正文会命中同一串，去重

                    var parts = ns.Split('.');
                    if (parts.Length < 2)
                        continue;

                    var topDir = parts[1]; // Extension.<顶层目录>
                    if (!TierOf.ContainsKey(topDir))
                        continue;

                    var toTier = TierOf[topDir];
                    if (toTier > fromTier)
                    {
                        violations.Add(
                            $"{rel}  →  {ns}   （T{fromTier} 的 {relDir} 引用了 T{toTier} 的 {topDir}）");
                    }
                }
            }

            Assert.True(violations.Count == 0,
                "以下文件破坏了依赖方向（低层引用高层）：\n  " + string.Join("\n  ", violations)
                + "\n\n处置：把被引用的类型下移到低层目录，或重新考虑该文件属于哪一层。");
        }

        /// <summary>
        /// 方向扫描器**不是死的**：只在**正文**里写完全限定名的向上引用，必须能被认出来。
        /// </summary>
        /// <remarks>
        /// ⛔ 2026-09-18 补（FILE-TIERING.md §6.L，序 12i）。动机：原方向测试只遍历 <c>using</c> 行，
        /// 「扫正文」这个修复只落到了**准入**测试上 ⇒ 方向测试长期带着第三盲区而**没有任何用例发现它**。
        /// 这条用例把「正文里的完全限定名也算数」变成会红的断言 —— 否则盲区修没修没人知道。
        ///
        /// 用**构造出来的假源码**做输入，不依赖仓库现状（仓库当前恰好 0 处，测它等于没测）。
        ///
        /// ⚠️ 实测到的扫描器属性（无害但要知道）：<see cref="ExtractInternalReferences"/> 连
        /// **文件自己的 `namespace` 声明**也会命中（`namespace Extension.Core.Probe` → 认出 `Extension.Core.Probe`）。
        /// 因为它与所在文件**同层**，`toTier == fromTier` ⇒ 永远不会变成违规；本用例因此要显式排除自身层。
        /// </remarks>
        [Fact]
        public void 方向扫描器必须能看见只在正文里的完全限定名()
        {
            // T0 目录里的文件，正文里直接写 T2 的完全限定名，`using` 块里**什么都没有**
            const string bodyOnly = """
                namespace Extension.Core.Probe
                {
                    internal static class Probe
                    {
                        internal static object Make()
                            => new Extension.Integration.Mapster.Probe();
                    }
                }
                """;

            var found = ExtractInternalReferences(bodyOnly).ToList();
            Assert.Contains("Extension.Integration.Mapster.Probe", found);

            // 对照：旧实现（只扫 using 行）在这段文本上**一个都认不出来**
            Assert.Empty(UsingRegex.Matches(bodyOnly).Cast<Match>());

            // 跨层引用只有 Runtime 一个（排除文件自身的 Extension.Core.* 声明）
            var crossTop = found
                .Select(ns => ns.Split('.')[1])
                .Where(top => TierOf.ContainsKey(top) && top != "Core")
                .Distinct()
                .ToList();
            Assert.Equal(new[] { "Integration" }, crossTop);

            Assert.True(TierOf["Integration"] > TierOf["Core"],
                "T0 的 Core 引用 T2 的 Runtime 必须是反向违规 —— 若这条不成立，说明层级表被改了，本用例需要同步。");

            // 注释行里的不算（否则说明文字会被误判成依赖）
            const string commented = """
                // Extension.Integration.Mapster.Probe 只是注释
                namespace Extension.Core.Probe { }
                """;
            Assert.DoesNotContain("Extension.Integration.Mapster.Probe", ExtractInternalReferences(commented));
        }

        [Fact]
        public void 允许清单本身必须是良构的_目录存在且登记项都真的会被检查()
        {
            var root = SourceRoot();
            var problems = new List<string>();

            foreach (var kv in Allowed)
            {
                var dir = Path.Combine(root, kv.Key.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(dir))
                    problems.Add($"Allowed 登记的目录不存在：{kv.Key}");

                foreach (var ns in kv.Value)
                {
                    if (!GuardedPrefixes.Contains(ns, StringComparer.Ordinal))
                        problems.Add(
                            $"{kv.Key} 登记了 {ns}，但它不在 GuardedPrefixes 里 —— 该条目永远不会被检查。");
                }
            }

            Assert.True(problems.Count == 0, string.Join("\n  ", problems));
        }

        [Fact]
        public void 层级表必须覆盖全部顶层目录_不得有漏网的层()
        {
            // 只统计**真的含有 .cs 的**顶层目录 —— 与本守卫实际扫描的范围一致。
            // 不用 Directory.EnumerateDirectories，否则 bin/obj/.vs 以及搬空后残留的
            // 空目录都会进来，让用例变成噪声源。
            var root = SourceRoot();
            var topDirs = EnumerateSources()
                .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
                .Where(rel => rel.Contains('/'))
                .Select(rel => rel.Split('/')[0])
                .Distinct(StringComparer.Ordinal)
                .ToList();

            Assert.True(topDirs.Count > 0, "没有扫到任何顶层目录 —— 路径解析可能出错");

            var missing = topDirs.Where(d => !TierOf.ContainsKey(d)).ToList();

            Assert.True(missing.Count == 0,
                "以下顶层目录没在 TierOf 里登记层级（会被当成 T0，可能放过反向引用）：\n  "
                + string.Join("\n  ", missing));
        }
    }
}
