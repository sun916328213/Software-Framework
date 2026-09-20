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
        [Fact]
        public void 默认值_浅色加Teal加Amber()
        {
            var s = new ThemeSettings();

            Assert.False(s.IsDark);
            Assert.Equal("Teal", s.Primary);
            Assert.Equal("Amber", s.Secondary);
        }

        [Fact]
        public void 具名参数构造_覆盖默认值()
        {
            var s = new ThemeSettings(IsDark: true, Primary: "Indigo", Secondary: "Pink");

            Assert.True(s.IsDark);
            Assert.Equal("Indigo", s.Primary);
            Assert.Equal("Pink", s.Secondary);
        }

        [Fact]
        public void 是record_按值相等()
        {
            var a = new ThemeSettings(IsDark: true, Primary: "Indigo", Secondary: "Pink");
            var b = new ThemeSettings(IsDark: true, Primary: "Indigo", Secondary: "Pink");

            Assert.Equal(a, b);
            Assert.True(a == b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

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
