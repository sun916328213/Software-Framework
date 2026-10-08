// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：CloneExTests、Inner、Model。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Reflection、System.Runtime.CompilerServices、Extension.Core.Buffers、Extension.IO.Compression。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Extension.Core.Buffers;
using Extension.IO.Compression;
using Extension.Integration.Cloning;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// 「深拷贝」专项：<c>Extension.Integration.Cloning.CloneEx</c>（12 行，唯一依赖 <c>FastDeepCloner</c> ⇒ 归 T2）。
    /// <para>
    /// 实现只有一句 <c>((object)obj).Clone()</c>，全部语义都来自 <c>FastDeepCloner</c> 的
    /// <c>Clone()</c> 扩展方法。这里钉住的是「引用隔离」这一条对外承诺
    /// —— 只要哪天换库/改成 <c>MemberwiseClone</c>（浅拷贝），下面的 <c>NotSame</c> 立刻变红。
    /// </para>
    /// </summary>
    public class CloneExTests
    {
        /// <summary>
        /// Inner 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        internal sealed class Inner
        {
            public int Value { get; set; }
        }

        /// <summary>
        /// Model 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        internal sealed class Model
        {
            public string Name { get; set; }
            public Inner Inner { get; set; }
            public List<int> Items { get; set; }
        }

        /// <summary>
        /// 测试相关入口 NewModel；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        private static Model NewModel()
        {
            return new Model
            {
                Name = "n",
                Inner = new Inner { Value = 7 },
                Items = new List<int> { 1, 2, 3 },
            };
        }

        /// <summary>
        /// 测试相关入口 DeepClone_对象_嵌套引用不共享；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 NewModel、DeepClone、NotSame、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void DeepClone_对象_嵌套引用不共享()
        {
            Model original = NewModel();

            Model copy = original.DeepClone();

            Assert.NotSame(original, copy);
            Assert.NotSame(original.Inner, copy.Inner);
            Assert.NotSame(original.Items, copy.Items);
            Assert.Equal("n", copy.Name);
            Assert.Equal(7, copy.Inner.Value);
            Assert.Equal(new[] { 1, 2, 3 }, copy.Items);
        }

        /// <summary>
        /// 测试相关入口 DeepClone_改副本不影响原件；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 NewModel、DeepClone、Add、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void DeepClone_改副本不影响原件()
        {
            Model original = NewModel();

            Model copy = original.DeepClone();
            copy.Name = "changed";
            copy.Inner.Value = 99;
            copy.Items.Add(4);

            Assert.Equal("n", original.Name);
            Assert.Equal(7, original.Inner.Value);
            Assert.Equal(3, original.Items.Count);
        }

        /// <summary>
        /// 测试相关入口 DeepClone_集合_得到独立实例；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 DeepClone、NotSame、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void DeepClone_集合_得到独立实例()
        {
            var original = new List<int> { 1, 2, 3 };

            List<int> copy = original.DeepClone();

            Assert.NotSame(original, copy);
            Assert.Equal(new[] { 1, 2, 3 }, copy);
        }

        /// <summary>
        /// 测试相关入口 DeepClone_字符串作为根对象会抛ArgumentException；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Exception、DeepClone、IsAssignableFrom、Contains；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void DeepClone_字符串作为根对象会抛ArgumentException()
        {
            // ⚠️ 缺陷（实测）：FastDeepCloner 对 string 走「创建未初始化实例」的快速路径，
            // 而 CLR 不允许这样构造 string ⇒ 抛
            //   ArgumentException: Uninitialized Strings cannot be created.
            // 注意与上一条用例的区别：string 作为**属性**（Model.Name）能正常克隆
            // （走 ReferenceClone 的属性赋值路径），只有 string 作为**克隆根**才炸。
            const string original = "abc";

            Exception ex = Record.Exception(() => original.DeepClone());

            Assert.IsAssignableFrom<ArgumentException>(ex);
            Assert.Contains("String", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 测试相关入口 DeepClone_null引用_原样返回null且不抛；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Null、Exception、DeepClone；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void DeepClone_null引用_原样返回null且不抛()
        {
            // 记录：FastDeepCloner 对 null 输入直接原样返回（null 进 null 出），不抛异常。
            // 好处是调用方不用先判空；代价是"对象意外为 null"不会在这里暴露出来。
            Assert.Null(Record.Exception(() => ((object)null).DeepClone()));
            Assert.Null(((object)null).DeepClone());
        }

        /// <summary>
        /// 测试相关入口 API面_DeepClone是泛型扩展方法；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 GetMethod、NotNull、True、IsDefined、Single；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void API面_DeepClone是泛型扩展方法()
        {
            MethodInfo m = typeof(CloneEx).GetMethod("DeepClone", BindingFlags.Public | BindingFlags.Static);

            Assert.NotNull(m);
            Assert.True(m.IsDefined(typeof(ExtensionAttribute), inherit: false));
            Assert.True(m.IsGenericMethodDefinition);
            Assert.Single(m.GetGenericArguments());
            Assert.True(m.ReturnType.IsGenericParameter);   // 返回 T 本身
        }

        /// <summary>
        /// 测试相关入口 CloneEx_是静态类_1个公开静态方法；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 True、Single、GetMethods、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void CloneEx_是静态类_1个公开静态方法()
        {
            Type t = typeof(CloneEx);

            Assert.True(t.IsAbstract && t.IsSealed);
            Assert.Single(t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
            Assert.Equal("Extension.Integration.Cloning", t.Namespace);
        }
    }
}
