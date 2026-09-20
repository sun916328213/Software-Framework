using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Extension.Core.Reflection;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// 反射用的样本类型。放在文件级（而不是嵌套在测试类里）是为了满足
    /// <c>where T : new()</c> 约束下的可见性要求。
    /// </summary>
    internal sealed class ReflectionTestModel
    {
        public int IntProp { get; set; }
        public string StrProp { get; set; }
        public double DblProp { get; set; }
        public decimal DecProp { get; set; }
        public long LongProp { get; set; }
        public bool BoolProp { get; set; }
        public DateTime DateProp { get; set; }
        public int? NullableInt { get; set; }

        /// <summary>get-only ⇒ PropertyInfo.CanWrite == false</summary>
        public int ReadOnlyProp { get; } = 7;

        /// <summary>是字段不是属性，反射的属性查找看不到它（用例只用 nameof，不读它的值）</summary>
        public int Field = 0;
    }

    /// <summary>
    /// T0 层「反射」专项：<c>Extension.Core.Reflection.ReflectionEx</c>。
    /// 该类被下游用于"从配置/字典批量填对象"，此前零用例覆盖。
    /// 重点是把 <c>ConvertValue</c> 的类型转换契约和几处**静默行为**钉住。
    /// </summary>
    public class ReflectionExTests
    {
        // ══════════════════════════════════════════════════════════════════
        // API 面
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void API面_ReflectionEx是普通类而不是静态类_与同目录其他Ex不同()
        {
            // ⚠️ 全仓其他 *Ex（HexEx / BinaryEx / BoolEx / DecimalEx / DateTimeEx / RegexEx /
            //    StringEx / ByteEx / ListEx …）都是 static class，只有 ReflectionEx 是实例类，
            //    调用方必须先 new。这是分级时暴露的 API 面不一致（未改，只记录）。
            Assert.NotNull(Activator.CreateInstance(typeof(ReflectionEx)));
            Assert.False(typeof(ReflectionEx).IsAbstract && typeof(ReflectionEx).IsSealed);
        }

        [Fact]
        public void API面_公开实例方法一共六个()
        {
            // 把 API 面钉住：任何增删都会让这条用例变红，迫使改动者同步更新本文件与文档。
            var names = typeof(ReflectionEx)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(m => m.Name)
                .OrderBy(n => n)
                .ToArray();

            Assert.Equal(
                new[]
                {
                    "CreateInstance", "CreateInstance",
                    "GetProperties", "GetProperty",
                    "SetProperties", "SetProperty",
                }.OrderBy(n => n).ToArray(),
                names);

            Assert.All(
                typeof(ReflectionEx).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => m.Name == "CreateInstance"),
                m =>
                {
                    Assert.True(m.IsGenericMethodDefinition);
                    Assert.Single(m.GetGenericArguments());
                    // new() 约束不会出现在 GetGenericParameterConstraints() 里，
                    // 它在 GenericParameterAttributes 上，这里只确认约束存在性可被观察到。
                    Assert.NotNull(m.GetGenericArguments()[0]);
                });
        }

        // ══════════════════════════════════════════════════════════════════
        // SetProperty
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void SetProperty_同类型直接赋值()
        {
            var ex = new ReflectionEx();
            var m = new ReflectionTestModel();

            ex.SetProperty(m, nameof(ReflectionTestModel.IntProp), 42);

            Assert.Equal(42, m.IntProp);
        }

        [Fact]
        public void SetProperty_字符串自动转数值()
        {
            var ex = new ReflectionEx();
            var m = new ReflectionTestModel();

            ex.SetProperty(m, nameof(ReflectionTestModel.IntProp), "123");
            ex.SetProperty(m, nameof(ReflectionTestModel.LongProp), "9999999999");
            ex.SetProperty(m, nameof(ReflectionTestModel.DblProp), "1.5");
            ex.SetProperty(m, nameof(ReflectionTestModel.DecProp), "2.5");

            Assert.Equal(123, m.IntProp);
            Assert.Equal(9999999999L, m.LongProp);
            Assert.Equal(1.5, m.DblProp);
            Assert.Equal(2.5m, m.DecProp);
        }

        [Fact]
        public void SetProperty_数值自动加宽或收窄()
        {
            var ex = new ReflectionEx();
            var m = new ReflectionTestModel();

            ex.SetProperty(m, nameof(ReflectionTestModel.IntProp), 5L);       // long -> int
            Assert.Equal(5, m.IntProp);

            ex.SetProperty(m, nameof(ReflectionTestModel.DblProp), 3);        // int -> double
            Assert.Equal(3.0, m.DblProp);
        }

        [Fact]
        public void SetProperty_字符串与bool与DateTime()
        {
            var ex = new ReflectionEx();
            var m = new ReflectionTestModel();

            ex.SetProperty(m, nameof(ReflectionTestModel.BoolProp), "true");
            Assert.True(m.BoolProp);

            ex.SetProperty(m, nameof(ReflectionTestModel.DateProp), "2026-09-18");
            Assert.Equal(new DateTime(2026, 9, 18), m.DateProp);

            // 目标类型是 string ⇒ 一律 ToString()
            ex.SetProperty(m, nameof(ReflectionTestModel.StrProp), 12345);
            Assert.Equal("12345", m.StrProp);
        }

        [Fact]
        public void SetProperty_值类型给null得到默认值()
        {
            var ex = new ReflectionEx();
            var m = new ReflectionTestModel { IntProp = 9 };

            ex.SetProperty(m, nameof(ReflectionTestModel.IntProp), null);

            Assert.Equal(0, m.IntProp);
        }

        [Fact]
        public void SetProperty_可空类型给null得到null而不是0()
        {
            var ex = new ReflectionEx();
            var m = new ReflectionTestModel();

            ex.SetProperty(m, nameof(ReflectionTestModel.NullableInt), "5");
            Assert.Equal(5, m.NullableInt);

            // ConvertValue 对 null 走 Activator.CreateInstance(typeof(int?))，
            // 而"无值的 Nullable<T>"装箱结果就是 null ⇒ 属性被置为 null（不是 0）。
            ex.SetProperty(m, nameof(ReflectionTestModel.NullableInt), null);
            Assert.Null(m.NullableInt);
        }

        [Fact]
        public void SetProperty_参数校验()
        {
            var ex = new ReflectionEx();
            var m = new ReflectionTestModel();

            Assert.Throws<ArgumentNullException>(() => ex.SetProperty((ReflectionTestModel)null, "IntProp", 1));
            Assert.Throws<ArgumentException>(() => ex.SetProperty(m, "NoSuchProp", 1));
            Assert.Throws<ArgumentException>(() => ex.SetProperty(m, nameof(ReflectionTestModel.ReadOnlyProp), 1));
            Assert.Throws<ArgumentException>(() => ex.SetProperty(m, nameof(ReflectionTestModel.Field), 1)); // 字段不算属性
            Assert.Throws<ArgumentException>(() => ex.SetProperty(m, "", 1));
            Assert.Throws<ArgumentException>(() => ex.SetProperty(m, null, 1));
        }

        [Fact]
        public void SetProperty_无法转换时包成InvalidOperationException()
        {
            var ex = new ReflectionEx();
            var m = new ReflectionTestModel();

            // "abc" -> int：ConvertValue 捕获 FormatException 后**返回原始值**（字符串），
            // 随后 property.SetValue 因类型不匹配抛 ArgumentException，
            // 最终被 SetPropertyValue 统一包成 InvalidOperationException。
            var e = Record.Exception(() => ex.SetProperty(m, nameof(ReflectionTestModel.IntProp), "abc"));

            Assert.NotNull(e);
            Assert.IsType<InvalidOperationException>(e);
        }

        [Fact]
        public void SetProperty_用的是typeof_T而不是运行时类型()
        {
            // ⚠️ 实现里是 `Type type = typeof(T)`，不是 obj.GetType()。
            //    以 object / 基类的静态类型传入派生实例时，属性查找必然失败。
            //    这是真实存在的易用性陷阱（未改，只记录）。
            var ex = new ReflectionEx();
            object boxed = new ReflectionTestModel();

            Assert.Throws<ArgumentException>(
                () => ex.SetProperty(boxed, nameof(ReflectionTestModel.IntProp), 1));

            // 同一实例，用正确的静态类型就没问题
            var typed = new ReflectionTestModel();
            ex.SetProperty(typed, nameof(ReflectionTestModel.IntProp), 1);
            Assert.Equal(1, typed.IntProp);
        }

        // ══════════════════════════════════════════════════════════════════
        // GetProperty / GetProperties
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void GetProperty_取值与参数校验()
        {
            var ex = new ReflectionEx();
            var m = new ReflectionTestModel { IntProp = 7, StrProp = "hi" };

            Assert.Equal(7, ex.GetProperty(m, nameof(ReflectionTestModel.IntProp)));
            Assert.Equal("hi", ex.GetProperty(m, nameof(ReflectionTestModel.StrProp)));

            Assert.Throws<ArgumentNullException>(() => ex.GetProperty((ReflectionTestModel)null, "IntProp"));
            Assert.Throws<ArgumentException>(() => ex.GetProperty(m, "NoSuchProp"));
            Assert.Throws<ArgumentException>(() => ex.GetProperty(m, ""));
        }

        [Fact]
        public void GetProperties_含只读属性但不含字段()
        {
            var ex = new ReflectionEx();
            var m = new ReflectionTestModel { IntProp = 1, StrProp = "x" };

            var props = ex.GetProperties(m);

            Assert.Equal(1, (int)props[nameof(ReflectionTestModel.IntProp)]);
            Assert.Equal("x", (string)props[nameof(ReflectionTestModel.StrProp)]);

            // get-only 属性也是公共属性，会被收集
            Assert.True(props.ContainsKey(nameof(ReflectionTestModel.ReadOnlyProp)));
            Assert.Equal(7, (int)props[nameof(ReflectionTestModel.ReadOnlyProp)]);

            // 公共字段不是属性，不会被收集
            Assert.False(props.ContainsKey(nameof(ReflectionTestModel.Field)));

            Assert.Throws<ArgumentNullException>(() => ex.GetProperties((ReflectionTestModel)null));
        }

        [Fact]
        public void GetProperties_可空属性为null时字典里存的是null()
        {
            var ex = new ReflectionEx();
            var props = ex.GetProperties(new ReflectionTestModel());

            Assert.True(props.ContainsKey(nameof(ReflectionTestModel.NullableInt)));
            Assert.Null(props[nameof(ReflectionTestModel.NullableInt)]);
        }

        // ══════════════════════════════════════════════════════════════════
        // SetProperties —— 与 SetProperty 的"静默"差异
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void SetProperties_静默跳过未知属性_与SetProperty的抛异常语义相反()
        {
            var ex = new ReflectionEx();
            var m = new ReflectionTestModel();

            ex.SetProperties(m, new Dictionary<string, object>
            {
                [nameof(ReflectionTestModel.IntProp)] = 5,
                ["NoSuchProp"] = 1,                              // 不存在 ⇒ 静默跳过
                [nameof(ReflectionTestModel.ReadOnlyProp)] = 1,  // 只读 ⇒ 静默跳过
                [nameof(ReflectionTestModel.Field)] = 1,         // 字段 ⇒ 静默跳过
            });

            Assert.Equal(5, m.IntProp);
            Assert.Equal(7, m.ReadOnlyProp);                     // 没被改写
        }

        [Fact]
        public void SetProperties_null或空字典是noop()
        {
            var ex = new ReflectionEx();
            var m = new ReflectionTestModel();

            ex.SetProperties(m, null);                                       // 不抛
            ex.SetProperties(m, new Dictionary<string, object>());           // 不抛

            Assert.Equal(0, m.IntProp);
        }

        [Fact]
        public void SetProperties_其中一个值转换失败时整体抛InvalidOperationException()
        {
            var ex = new ReflectionEx();
            var m = new ReflectionTestModel();

            // SetProperties 不吞异常：转换失败照样冒到 SetPropertyValue 的包装里
            Assert.Throws<InvalidOperationException>(() => ex.SetProperties(m, new Dictionary<string, object>
            {
                [nameof(ReflectionTestModel.IntProp)] = "abc",
            }));
        }

        // ══════════════════════════════════════════════════════════════════
        // CreateInstance
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void CreateInstance_无参重载就是new_T()
        {
            var ex = new ReflectionEx();

            var a = ex.CreateInstance<ReflectionTestModel>();
            var b = ex.CreateInstance<ReflectionTestModel>();

            Assert.NotNull(a);
            Assert.NotNull(b);
            Assert.NotSame(a, b);                    // 每次都是新实例
            Assert.Equal(0, a.IntProp);
            Assert.Equal(7, a.ReadOnlyProp);
        }

        [Fact]
        public void CreateInstance_带字典重载_创建后立即填值()
        {
            var ex = new ReflectionEx();

            var m = ex.CreateInstance<ReflectionTestModel>(new Dictionary<string, object>
            {
                [nameof(ReflectionTestModel.IntProp)] = 11,
                [nameof(ReflectionTestModel.StrProp)] = "s",
                ["NoSuchProp"] = "ignored",
            });

            Assert.Equal(11, m.IntProp);
            Assert.Equal("s", m.StrProp);
        }

        [Fact]
        public void CreateInstance_带null字典等价于无参()
        {
            var ex = new ReflectionEx();
            var m = ex.CreateInstance<ReflectionTestModel>(null);

            Assert.NotNull(m);
            Assert.Equal(0, m.IntProp);
        }

        // ══════════════════════════════════════════════════════════════════
        // 往返
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void GetProperties到SetProperties往返_值保持不变()
        {
            var ex = new ReflectionEx();
            var src = new ReflectionTestModel
            {
                IntProp = 3,
                StrProp = "abc",
                DblProp = 2.25,
                BoolProp = true,
                DateProp = new DateTime(2026, 1, 2, 3, 4, 5),
                NullableInt = 8,
            };

            var snapshot = ex.GetProperties(src);
            var dst = ex.CreateInstance<ReflectionTestModel>(snapshot);

            Assert.Equal(src.IntProp, dst.IntProp);
            Assert.Equal(src.StrProp, dst.StrProp);
            Assert.Equal(src.DblProp, dst.DblProp);
            Assert.Equal(src.BoolProp, dst.BoolProp);
            Assert.Equal(src.DateProp, dst.DateProp);
            Assert.Equal(src.NullableInt, dst.NullableInt);
        }
    }
}
