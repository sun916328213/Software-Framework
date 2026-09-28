using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using Vision.Core;
using DPLibrary_Halcon_F48.Tools;
using DPLibrary_Halcon_F48.Tools.FindLine;
using Xunit;

namespace WPFControl.Tests
{
    /// <summary>
    /// 序列化契约守卫：钉住「公有属性 + 非公有 setter」这个**会静默失效**的组合。
    ///
    /// 背景（见仓库 <c>.workbuddy-ai/memory/REFERENCE.md</c>「System.Text.Json 的「非公有 setter」契约」）：
    /// 本仓库已从 <c>Newtonsoft.Json</c> 全量迁到 <c>System.Text.Json</c>（DPLibrary_* 里已无 Newtonsoft 引用）。
    /// 二者有一条**关键契约差异**：
    /// <list type="bullet">
    ///   <item>Newtonsoft <b>会</b>给非公有 setter 赋值；</item>
    ///   <item>STJ <b>不会</b>，而且是<b>静默跳过</b> —— 不抛异常、不警告、无全局开关可放开。</item>
    /// </list>
    /// 后果：JSON 文本里明明写着 <c>"X": "值"</c>，反序列化后属性却停在**默认值**
    /// （引用类型 = null、枚举 = 0 值成员、Point = (0,0)）。编译期、运行期都不报错。
    ///
    /// 同一组合还会连带打掉**属性面板**的可编辑性 ——
    /// <c>Vision/Vision.Controls/Ctr_Base/PropertyEditorBindingHelper.cs</c> 的判据是
    /// <c>prop.CanWrite &amp;&amp; prop.SetMethod != null &amp;&amp; prop.SetMethod.IsPublic</c>。
    ///
    /// ⚠️ 本守卫**必须用反射**，不能用正则扫源码。实测教训：
    /// <c>private set;</c>（自动属性）能被 <c>\b(private|protected|internal)\s+(?:init|set)\s*;</c> 抓到，
    /// 但 <c>protected set { ... }</c>（带体属性）**抓不到**（<c>set</c> 后面没有分号），
    /// 于是 <c>HBase_TemplateControl::BasePoint</c> 被正则漏掉、靠反射才发现。
    ///
    /// 约定（与 <see cref="ArchitectureDependencyTests"/> 一致）：已知项写进断言里的字面量清单，
    /// 断言「实际集合 == 登记集合」。新增一处 ⇒ 测试红；偿还一处 ⇒ 测试同样红，
    /// 提醒**同步删掉登记条目**，让清单不腐烂。
    ///
    /// 登记粒度是「**类型全名 :: 属性名**」—— 只记类型会漏掉「同一类型里再叠加一个属性」的情况。
    /// </summary>
    public sealed class SerializationContractTests
    {
        /// <summary>
        /// 序列化域：真正会走 STJ 存/读档的两个程序集。
        /// <c>DPLibrary_Halcon_F48</c> 引 <c>Vision.Core</c>；
        /// <c>Tool.Save()</c>（ITool.cs）用 <c>JsonHelper.SerializeToFile(this, …)</c> 整实例存盘，
        /// <c>ToolMge</c> 读回 ⇒ 这两棵树里的属性都在契约射程内。
        /// </summary>
        private static readonly Assembly[] DomainAssemblies =
        [
            typeof(ToolPropertyAttribute).Assembly,   // Vision.Core
            typeof(MeasureParam).Assembly             // DPLibrary_Halcon_F48
        ];

        /// <summary>
        /// 所有「公有实例属性、有 setter、但 setter 非公有、且没有显式退出/进入序列化」的属性，
        /// 必须逐一登记。新增一处即红。
        ///
        /// 判定「退出/进入序列化」的两个豁免特性：
        /// <list type="bullet">
        ///   <item><c>[JsonIgnore]</c> —— 明确不参与序列化，STJ 跳过是**预期行为**；</item>
        ///   <item><c>[JsonInclude]</c> —— 明确要求 STJ 使用这个非公有 setter（实测有效）。</item>
        /// </list>
        /// </summary>
        [Fact]
        public void NonPublicSetters_WithoutExplicitJsonOptIn_AreRegistered()
        {
            var allProperties = 0;
            var declaredSerializable = 0;
            var offenders = new List<string>();

            foreach (var assembly in DomainAssemblies)
            foreach (var type in LoadableTypes(assembly))
            {
                if (type.IsInterface || type.IsEnum || type.Name.Contains('<'))
                    continue;

                PropertyInfo[] properties;
                try
                {
                    properties = type.GetProperties(
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                }
                catch (Exception)
                {
                    // 个别类型元数据不可读时跳过，不让整条守卫挂掉
                    continue;
                }

                foreach (var property in properties)
                {
                    allProperties++;
                    var setter = property.SetMethod;
                    if (property.GetCustomAttribute<JsonPropertyNameAttribute>() != null ||
                        property.GetCustomAttribute<ToolPropertyAttribute>() != null)
                        declaredSerializable++;

                    if (setter == null || setter.IsPublic)
                        continue;
                    if (property.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                        continue;
                    if (property.GetCustomAttribute<JsonIncludeAttribute>() != null)
                        continue;

                    offenders.Add($"{type.FullName}::{property.Name}");
                }
            }

            // 防「扫描静默失效 ⇒ 没有断言 ⇒ 假绿」
            Assert.True(allProperties > 300, $"只扫到 {allProperties} 个公有实例属性，反射扫描疑似失效。");
            Assert.True(declaredSerializable > 100, $"只扫到 {declaredSerializable} 个已声明序列化意图的属性，反射扫描疑似失效。");

            Assert.Equal(
                [
                    // ── 设计使然：**不要**改成 public ─────────────────────────────────────────
                    // ToolInputPorts / ToolOutputPorts 只有带参构造（ToolBus.cs），没有无参构造。
                    // 正因为 setter 非公有 ⇒ STJ 把属性判为不可写 ⇒ **整个属性跳过**（连构造都不做）
                    // ⇒ 不抛异常；而这两个集合本来就在派生类构造函数里重建
                    // （HTool_Fixtrue.cs / HTool_TemplateMatch.cs）⇒ 行为一致。
                    // ⛔ 一旦改成 public，STJ 会尝试构造 ToolInputPorts
                    // ⇒ NotSupportedException（没有可用的无参构造函数）。改了就炸。
                    "Vision.Core.Tools.Tool::Inputs",
                    "Vision.Core.Tools.Tool::Outputs"
                    // 2026-09-23：HBase_TemplateControl::BasePoint 与 HTool_Fixtrue::Matrix
                    // 已按「计算结果」语义加 [JsonInclude]（保留 protected set）修复，
                    // 因此被本守卫的 [JsonInclude] 豁免口径排除 —— 按仓库约定，
                    // 还债时同步删掉登记条目，本行即是那次删除的记录。
                ],
                offenders.OrderBy(entry => entry, StringComparer.Ordinal).ToArray());
        }

        /// <summary>
        /// 正向守卫：以下三处是 2026-09-23 按「计算结果 / 只在构造时确定」语义，
        /// 用 <c>[JsonInclude]</c> + <b>保留非公有 setter</b> 修好的 STJ 静默丢弃缺陷。
        ///
        /// 为什么必须单独再钉一遍：上面那条守卫把 <c>[JsonInclude]</c> 当作**豁免**，
        /// 于是「<b>误删</b> <c>[JsonInclude]</c>」这种退化<b>不会</b>被它发现 ——
        /// 属性会静默退回「读档丢值」状态，守卫却照样绿（修掉一个盲区又开了一个）。
        ///
        /// 两个方向都钉死：
        /// <list type="bullet">
        ///   <item>必须仍带 <c>[JsonInclude]</c> —— 否则 STJ 静默跳过非公有 setter ⇒ 读档丢值；</item>
        ///   <item>setter 必须<b>保持非公有</b> —— 面板据此降级为只读文本，这是刻意设计
        ///     （<c>PropertyEditorBindingHelper.IsEditableProperty</c>）；改成 public 属行为变更。</item>
        /// </list>
        /// </summary>
        /// <param name="type">声明该属性的类型。</param>
        /// <param name="propertyName">属性名。</param>
        [Theory]
        [InlineData(typeof(MeasureParam), "XLDType")]
        [InlineData(typeof(HBase_TemplateControl), "BasePoint")]
        [InlineData(typeof(HTool_Fixtrue), "Matrix")]
        public void NonPublicSetter_SerializedViaJsonInclude_MustKeepOptInAndStayReadOnly(Type type, string propertyName)
        {
            var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property);

            Assert.True(
                property!.GetCustomAttribute<JsonIncludeAttribute>() != null,
                $"{type.Name}::{propertyName} 丢了 [JsonInclude]：STJ 默认不给非公有 setter 赋值且静默跳过，" +
                "读档后该属性会回退成默认值（不抛异常、不警告）。");

            var setter = property.SetMethod;
            Assert.NotNull(setter);
            Assert.False(
                setter!.IsPublic,
                $"{type.Name}::{propertyName} 的 setter 变成 public 了：属性面板会从只读文本变成可编辑控件，" +
                "这是行为变更（该属性的设计意图是只在构造 / 计算时确定，面板不可改）。");
        }

        /// <summary>
        /// 加载程序集里的全部类型；个别类型解析失败时退化为「能加载的那些」，
        /// 不让一个坏类型把整条守卫变成 <see cref="ReflectionTypeLoadException"/>。
        /// </summary>
        /// <param name="assembly">目标程序集。</param>
        /// <returns>可用的类型序列。</returns>
        private static IEnumerable<Type> LoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.OfType<Type>();
            }
        }
    }
}
