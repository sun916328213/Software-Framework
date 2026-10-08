// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ThemeSettingsTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Linq、Extension.UI.Theme、Xunit。导入不代表所有类型都会在运行时加载。
using System;
using System.Linq;
using Extension.UI.Theme;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// <c>Ui/Theme/ThemeSettings.cs</c> 的特征测试（2026-09-18 补测第四轮）。
    /// </summary>
    /// <remarks>
    /// 这个文件是"**级别与目录名正交**"的最干净样本（FILE-TIERING.md §1 复核表）：
    /// 它住在 T2 的 `Ui/Theme/` 里（邻居 `ThemeService.cs` 需要 MaterialDesign + WPF），
    /// 自己却是一个**零 `using`** 的纯 record ⇒ 属 **T0**。
    ///
    /// ⛔ 本文件**刻意不测 `ThemeService`**：它的公开签名里带 WPF 类型，
    /// 而 `Extension.Tests` 没有 `UseWPF` ⇒ 连 `typeof(ThemeService)` 都可能触发程序集加载失败。
    /// 那属于 T2 面，不在 T0 补测范围内。
    /// </remarks>
    public class ThemeSettingsTests
    {
        /// <summary>
        /// 测试相关入口 默认值_浅色加Teal加Amber；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 False、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void 默认值_浅色加Teal加Amber()
        {
            var s = new ThemeSettings();

            Assert.False(s.IsDark);
            Assert.Equal("Teal", s.Primary);
            Assert.Equal("Amber", s.Secondary);
        }

        /// <summary>
        /// 测试相关入口 具名参数构造_覆盖默认值；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 True、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void 具名参数构造_覆盖默认值()
        {
            var s = new ThemeSettings(IsDark: true, Primary: "Indigo", Secondary: "Pink");

            Assert.True(s.IsDark);
            Assert.Equal("Indigo", s.Primary);
            Assert.Equal("Pink", s.Secondary);
        }

        /// <summary>
        /// 测试相关入口 是record_按值相等；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、True、GetHashCode；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void 是record_按值相等()
        {
            var a = new ThemeSettings(IsDark: true, Primary: "Indigo", Secondary: "Pink");
            var b = new ThemeSettings(IsDark: true, Primary: "Indigo", Secondary: "Pink");

            Assert.Equal(a, b);
            Assert.True(a == b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        /// <summary>
        /// 测试相关入口 With表达式_只改指定成员；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 True、Equal、False；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void With表达式_只改指定成员()
        {
            var light = new ThemeSettings();
            var dark = light with { IsDark = true };

            Assert.True(dark.IsDark);
            Assert.Equal("Teal", dark.Primary);   // 其余成员继承
            Assert.Equal("Amber", dark.Secondary);
            Assert.False(light.IsDark);           // 原实例不受影响（不可变）
        }

        /// <summary>
        /// 测试相关入口 是sealed_record_不是普通类；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 True、NotNull、GetMethod；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void 是sealed_record_不是普通类()
        {
            var type = typeof(ThemeSettings);

            Assert.True(type.IsSealed, "record 声明为 sealed");
            // record 会生成一个名为 `<Clone>$` 的受保护克隆方法，普通 class 没有。
            Assert.NotNull(type.GetMethod("<Clone>$", System.Reflection.BindingFlags.Public
                                                       | System.Reflection.BindingFlags.NonPublic
                                                       | System.Reflection.BindingFlags.Instance));
        }

        /// <summary>
        /// 测试相关入口 属性类型全部来自BCL_所以它属于T0；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 GetProperties、NotEmpty、All、True；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void 属性类型全部来自BCL_所以它属于T0()
        {
            // 这是"`ThemeSettings` 是 T0 文件"的可执行证据：
            // 它不对外暴露任何第三方/UI 类型，宿主直接构造它不会拉起 WPF 或 MaterialDesign。
            var properties = typeof(ThemeSettings).GetProperties();

            Assert.NotEmpty(properties);
            Assert.All(properties, p =>
            {
                var ns = p.PropertyType.Namespace;
                bool isBcl = ns == "System"
                             || (ns?.StartsWith("System.", StringComparison.Ordinal) ?? false);
                Assert.True(isBcl, $"属性 {p.Name} 的类型 {p.PropertyType} 不在 BCL 里（Namespace={ns ?? "<null>"}）");
            });
        }

        /// <summary>
        /// 测试相关入口 ToString_带属性名与值；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToString、Contains；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToString_带属性名与值()
        {
            var text = new ThemeSettings().ToString();

            Assert.Contains("IsDark", text);
            Assert.Contains("Teal", text);
            Assert.Contains("Amber", text);
        }
    }
}
