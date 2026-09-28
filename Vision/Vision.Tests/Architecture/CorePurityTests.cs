using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Vision.Core.AI;
using Vision.Core.Coordinate;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;
using Vision.Core.Serialization;
using Xunit;

namespace Vision.Tests.Architecture
{
    /// <summary>
    /// P2 验收守卫 —— 《Vision_Architecture_Optimization_Plan v1.2》§P2：
    /// <code>
    /// 添加架构守卫测试，禁止 Core 引用 HALCON/OpenCV/WPF/厂商 SDK。
    /// 验收：Core 可独立编译和测试；无第三方视觉 SDK。
    /// </code>
    ///
    /// 为什么必须靠测试钉住：P2 的交付物是**契约类型 + 摘掉一条 ProjectReference**，
    /// 没有任何算法行为变化 —— 「越界」在编译期**完全不报错**：
    /// 给 Core 加一条 <c>ProjectReference</c> 指向 <c>DPLibrary_Halcon_F48</c>、
    /// 或在 Core 里写一句 <c>using HalconDotNet;</c>，整个解决方案照样编译通过，
    /// 直到某个「不需要视觉 SDK 的部署环境」加载 Core 时才发现多背了 200MB 依赖。
    ///
    /// ⛔ 与 <see cref="SolutionStructureTests"/> 的分工：
    ///   那个守「Vision/ 下的工程集合与引用方向」（骨架）；
    ///   这个守「Vision.Core 这个程序集内部是否干净」（纯度）。
    ///
    /// ⛔ 扫描口径三条纪律（沿用 <c>SolutionStructureTests</c> 与
    /// <c>Invi/InviRetirementTests</c> 的教训）：
    ///   ① 必须跳过 <c>bin</c>/<c>obj</c>，否则构建产物里的 <c>*_wpftmp</c> 与备份目录造成假红；
    ///   ② **先剥注释**再扫 —— 本仓库的注释里大量出现旧工程名与旧命名空间
    ///     （例如 <c>ToolMge.cs</c> 的注释就写着「原先声明的是 namespace DPLibrary_Halcon_F48.Tools」），
    ///     不剥注释必然假红；
    ///   ③ 每条断言都要配**防假绿**断言（扫到的文件数必须大于下限），
    ///     否则路径写错 ⇒ 扫到 0 个 ⇒ 断言恒真 ⇒ 守卫形同不存在。
    /// </summary>
    [Trait("Category", "Architecture")]
    public sealed class CorePurityTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // 登记清单
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 禁止在 Vision.Core 里出现的命名空间**根**（第三方视觉 SDK / 厂商运行时）。
        /// 匹配口径是「<c>using X</c> 或 <c>X.</c> 形式的引用」，不是简单的子串包含 ——
        /// 否则 <c>HalconDotNet</c> 会误伤注释与字符串里提到的名字（注释已先剥掉）。
        /// </summary>
        private static readonly string[] ForbiddenNamespaceRoots =
        [
            "HalconDotNet",        // MVTec HALCON 的 .NET 封装（唯一入口是 Extension.csproj，见硬规则 12）
            "MVTec",               // HALCON 包 / 命名空间前缀
            "OpenCvSharp",         // OpenCV 封装
            "Emgu",                // OpenCV 的另一套封装
            "Cognex",              // Cognex VisionPro（不单独登记 "VisionPro"：
                                   //   E_VisionType.VisionPro 是既有的领域枚举成员，登记了会假红）
            "Aiditron",            // 谛创 AI SDK
            "Basler",              // 相机 SDK（Basler.Pylon 已被本前缀覆盖）
            "DPLibrary_Base_F48",  // 旧工程命名空间（P1 已改名 Vision.Core；迁移源，P9 退场）
            "DPLibrary_Halcon_F48",
            "Extension",           // 本仓 Extension 工程 —— HalconDotNet 的唯一入口，Core 不得引用
        ];

        /// <summary>
        /// Vision.Core 允许的 <c>PackageReference</c>（只做「不能是厂商 SDK」的负向判定 +
        /// 一条「确实解析到了包」的防假绿断言；见 <see cref="CoreProject_ReferencesNoProjects_AndNoVisionSdkPackages"/>）。
        /// </summary>
        private const string ExpectedCorePackage = "System.Text.Json";

        /// <summary>
        /// <b>WPF 残留冻结清单</b> —— 这是 P2 对计划原文的一处**有意偏离**，必须写在代码里：
        ///
        /// 计划 §P2 写的是「禁止 Core 引用 HALCON/OpenCV/<b>WPF</b>/厂商 SDK」，
        /// 但把 WPF 清干净等价于「ROI / Coordinate 统一」——
        /// 那正是 **P7** 的交付物（§P7「把 ROI 和坐标语义从 HALCON 类型中抽出」）。
        /// 本轮若强行清掉，就要同时改动 30+ 个文件（<c>System.Windows.Point</c> 是
        /// <c>I_ROI_*Data</c> / <c>I_XLD_*Data</c> 的公开契约），
        /// 把 P2 变成一次跨模块行为变更，违背「只改边界与引用」。
        ///
        /// 所以 P2 的处置是：**把残留冻结成清单**——清单外的任何 WPF 引用 ⇒ 红；
        /// 清单内的残留被清掉（P7 做）⇒ 也红，提醒同步删登记条目（清单不腐烂）。
        /// 硬性禁止的是 HALCON / OpenCV / 厂商 SDK（见 <see cref="ForbiddenNamespaceRoots"/>）。
        /// </summary>
        private static readonly string[] FrozenWpfResidualFiles =
        [
            // IValueConverter：唯一实现是 DoubleArray2DToStringConverter，全仓**无消费者**（P7 随 ROI 一起处置）
            "Convertors.cs",
            // BindingOperations.EnableCollectionSynchronization：WPF 数据绑定线程亲和性，属性面板依赖它
            "ObservableCollectionEx/BulkObservableCollection.cs",
            // MessageBox.Show：OnBindingPortBT_Clicked 里的调试残留，属「发现即记录、不顺手改」
            "Tools/ITool.cs",
            // System.Windows.Point：ROI 顶点契约
            "Vision/I_ROIs.cs",
            // System.Windows.Point：XLD 端点/圆心契约
            "Vision/I_XLDs.cs",
        ];

        /// <summary>P2 新建的契约类型 —— 逐个用反射检查公开签名里没有厂商类型。</summary>
        private static readonly Type[] P2ContractTypes =
        [
            typeof(VisionImage),
            typeof(PixelFormat),
            typeof(Point2D),
            typeof(Size2D),
            typeof(Rect2D),
            typeof(Pose2D),
            typeof(Vision.Core.Regions.RegionKind),
            typeof(Vision.Core.Regions.IVisionRegion),
            typeof(Vision.Core.Regions.IVisionContour),
            typeof(Vision.Core.Roi.RoiKind),
            typeof(IRoi),
            typeof(CoordinateSpace),
            typeof(ICoordinateTransform),
            typeof(ValidationResult),
            typeof(ValidationSeverity),
            typeof(ToolResult),
            typeof(AIModelType),
            typeof(AICapabilities),
            typeof(IAIProvider),
            typeof(IAIModelSession),
            typeof(AIClassificationResult),
            typeof(AIDetectionResult),
            typeof(AISegmentationResult),
            typeof(AIAnomalyResult),
        ];

        /// <summary>Vision.Core 源文件数的下限（当前 ~40），用作「扫描没失效」的判据。</summary>
        private const int MinimumCoreSourceFiles = 25;

        // ─────────────────────────────────────────────────────────────────────
        // 1. csproj 层：不引任何工程、不引任何视觉 SDK 包
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// P2 摘掉了 <c>Vision.Core → Extension</c> 的引用；这条守卫保证它不会被加回来，
        /// 也保证不会有别的工程引用（或厂商 NuGet 包）悄悄回到 Core。
        /// </summary>
        [Fact]
        public void CoreProject_ReferencesNoProjects_AndNoVisionSdkPackages()
        {
            var csproj = Path.Combine(CoreDirectory, "Vision.Core.csproj");
            Assert.True(File.Exists(csproj), $"Vision.Core 工程不存在：{csproj}");

            var text = File.ReadAllText(csproj);

            // ⛔ 必须先剥 XML 注释再匹配 —— 本工程的文件头/删除说明里**故意**保留了
            //    被删掉的那条引用的原文（`<!-- ... <ProjectReference Include="..\..\Extension\..."> ... -->`），
            //    不剥注释就会把「历史说明」当成「现存引用」⇒ 假红。
            //    （这条不是假设：本守卫第一次运行就是被它自己的注释绊倒的。）
            text = Regex.Replace(text, "<!--.*?-->", " ", RegexOptions.Singleline);

            // ① 不得有任何 ProjectReference
            var projectReferences = Regex
                .Matches(text, "<ProjectReference\\s+Include=\"(?<path>[^\"]+)\"")
                .Select(m => m.Groups["path"].Value)
                .ToArray();

            Assert.True(
                projectReferences.Length == 0,
                "Vision.Core 不允许引用任何工程（P2 已摘掉 Extension，且 Core 是依赖树的根）。实际引用：" +
                string.Join(", ", projectReferences));

            // ② 不得有厂商 SDK 包
            var packages = Regex
                .Matches(text, "<PackageReference\\s+Include=\"(?<name>[^\"]+)\"")
                .Select(m => m.Groups["name"].Value)
                .ToArray();

            // 防假绿：连一个包都没解析出来 ⇒ 正则或文件格式变了，下面的断言会恒真
            Assert.True(
                packages.Length > 0,
                $"Vision.Core.csproj 里一个 PackageReference 都没解析到，正则疑似失效：{csproj}");

            var offenders = packages
                .Where(name => ForbiddenNamespaceRoots.Any(
                    root => name.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            Assert.True(
                offenders.Length == 0,
                "Vision.Core 不允许引用视觉 / 厂商 SDK 包。违规包：" + string.Join(", ", offenders));

            // ③ 反方向：计划要求的「Core 可独立编译」靠的是 BCL + 显式 UseWPF，而不是借来的依赖。
            //    这里只钉住「当前确实只引 System.Text.Json」这个事实，避免以后静默膨胀。
            Assert.Contains(ExpectedCorePackage, packages);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2. 源码层：不引用厂商命名空间
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 硬性禁止项：HALCON / OpenCV / 厂商 SDK 的命名空间不得出现在 Core 源码里
        /// （无论是 <c>using</c> 还是完全限定名）。
        /// </summary>
        [Fact]
        public void CoreSources_DoNotReference_VisionSdkNamespaces()
        {
            var files = EnumerateCoreSourceFiles().ToArray();

            // 防假绿
            Assert.True(
                files.Length >= MinimumCoreSourceFiles,
                $"Vision.Core 下只扫到 {files.Length} 个 .cs（下限 {MinimumCoreSourceFiles}），扫描路径疑似失效。");

            var offenders = new List<string>();

            foreach (var file in files)
            {
                var code = StripComments(File.ReadAllText(file));
                var relative = RelativeToCore(file);

                foreach (var root in ForbiddenNamespaceRoots)
                {
                    // using X; / using X.Y; —— 允许前置 global 与空白（本仓多数文件带 BOM）
                    if (Regex.IsMatch(code, $@"\busing\s+{Regex.Escape(root)}(\.|;|\s)", RegexOptions.Multiline))
                        offenders.Add($"{relative} -> using {root}");

                    // 完全限定名 X.Y / X<...>
                    if (Regex.IsMatch(code, $@"(?<![\w.]){Regex.Escape(root)}(\.|<)"))
                        offenders.Add($"{relative} -> {root}.*");
                }
            }

            Assert.Empty(offenders);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 3. 源码层：只声明 Vision.Core.* 命名空间（P2 收编的回归守卫）
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// P2 把两个「游离命名空间」收回了 Vision.Core：
        /// <list type="bullet">
        ///   <item><c>DPLibrary_Halcon_F48.Tools</c>（<c>ToolMge.cs</c>）→ <c>Vision.Core.Tools</c>；
        ///     它原先与真项目 DPLibrary_Halcon_F48 的 Tools 命名空间**同名**，
        ///     跨程序集合并成一个逻辑命名空间，于是 Halcon 侧不写 using 就能用 ToolMge。</item>
        ///   <item><c>Common.StreamEX.Json</c>（<c>JsonHelper.cs</c>）→ <c>Vision.Core.Serialization</c>。</item>
        /// </list>
        /// 这条守卫保证它们不会被改回去 —— 因为「改回去」在编译期同样无感。
        /// </summary>
        [Fact]
        public void CoreSources_DeclareOnlyVisionCoreNamespaces()
        {
            var files = EnumerateCoreSourceFiles().ToArray();
            Assert.True(files.Length >= MinimumCoreSourceFiles, $"只扫到 {files.Length} 个 .cs，扫描疑似失效。");

            var declared = 0;
            var offenders = new List<string>();

            foreach (var file in files)
            {
                var code = StripComments(File.ReadAllText(file));
                var relative = RelativeToCore(file);

                foreach (Match match in Regex.Matches(
                             code,
                             @"(?m)^[ \t]*namespace[ \t]+(?<name>[A-Za-z_][A-Za-z0-9_.]*)",
                             RegexOptions.Multiline))
                {
                    declared++;
                    var name = match.Groups["name"].Value;

                    if (name == "Vision.Core" ||
                        name.StartsWith("Vision.Core.", StringComparison.Ordinal))
                        continue;

                    offenders.Add($"{relative} -> namespace {name}");
                }
            }

            // 防假绿：一个 namespace 都没解析到 ⇒ 正则失效
            Assert.True(declared > 0, "没有解析出任何 namespace 声明，正则疑似失效。");

            Assert.Empty(offenders);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 4. WPF 残留冻结（P2 对计划原文的有意偏离，见 FrozenWpfResidualFiles 注释）
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Core 里对 WPF 的引用必须**恰好**是冻结清单那 5 个文件：
        /// 多一个 ⇒ 红（有人把 WPF 又拖进 Core）；少一个 ⇒ 红（提醒删登记条目）。
        /// </summary>
        [Fact]
        public void CoreWpfResidual_MatchesFrozenExemptionList()
        {
            var files = EnumerateCoreSourceFiles().ToArray();
            Assert.True(files.Length >= MinimumCoreSourceFiles, $"只扫到 {files.Length} 个 .cs，扫描疑似失效。");

            var actual = files
                .Where(file =>
                {
                    var code = StripComments(File.ReadAllText(file));
                    return Regex.IsMatch(code, @"\bSystem\.Windows(\.|;|\s)");
                })
                .Select(RelativeToCore)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            // 防假绿：清单本身非空；若扫描失效会得到空集，Equal 也会红但原因指向不明
            Assert.True(
                FrozenWpfResidualFiles.Length > 0,
                "WPF 残留冻结清单为空 —— 要么 P7 已完成（那请连这条断言一起删），要么登记被误清。");

            Assert.Equal(
                FrozenWpfResidualFiles.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
                actual);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 5. 契约类型存在，且公开签名里没有厂商类型
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 反射检查 P2 契约类型的**公开签名**：属性类型、方法返回/参数类型、字段类型、基类与接口
        /// （含泛型实参）只允许来自 <c>Vision.Core</c> 自身或 BCL。
        ///
        /// 为什么比扫 <c>using</c> 强：完全限定名可以绕过 using 扫描
        /// （<c>public HObject Image { get; }</c> 一个字都不用 using），
        /// 反射看到的是**编译后的真实类型**，绕不过去。
        /// </summary>
        [Fact]
        public void P2DomainContracts_Exist_AndExposeOnlyBclOrCoreTypes()
        {
            Assert.True(
                P2ContractTypes.Length >= 20,
                $"P2 契约类型登记只有 {P2ContractTypes.Length} 项，清单疑似被误删。");

            var offenders = new List<string>();

            foreach (var type in P2ContractTypes)
            {
                Assert.NotNull(type);

                foreach (var exposed in ExposedTypes(type))
                {
                    if (IsAllowedContractType(exposed))
                        continue;

                    offenders.Add($"{type.FullName} 暴露了非 BCL/Core 类型：{exposed.FullName}");
                }
            }

            Assert.Empty(offenders.Distinct());
        }

        // ─────────────────────────────────────────────────────────────────────
        // 6. 契约可 JSON 往返（「Core 可独立编译和测试」的正面证据）
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// P2 新建的领域类型必须能经 <see cref="JsonHelper"/> 原样往返。
        ///
        /// 这条守的是仓库已经踩过的那个坑：<c>System.Text.Json</c> 对**非公有 setter**
        /// 是静默跳过（不抛、不警告），读档后属性回落到默认值。
        /// 新契约全部用公有 setter，这里用往返断言把它钉住
        /// （与 <c>WPFControl.Tests/SerializationContractTests</c> 的口径一致，那个是静态扫描，这个是行为验证）。
        /// </summary>
        [Fact]
        public void P2DomainContracts_RoundTripThroughJson()
        {
            // 几何值类型
            var pose = new Pose2D(12.5, -3.25, 30);
            Assert.Equal(pose, JsonHelper.Deserialize<Pose2D>(JsonHelper.Serialize(pose)));

            var rect = new Rect2D(1.5, 2.5, 30, 40);
            Assert.Equal(rect, JsonHelper.Deserialize<Rect2D>(JsonHelper.Serialize(rect)));

            var point = new Point2D(7, 8);
            Assert.Equal(point, JsonHelper.Deserialize<Point2D>(JsonHelper.Serialize(point)));

            var size = new Size2D(640, 480);
            Assert.Equal(size, JsonHelper.Deserialize<Size2D>(JsonHelper.Serialize(size)));

            // 校验结果
            var validation = ValidationResult
                .Success()
                .AddError("ROI_EMPTY", "ROI 为空", "Roi")
                .AddWarning("TH_LOW", "阈值偏低", "Threshold");

            var validationBack = JsonHelper.Deserialize<ValidationResult>(JsonHelper.Serialize(validation));
            Assert.Equal(2, validationBack.Issues.Count);
            Assert.False(validationBack.IsValid);   // 有 Error
            Assert.True(validationBack.HasWarnings);
            Assert.Equal("ROI_EMPTY", validationBack.Issues[0].Code);
            Assert.Equal("Roi", validationBack.Issues[0].Member);

            // 图像（像素走 byte[] → base64）
            var image = VisionImage.FromBuffer(4, 2, PixelFormat.Mono8, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, source: "unit-test");
            var imageBack = JsonHelper.Deserialize<VisionImage>(JsonHelper.Serialize(image));
            Assert.Equal(4, imageBack.Width);
            Assert.Equal(2, imageBack.Height);
            Assert.Equal(PixelFormat.Mono8, imageBack.Format);
            Assert.Equal("unit-test", imageBack.Source);
            Assert.Equal(image.Pixels, imageBack.Pixels);
            Assert.False(imageBack.IsEmpty);

            // AI 公共结果
            var detection = new AIDetectionResult
            {
                Success = true,
                ElapsedMs = 42,
                Boxes =
                {
                    new AIDetectionBox { Box = rect, Label = "ok", ClassId = 1, Score = 0.93 },
                },
            };

            var detectionBack = JsonHelper.Deserialize<AIDetectionResult>(JsonHelper.Serialize(detection));
            Assert.Single(detectionBack.Boxes);
            Assert.Equal("ok", detectionBack.Boxes[0].Label);
            Assert.Equal(rect, detectionBack.Boxes[0].Box);
            Assert.Equal(42, detectionBack.ElapsedMs);
            Assert.False(detectionBack.IsFailed);   // IToolResult 桥接：Success=true ⇒ IsFailed=false

            var classification = new AIClassificationResult
            {
                TopLabel = "NG",
                TopScore = 0.81,
                Scores = { new AIClassScore { Label = "NG", ClassId = 1, Score = 0.81 } },
            };

            var classificationBack =
                JsonHelper.Deserialize<AIClassificationResult>(JsonHelper.Serialize(classification));
            Assert.Equal("NG", classificationBack.TopLabel);
            Assert.Single(classificationBack.Scores);
            Assert.Equal(0.81, classificationBack.Scores[0].Score, 6);

            var segmentation = new AISegmentationResult
            {
                Items =
                {
                    new AISegmentationItem
                    {
                        Label = "defect",
                        Score = 0.7,
                        Bounds = rect,
                        Polygon = { new Point2D(1, 1), new Point2D(2, 2), new Point2D(3, 1) },
                    },
                },
            };

            var segmentationBack = JsonHelper.Deserialize<AISegmentationResult>(JsonHelper.Serialize(segmentation));
            Assert.Single(segmentationBack.Items);
            Assert.Equal(3, segmentationBack.Items[0].Polygon.Count);
            Assert.Equal(new Point2D(2, 2), segmentationBack.Items[0].Polygon[1]);

            var anomaly = new AIAnomalyResult { IsAnomaly = true, Score = 0.66, AnomalyBounds = rect };
            var anomalyBack = JsonHelper.Deserialize<AIAnomalyResult>(JsonHelper.Serialize(anomaly));
            Assert.True(anomalyBack.IsAnomaly);
            Assert.Equal(0.66, anomalyBack.Score, 6);
            Assert.Equal(rect, anomalyBack.AnomalyBounds);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 工具
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Vision 模块目录（<c>&lt;仓库根&gt;/Vision</c>）。</summary>
        private static string VisionDirectory => Path.Combine(RepoRoot, "Vision");

        /// <summary>Vision.Core 目录。</summary>
        private static string CoreDirectory => Path.Combine(VisionDirectory, "Vision.Core");

        /// <summary>仓库根（以 Frame.sln 为标记向上找）。与 SolutionStructureTests 同口径，各自独立。</summary>
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

        /// <summary>遍历 Vision.Core 下的 .cs（跳过 bin / obj / .vs 等）。</summary>
        /// <returns>.cs 绝对路径序列。</returns>
        private static IEnumerable<string> EnumerateCoreSourceFiles()
        {
            var skipDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "obj", "bin", ".vs", ".git",
            };

            var stack = new Stack<string>();
            stack.Push(CoreDirectory);

            while (stack.Count > 0)
            {
                var dir = stack.Pop();

                foreach (var sub in Directory.GetDirectories(dir))
                {
                    if (!skipDirs.Contains(Path.GetFileName(sub)))
                        stack.Push(sub);
                }

                foreach (var file in Directory.GetFiles(dir, "*.cs"))
                    yield return file;
            }
        }

        /// <summary>把绝对路径转成相对 Vision.Core 的正斜杠路径。</summary>
        /// <param name="file">文件绝对路径。</param>
        /// <returns>相对路径。</returns>
        private static string RelativeToCore(string file) =>
            Path.GetRelativePath(CoreDirectory, file).Replace('\\', '/');

        /// <summary>
        /// 剥掉 C# 注释（<c>//</c> 与 <c>/* */</c>）并去掉 BOM。
        /// ⛔ 保留字符串字面量 —— 字符串里出现厂商类型名（如 <c>Type.GetType("HalconDotNet.HObject")</c>）
        /// 恰恰是**真实依赖**，必须能被扫到。
        ///
        /// 为什么用手写状态机而不是 <c>Regex.Replace</c>：字符串字面量里完全可以出现 <c>//</c>
        /// （URL、路径分隔、正则字面量），正则版会把该行后半段一起吃掉 ——
        /// 那会造成**假绿**（把同一行上的真实违规一起删掉），而假绿比假红危险得多。
        /// </summary>
        /// <param name="code">源码文本。</param>
        /// <returns>剥掉注释、保留字符串与字符字面量的文本。</returns>
        private static string StripComments(string code)
        {
            var builder = new StringBuilder(code.Length);
            var i = 0;

            while (i < code.Length)
            {
                var current = code[i];

                // 行注释
                if (current == '/' && i + 1 < code.Length && code[i + 1] == '/')
                {
                    while (i < code.Length && code[i] != '\n')
                        i++;

                    continue;
                }

                // 块注释
                if (current == '/' && i + 1 < code.Length && code[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < code.Length && !(code[i] == '*' && code[i + 1] == '/'))
                        i++;

                    i = Math.Min(i + 2, code.Length);
                    continue;
                }

                // 逐字字符串 @"..."（内部用 "" 转义，且不处理反斜杠）
                if (current == '@' && i + 1 < code.Length && code[i + 1] == '"')
                {
                    builder.Append('@').Append('"');
                    i += 2;

                    while (i < code.Length)
                    {
                        builder.Append(code[i]);

                        if (code[i] == '"')
                        {
                            if (i + 1 < code.Length && code[i + 1] == '"')
                            {
                                builder.Append(code[i + 1]);
                                i += 2;
                                continue;
                            }

                            i++;
                            break;
                        }

                        i++;
                    }

                    continue;
                }

                // 普通字符串 / 字符字面量
                if (current == '"' || current == '\'')
                {
                    var quote = current;
                    builder.Append(quote);
                    i++;

                    while (i < code.Length)
                    {
                        builder.Append(code[i]);

                        if (code[i] == '\\' && i + 1 < code.Length)
                        {
                            builder.Append(code[i + 1]);
                            i += 2;
                            continue;
                        }

                        if (code[i] == quote)
                        {
                            i++;
                            break;
                        }

                        i++;
                    }

                    continue;
                }

                builder.Append(current);
                i++;
            }

            return builder.ToString().Replace("\uFEFF", string.Empty);
        }

        /// <summary>收集一个类型公开签名里出现的所有类型（含泛型实参、数组元素、基类与接口）。</summary>
        /// <param name="type">目标类型。</param>
        /// <returns>类型序列（可能含 null，调用方过滤）。</returns>
        private static IEnumerable<Type> ExposedTypes(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

            if (type.BaseType != null)
                yield return type.BaseType;

            foreach (var itf in type.GetInterfaces())
                yield return itf;

            foreach (var property in type.GetProperties(flags))
                yield return property.PropertyType;

            foreach (var field in type.GetFields(flags))
                yield return field.FieldType;

            foreach (var method in type.GetMethods(flags))
            {
                if (method.IsSpecialName)
                    continue;   // 属性/事件的存取器已由上面覆盖

                yield return method.ReturnType;

                foreach (var parameter in method.GetParameters())
                    yield return parameter.ParameterType;
            }
        }

        /// <summary>
        /// 判断一个类型是否允许出现在 Core 的公开契约里：
        /// 只允许 <c>Vision.Core</c> 自身、BCL（<c>System.*</c> / <c>netstandard</c> / <c>mscorlib</c>）、
        /// 以及泛型参数 / 数组 / 指针这类无程序集归属的构造。
        /// ⛔ 泛型实参也要递归判断 —— 否则 <c>List&lt;HObject&gt;</c> 会因为 <c>List&lt;&gt;</c> 是 BCL 而漏网。
        /// </summary>
        /// <param name="type">待判定类型。</param>
        /// <returns>允许时返回 true。</returns>
        private static bool IsAllowedContractType(Type type)
        {
            if (type == null)
                return true;

            if (type.IsGenericParameter)
                return true;

            if (type.IsArray)
                return IsAllowedContractType(type.GetElementType());

            if (type.IsByRef || type.IsPointer)
                return IsAllowedContractType(type.GetElementType());

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    if (!IsAllowedContractType(argument))
                        return false;
                }
            }

            var assemblyName = type.Assembly.GetName().Name ?? string.Empty;

            // ⛔ 刻意**不放行** WindowsBase / PresentationCore / PresentationFramework：
            //    P2 新建的契约必须彻底与 WPF 无关（P2 对计划原文的偏离只发生在
            //    旧 I_ROI / I_XLD / Convertors / BulkObservableCollection 这 5 个文件上，
            //    见 FrozenWpfResidualFiles）。所以 System.Windows.Point 出现在新契约里 ⇒ 红。
            //    BCL 的判据用 "System." 前缀足够：.NET 9 里 BCL 程序集都叫 System.*。
            return assemblyName == "Vision.Core" ||
                   assemblyName.StartsWith("System.", StringComparison.Ordinal) ||
                   assemblyName == "System" ||
                   assemblyName == "netstandard" ||
                   assemblyName == "mscorlib";
        }
    }
}
