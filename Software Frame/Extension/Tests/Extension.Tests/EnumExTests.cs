// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SampleEnum、EnumExTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.ComponentModel、System.Linq、Extension.Core.Enums、Xunit。导入不代表所有类型都会在运行时加载。
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

        /// <summary>
        /// 验证 GetDescription_有Description特性时返回描述 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、GetDescription；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetDescription_有Description特性时返回描述()
        {
            Assert.Equal("第一项", SampleEnum.First.GetDescription());
            Assert.Equal("第二项", SampleEnum.Second.GetDescription());
        }

        /// <summary>
        /// 验证 GetDescription_没有Description特性时回退成名称 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、GetDescription；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetDescription_没有Description特性时回退成名称()
        {
            Assert.Equal("NoDescription", SampleEnum.NoDescription.GetDescription());
        }

        /// <summary>
        /// 验证 GetDescription_空描述返回空串而不是名称 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、GetDescription；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetDescription_空描述返回空串而不是名称()
        {
            // ⚠️ 有特性但 Description 为空串 ⇒ 返回 ""，**不会**回退成名称。
            //    这与"没有特性"的行为不同（那个会回退），是一个易被误判的分支。
            Assert.Equal(string.Empty, SampleEnum.EmptyDescription.GetDescription());
        }

        /// <summary>
        /// 验证 GetDescription_未定义的枚举值返回空串 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、GetDescription；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetDescription_未定义的枚举值返回空串()
        {
            // Enum.GetName 对没有对应常量的值返回 null ⇒ 方法返回 ""
            Assert.Equal(string.Empty, ((SampleEnum)99).GetDescription());
            Assert.Equal(string.Empty, ((SampleEnum)(-1)).GetDescription());
        }

        /// <summary>
        /// 验证 GetDescription_null返回空串不抛 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、GetDescription；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetDescription_null返回空串不抛()
        {
            // Enum 是引用类型，可以传 null；实现里显式判了 null
            Assert.Equal(string.Empty, EnumEx.GetDescription((Enum)null));
        }

        /// <summary>
        /// 验证 GetDescription_两个重载对同一个值给出一致结果 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 GetDescription、Equal；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 GetName_按声明顺序返回全部名称 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、GetName；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetName_按声明顺序返回全部名称()
        {
            Assert.Equal(
                new[] { "First", "Second", "NoDescription", "EmptyDescription" },
                EnumEx.GetName<SampleEnum>());
        }

        /// <summary>
        /// 验证 GetEnum_按名称解析 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、GetEnum；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetEnum_按名称解析()
        {
            Assert.Equal(SampleEnum.Second, "Second".GetEnum<SampleEnum>());
            Assert.Equal(SampleEnum.First, "First".GetEnum<SampleEnum>());
        }

        /// <summary>
        /// 验证 GetEnum_区分大小写 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Throws、GetEnum；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetEnum_区分大小写()
        {
            // Enum.Parse 默认 **区分大小写** ⇒ "second" 解析失败
            Assert.Throws<ArgumentException>(() => "second".GetEnum<SampleEnum>());
        }

        /// <summary>
        /// 验证 GetEnum_名称不存在时抛ArgumentException_null时抛ArgumentNullException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Throws、GetEnum；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetEnum_名称不存在时抛ArgumentException_null时抛ArgumentNullException()
        {
            // 两种异常类型不同：名称找不到是 ArgumentException，传 null 是 ArgumentNullException
            // （后者派生自前者，所以 Assert.Throws<ArgumentException> 这种精确匹配抓不到它）
            Assert.Throws<ArgumentException>(() => "NoSuchName".GetEnum<SampleEnum>());
            Assert.Throws<ArgumentNullException>(() => ((string)null).GetEnum<SampleEnum>());
        }

        /// <summary>
        /// 验证 GetEnum_传入纯数字字符串按底层值解析_不校验是否为已命名常量 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、GetEnum、Exception、Null；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 GetAllDescriptions_返回值与描述的全量对照 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 GetAllDescriptions、Equal、ToArray、Select；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 GetAllDescriptions_顺序与声明顺序一致 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ToArray、Select、GetAllDescriptions、Equal；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 验证 API面_GetDescription有泛型与Enum两个重载 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ToArray、Where、GetMethods、Equal、Single；具体条件和先后次序见方法体。
        /// </summary>
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
