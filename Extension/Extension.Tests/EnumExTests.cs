using System;
using System.ComponentModel;
using System.Linq;
using Extension.Core.Enums;
using Xunit;

namespace Extension.Tests
{
    /// <summary>枚举样本：覆盖"有描述 / 无描述 / 空描述"三种情况。</summary>
    internal enum SampleEnum
    {
        [Description("第一项")]
        First = 0,

        [Description("第二项")]
        Second = 1,

        /// <summary>没有 Description 特性 ⇒ 应回退成名称</summary>
        NoDescription = 2,

        [Description("")]
        EmptyDescription = 3,
    }

    /// <summary>
    /// T0 层「枚举」专项：<c>Extension.Core.Enums.EnumEx</c>。
    /// 全仓用来把枚举翻成中文界面的唯一通路，此前零用例覆盖。
    /// </summary>
    public class EnumExTests
    {
        // ══════════════════════════════════════════════════════════════════
        // GetDescription
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void GetDescription_有Description特性时返回描述()
        {
            Assert.Equal("第一项", SampleEnum.First.GetDescription());
            Assert.Equal("第二项", SampleEnum.Second.GetDescription());
        }

        [Fact]
        public void GetDescription_没有Description特性时回退成名称()
        {
            Assert.Equal("NoDescription", SampleEnum.NoDescription.GetDescription());
        }

        [Fact]
        public void GetDescription_空描述返回空串而不是名称()
        {
            // ⚠️ 有特性但 Description 为空串 ⇒ 返回 ""，**不会**回退成名称。
            //    这与"没有特性"的行为不同（那个会回退），是一个易被误判的分支。
            Assert.Equal(string.Empty, SampleEnum.EmptyDescription.GetDescription());
        }

        [Fact]
        public void GetDescription_未定义的枚举值返回空串()
        {
            // Enum.GetName 对没有对应常量的值返回 null ⇒ 方法返回 ""
            Assert.Equal(string.Empty, ((SampleEnum)99).GetDescription());
            Assert.Equal(string.Empty, ((SampleEnum)(-1)).GetDescription());
        }

        [Fact]
        public void GetDescription_null返回空串不抛()
        {
            // Enum 是引用类型，可以传 null；实现里显式判了 null
            Assert.Equal(string.Empty, EnumEx.GetDescription((Enum)null));
        }

        [Fact]
        public void GetDescription_两个重载对同一个值给出一致结果()
        {
            // 泛型重载 (this T where T : Enum) 与实例重载 (this Enum) 都存在，
            // 前者按"恒等转换优于装箱"胜出；这里确认两条路结果一致。
            var byGeneric = EnumEx.GetDescription(SampleEnum.Second);
            var byEnum = EnumEx.GetDescription((Enum)SampleEnum.Second);

            Assert.Equal("第二项", byGeneric);
            Assert.Equal(byGeneric, byEnum);
        }

        // ══════════════════════════════════════════════════════════════════
        // GetName / GetEnum
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void GetName_按声明顺序返回全部名称()
        {
            Assert.Equal(
                new[] { "First", "Second", "NoDescription", "EmptyDescription" },
                EnumEx.GetName<SampleEnum>());
        }

        [Fact]
        public void GetEnum_按名称解析()
        {
            Assert.Equal(SampleEnum.Second, "Second".GetEnum<SampleEnum>());
            Assert.Equal(SampleEnum.First, "First".GetEnum<SampleEnum>());
        }

        [Fact]
        public void GetEnum_区分大小写()
        {
            // Enum.Parse 默认 **区分大小写** ⇒ "second" 解析失败
            Assert.Throws<ArgumentException>(() => "second".GetEnum<SampleEnum>());
        }

        [Fact]
        public void GetEnum_名称不存在时抛ArgumentException_null时抛ArgumentNullException()
        {
            // 两种异常类型不同：名称找不到是 ArgumentException，传 null 是 ArgumentNullException
            // （后者派生自前者，所以 Assert.Throws<ArgumentException> 这种精确匹配抓不到它）
            Assert.Throws<ArgumentException>(() => "NoSuchName".GetEnum<SampleEnum>());
            Assert.Throws<ArgumentNullException>(() => ((string)null).GetEnum<SampleEnum>());
        }

        [Fact]
        public void GetEnum_传入纯数字字符串按底层值解析_不校验是否为已命名常量()
        {
            // ⚠️ Enum.Parse 的既有行为：以数字开头的字符串走**数值**分支，按底层 int 解析。
            //    它**不会**检查该数值是否是枚举里已命名的常量 ⇒ "99" 也能过。
            //    ⇒ 若用它做"用户输入校验"，非法输入挡不住。
            Assert.Equal(SampleEnum.Second, "1".GetEnum<SampleEnum>());

            var ex = Record.Exception(() => "99".GetEnum<SampleEnum>());
            Assert.Null(ex);
            Assert.Equal((SampleEnum)99, "99".GetEnum<SampleEnum>());
        }

        // ══════════════════════════════════════════════════════════════════
        // GetAllDescriptions
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void GetAllDescriptions_返回值与描述的全量对照()
        {
            var all = EnumEx.GetAllDescriptions<SampleEnum>();

            Assert.Equal(4, all.Length);
            Assert.Equal(
                new[] { SampleEnum.First, SampleEnum.Second, SampleEnum.NoDescription, SampleEnum.EmptyDescription },
                all.Select(x => x.Value).ToArray());
            Assert.Equal(new[] { "第一项", "第二项", "NoDescription", "" }, all.Select(x => x.Description).ToArray());
        }

        [Fact]
        public void GetAllDescriptions_顺序与声明顺序一致()
        {
            // Enum.GetValues 对连续值按底层值升序返回，这里确认这个前提成立
            var values = EnumEx.GetAllDescriptions<SampleEnum>().Select(x => (int)x.Value).ToArray();
            Assert.Equal(new[] { 0, 1, 2, 3 }, values);
        }

        // ══════════════════════════════════════════════════════════════════
        // API 面
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void API面_EnumEx是静态类_五个公开方法()
        {
            Assert.True(typeof(EnumEx).IsAbstract && typeof(EnumEx).IsSealed);

            var names = typeof(EnumEx)
                .GetMethods(System.Reflection.BindingFlags.Public
                            | System.Reflection.BindingFlags.Static
                            | System.Reflection.BindingFlags.DeclaredOnly)
                .Select(m => m.Name)
                .OrderBy(n => n)
                .ToArray();

            Assert.Equal(
                new[] { "GetAllDescriptions", "GetDescription", "GetDescription", "GetEnum", "GetName" }
                    .OrderBy(n => n).ToArray(),
                names);
        }

        [Fact]
        public void API面_GetDescription有泛型与Enum两个重载()
        {
            var overloads = typeof(EnumEx)
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(m => m.Name == "GetDescription")
                .ToArray();

            Assert.Equal(2, overloads.Length);
            Assert.Single(overloads, m => m.IsGenericMethodDefinition);
            Assert.Single(overloads, m => !m.IsGenericMethodDefinition);
        }
    }
}
