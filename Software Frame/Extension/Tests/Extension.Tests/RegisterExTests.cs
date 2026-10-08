// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：RegisterExTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Linq、System.Reflection、System.Text、Extension.Core.Conversion。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Extension.Core.Conversion;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// T0 层「寄存器编解码」专项：<c>Extension.Core.Conversion.RegisterEx</c>（16 个扩展方法）。
    /// 这是 PLC / Modbus 寄存器数组与 int/float/double/string 之间的**唯一桥梁**，
    /// 此前零用例覆盖。本文件覆盖三组能力：
    /// <list type="bullet">
    /// <item>short → bool[] / string（位视图）</item>
    /// <item>short[] → int[] / float[] / double[] / string[]（拆包）</item>
    /// <item>int / float / double / string → short[]（打包）</item>
    /// </list>
    /// 并把 4 处**只在运行时暴露**的语义陷阱固化成可见用例（硬规则 1：只记录不顺手改）：
    /// <list type="number">
    /// <item><c>ToShortArray(this string)</c> 末尾 <c>.Where(x =&gt; x != 0)</c> 会吞掉**所有 0 值**，
    /// 内嵌 NUL 与 U+10000 这类「低字恰好为 0」的码位会被静默破坏；</item>
    /// <item><c>ToStringArray</c> 走 UTF8 逐 2 字节解码，与 <c>string.ToShortArray</c>（UTF32）**不是互逆**，
    /// 且非法字节序列会被替换成 U+FFFD（不可逆）；</item>
    /// <item><c>ToIntArray</c> / <c>ToFloatArray</c> / <c>ToDoubleArray</c> 的**长度校验档位各不相同**
    /// （2 的倍数 / 2 的倍数 / 4 的倍数），且 null/空在**校验之前**返回空数组；</item>
    /// <item><c>ToBoolArray</c> 的 <c>1 &lt;&lt; i</c> 在 i ≥ 16 时按 **int 提升**取值，
    /// 负 short 的高位会给出 true（与「16 位补码」直觉一致，但超过 16 位就变成 32 位语义）。</item>
    /// </list>
    /// 所有调用一律**显式静态调用**（<c>RegisterEx.Xxx(...)</c>），
    /// 避免与 <c>ByteEx</c> / <c>BinaryEx</c> / <c>ASCIIEx</c> 的同名扩展方法发生 <c>CS0121</c> 二义性。
    /// </summary>
    public class RegisterExTests
    {
        // ══════════════════════════════════════════════════════════════════
        // short → bool[]（位视图，LSB 在前）
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void ToBoolArray_默认长度16_LSB在前()
        {
            // 0x00A5 = 0b1010_0101 → bit0..7 = 1,0,1,0,0,1,0,1
            bool[] r = RegisterEx.ToBoolArray((short)0x00A5);

            Assert.Equal(16, r.Length);
            Assert.Equal(new[] { true, false, true, false, false, true, false, true },
                         r.Take(8).ToArray());
            Assert.All(r.Skip(8), b => Assert.False(b));
        }

        /// <summary>
        /// 测试相关入口 ToBoolArray_长度参数可自定义_短于16位只取低位；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToBoolArray、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToBoolArray_长度参数可自定义_短于16位只取低位()
        {
            bool[] r = RegisterEx.ToBoolArray((short)0x00A5, 4);

            Assert.Equal(4, r.Length);
            // 只取低 4 位：0101 → LSB 在前 = 1,0,1,0
            Assert.Equal(new[] { true, false, true, false }, r);
        }

        /// <summary>
        /// 测试相关入口 ToBoolArray_长度0返回空数组；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Empty、ToBoolArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToBoolArray_长度0返回空数组()
        {
            Assert.Empty(RegisterEx.ToBoolArray((short)123, 0));
        }

        /// <summary>
        /// 测试相关入口 ToBoolArray_负数16位_补码全1；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 All、ToBoolArray、True、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToBoolArray_负数16位_补码全1()
        {
            Assert.All(RegisterEx.ToBoolArray((short)-1), b => Assert.True(b));
            // -2 = 0xFFFE → 只有 bit0 为 false
            Assert.Equal(new[] { false, true, true, true }, RegisterEx.ToBoolArray((short)-2, 4));
        }

        /// <summary>
        /// 测试相关入口 ToBoolArray_长度超过16位时_高位按int提升后的补码给出true；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToBoolArray、Equal、All、True、Skip；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToBoolArray_长度超过16位时_高位按int提升后的补码给出true()
        {
            // (short)-1 提升为 int 是 0xFFFFFFFF ⇒ i ≥ 16 时 (1 << i) 仍命中
            bool[] all32 = RegisterEx.ToBoolArray((short)-1, 32);
            Assert.Equal(32, all32.Length);
            Assert.All(all32, b => Assert.True(b));

            // 正数的高位补 0
            bool[] one32 = RegisterEx.ToBoolArray((short)1, 32);
            Assert.True(one32[0]);
            Assert.All(one32.Skip(1), b => Assert.False(b));
        }

        /// <summary>
        /// 测试相关入口 ToBoolArray_负长度会抛异常；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 NotNull、Exception、ToBoolArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToBoolArray_负长度会抛异常()
        {
            // new bool[-1] 的异常类型由运行时决定，这里只钉住「不静默返回空」
            Assert.NotNull(Record.Exception(() => RegisterEx.ToBoolArray((short)1, -1)));
        }

        /// <summary>
        /// 测试相关入口 ToBoolArray_与ToBinaryArray的字符串正好互为逆序；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToBinaryArray、Concat、Select、Reverse、ToBoolArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToBoolArray_与ToBinaryArray的字符串正好互为逆序()
        {
            short[] samples = { 0, 1, -1, 0x1234, short.MinValue, short.MaxValue };

            foreach (short s in samples)
            {
                string msbFirst = RegisterEx.ToBinaryArray(s);
                string fromBools = string.Concat(RegisterEx.ToBoolArray(s).Reverse().Select(b => b ? '1' : '0'));

                Assert.Equal(16, msbFirst.Length);
                Assert.Equal(msbFirst, fromBools);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // short[] → List<bool[]>
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 测试相关入口 ToList_BoolArray_逐元素转换且顺序保持；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToList_BoolArray、Equal、True、All、False；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToList_BoolArray_逐元素转换且顺序保持()
        {
            List<bool[]> r = RegisterEx.ToList_BoolArray(new short[] { 1, 0, -1 });

            Assert.Equal(3, r.Count);
            Assert.Equal(16, r[0].Length);
            Assert.True(r[0][0]);
            Assert.All(r[1], b => Assert.False(b));
            Assert.All(r[2], b => Assert.True(b));
        }

        /// <summary>
        /// 测试相关入口 ToList_BoolArray_长度参数对每个元素都生效；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToList_BoolArray、Equal、All；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToList_BoolArray_长度参数对每个元素都生效()
        {
            List<bool[]> r = RegisterEx.ToList_BoolArray(new short[] { 1, 2 }, 8);

            Assert.Equal(2, r.Count);
            Assert.All(r, x => Assert.Equal(8, x.Length));
            Assert.Equal(new[] { true, false, false, false, false, false, false, false }, r[0]);
            Assert.Equal(new[] { false, true, false, false, false, false, false, false }, r[1]);
        }

        /// <summary>
        /// 测试相关入口 ToList_BoolArray_空数组返回空列表；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Empty、ToList_BoolArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToList_BoolArray_空数组返回空列表()
        {
            Assert.Empty(RegisterEx.ToList_BoolArray(Array.Empty<short>()));
        }

        /// <summary>
        /// 测试相关入口 ToList_BoolArray_null会抛NullReferenceException_没有null保护；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Throws、ToList_BoolArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToList_BoolArray_null会抛NullReferenceException_没有null保护()
        {
            // 对比：ToIntArray / ToFloatArray / ToDoubleArray 都做了 null 保护，本方法没有
            Assert.Throws<NullReferenceException>(() => RegisterEx.ToList_BoolArray(null));
        }

        // ══════════════════════════════════════════════════════════════════
        // short / short[] → 二进制字符串（MSB 在前，16 字符）
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void ToBinaryArray_short_是16字符MSB在前的字符串()
        {
            Assert.Equal("0001001000110100", RegisterEx.ToBinaryArray((short)0x1234));
            Assert.Equal("0000000000000000", RegisterEx.ToBinaryArray((short)0));
            Assert.Equal("0000000000000001", RegisterEx.ToBinaryArray((short)1));
        }

        /// <summary>
        /// 测试相关入口 ToBinaryArray_short_负数与边界值_取低16位补码；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ToBinaryArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToBinaryArray_short_负数与边界值_取低16位补码()
        {
            // Convert.ToString(-1, 2) 会先给出 32 个 1，再被 PaddingLength 截取低 16 位
            Assert.Equal("1111111111111111", RegisterEx.ToBinaryArray((short)-1));
            Assert.Equal("1111111111111110", RegisterEx.ToBinaryArray((short)-2));
            Assert.Equal("1000000000000000", RegisterEx.ToBinaryArray(short.MinValue));
            Assert.Equal("0111111111111111", RegisterEx.ToBinaryArray(short.MaxValue));
        }

        /// <summary>
        /// 测试相关入口 ToBinaryArray_short数组_逐元素且顺序保持；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToBinaryArray、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToBinaryArray_short数组_逐元素且顺序保持()
        {
            string[] r = RegisterEx.ToBinaryArray(new short[] { 1, 0x1234 });

            Assert.Equal(2, r.Length);
            Assert.Equal("0000000000000001", r[0]);
            Assert.Equal("0001001000110100", r[1]);
        }

        /// <summary>
        /// 测试相关入口 ToBinaryArray_short数组_空数组返回空_但null会抛异常；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Empty、ToBinaryArray、Throws；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToBinaryArray_short数组_空数组返回空_但null会抛异常()
        {
            Assert.Empty(RegisterEx.ToBinaryArray(Array.Empty<short>()));
            Assert.Throws<NullReferenceException>(() => RegisterEx.ToBinaryArray((short[])null));
        }

        // ══════════════════════════════════════════════════════════════════
        // short[] → int[]
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 测试相关入口 ToIntArray_低字在前_高字在后；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ToIntArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToIntArray_低字在前_高字在后()
        {
            // BlockCopy + BitConverter.ToInt32 ⇒ 小端：data[2i] 是低 16 位
            Assert.Equal(new[] { 0x56781234 }, RegisterEx.ToIntArray(new short[] { 0x1234, 0x5678 }));
            Assert.Equal(new[] { 1 }, RegisterEx.ToIntArray(new short[] { 1, 0 }));
            Assert.Equal(new[] { 0x00010000 }, RegisterEx.ToIntArray(new short[] { 0, 1 }));
        }

        /// <summary>
        /// 测试相关入口 ToIntArray_多个元素_按两两一组；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ToIntArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToIntArray_多个元素_按两两一组()
        {
            Assert.Equal(new[] { 1, 2 },
                RegisterEx.ToIntArray(new short[] { 1, 0, 2, 0 }));
        }

        /// <summary>
        /// 测试相关入口 ToIntArray_null或空返回空数组；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Empty、ToIntArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToIntArray_null或空返回空数组()
        {
            Assert.Empty(RegisterEx.ToIntArray(null));
            Assert.Empty(RegisterEx.ToIntArray(Array.Empty<short>()));
        }

        /// <summary>
        /// 测试相关入口 ToIntArray_奇数长度抛ArgumentException；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Throws、ToIntArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToIntArray_奇数长度抛ArgumentException()
        {
            Assert.Throws<ArgumentException>(() => RegisterEx.ToIntArray(new short[] { 1, 2, 3 }));
            Assert.Throws<ArgumentException>(() => RegisterEx.ToIntArray(new short[] { 1 }));
        }

        /// <summary>
        /// 测试相关入口 ToIntArray_与int的ToShortArray互为逆运算_含负数与边界；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ToIntArray、ToShortArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToIntArray_与int的ToShortArray互为逆运算_含负数与边界()
        {
            int[] samples = { 0, 1, -1, 2, -2, 0x12345678, int.MinValue, int.MaxValue };

            Assert.Equal(samples, RegisterEx.ToIntArray(RegisterEx.ToShortArray(samples)));

            foreach (int v in samples)
                Assert.Equal(new[] { v }, RegisterEx.ToIntArray(RegisterEx.ToShortArray(v)));
        }

        // ══════════════════════════════════════════════════════════════════
        // short[] → string[]
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 测试相关入口 ToStringArray_按UTF8逐2字节解码_每个short一个字符串；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToStringArray、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToStringArray_按UTF8逐2字节解码_每个short一个字符串()
        {
            string[] r = RegisterEx.ToStringArray(new short[] { 0x0041, 0x0042 });

            Assert.Equal(2, r.Length);
            Assert.Equal("A\0", r[0]);   // 0x41 0x00 ⇒ "A" + NUL
            Assert.Equal("B\0", r[1]);
        }

        /// <summary>
        /// 测试相关入口 ToStringArray_两字节都是可打印字符时_得到一个双字符字符串；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ToStringArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToStringArray_两字节都是可打印字符时_得到一个双字符字符串()
        {
            // 0x4241 → 字节 0x41 0x42（小端）⇒ "AB"
            Assert.Equal(new[] { "AB" }, RegisterEx.ToStringArray(new short[] { 0x4241 }));
            // 0x1234 → 字节 0x34 0x12 ⇒ '4' + U+0012（控制字符）
            Assert.Equal(new[] { "4\u0012" }, RegisterEx.ToStringArray(new short[] { 0x1234 }));
        }

        /// <summary>
        /// 测试相关入口 ToStringArray_非法UTF8字节会被替换成UFFFD_信息不可逆；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToStringArray、Single、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToStringArray_非法UTF8字节会被替换成UFFFD_信息不可逆()
        {
            // 0x00FF → 字节 0xFF 0x00；0xFF 不是合法起始字节 ⇒ 替换字符 + NUL
            string[] r = RegisterEx.ToStringArray(new short[] { 0x00FF });

            Assert.Single(r);
            Assert.Equal("\uFFFD\0", r[0]);
            Assert.Equal(2, r[0].Length);
        }

        /// <summary>
        /// 测试相关入口 ToStringArray_null或空返回空数组；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Empty、ToStringArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToStringArray_null或空返回空数组()
        {
            Assert.Empty(RegisterEx.ToStringArray(null));
            Assert.Empty(RegisterEx.ToStringArray(Array.Empty<short>()));
        }

        /// <summary>
        /// 测试相关入口 ToStringArray_与string的ToShortArray不是互逆关系；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToStringArray、ToShortArray、Equal、NotEqual；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToStringArray_与string的ToShortArray不是互逆关系()
        {
            // 打包走 UTF32（4 字节/字符）并丢弃 0；拆包走 UTF8（2 字节/short）
            string[] back = RegisterEx.ToStringArray(RegisterEx.ToShortArray("AB"));

            Assert.Equal(new[] { "A\0", "B\0" }, back);
            // 逐元素比，别用数组级 Assert.NotEqual（那会走 xunit 的集合比较器，语义不是"逐元素不等"）
            Assert.NotEqual("A", back[0]);
            Assert.NotEqual("B", back[1]);
        }

        // ══════════════════════════════════════════════════════════════════
        // short[] → float[] / double[]
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 测试相关入口 ToFloatArray_成对还原float；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ToFloatArray、ToShortArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToFloatArray_成对还原float()
        {
            // 1.0f = 0x3F800000 → 小端字节 00 00 80 3F → shorts { 0x0000, 0x3F80 }
            Assert.Equal(new[] { 1.0f }, RegisterEx.ToFloatArray(new short[] { 0x0000, 0x3F80 }));
            Assert.Equal(new[] { -2.5f }, RegisterEx.ToFloatArray(RegisterEx.ToShortArray(-2.5f)));
        }

        /// <summary>
        /// 测试相关入口 ToFloatArray_null或空返回空数组_奇数长度抛异常；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Empty、ToFloatArray、Throws；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToFloatArray_null或空返回空数组_奇数长度抛异常()
        {
            Assert.Empty(RegisterEx.ToFloatArray(null));
            Assert.Empty(RegisterEx.ToFloatArray(Array.Empty<short>()));
            Assert.Throws<ArgumentException>(() => RegisterEx.ToFloatArray(new short[] { 1 }));
            Assert.Throws<ArgumentException>(() => RegisterEx.ToFloatArray(new short[] { 1, 2, 3 }));
        }

        /// <summary>
        /// 测试相关入口 ToFloatArray_长度2是合法的_但长度6也是合法的；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Single、ToFloatArray、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToFloatArray_长度2是合法的_但长度6也是合法的()
        {
            // 只校验「2 的倍数」，不校验 4 字节对齐
            Assert.Single(RegisterEx.ToFloatArray(new short[] { 1, 2 }));
            Assert.Equal(3, RegisterEx.ToFloatArray(new short[] { 1, 2, 3, 4, 5, 6 }).Length);
        }

        /// <summary>
        /// 测试相关入口 ToFloatArray_与float的ToShortArray互为逆运算；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ToFloatArray、ToShortArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToFloatArray_与float的ToShortArray互为逆运算()
        {
            float[] samples = { 0f, 1f, -1f, 1.5f, -2.5f, float.MaxValue, float.MinValue, float.Epsilon };

            Assert.Equal(samples, RegisterEx.ToFloatArray(RegisterEx.ToShortArray(samples)));
        }

        /// <summary>
        /// 测试相关入口 ToDoubleArray_四元组还原double；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ToDoubleArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToDoubleArray_四元组还原double()
        {
            // 1.0 = 0x3FF0000000000000 → 小端字节 00×6 F0 3F → shorts { 0,0,0,0x3FF0 }
            Assert.Equal(new[] { 1.0 }, RegisterEx.ToDoubleArray(new short[] { 0, 0, 0, 0x3FF0 }));
        }

        /// <summary>
        /// 测试相关入口 ToDoubleArray_null或空返回空数组_长度必须是4的倍数；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Empty、ToDoubleArray、Single、Throws；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToDoubleArray_null或空返回空数组_长度必须是4的倍数()
        {
            Assert.Empty(RegisterEx.ToDoubleArray(null));
            Assert.Empty(RegisterEx.ToDoubleArray(Array.Empty<short>()));

            Assert.Single(RegisterEx.ToDoubleArray(new short[] { 1, 2, 3, 4 }));
            // 2 与 6 对 float 合法，对 double 不合法 —— 档位差异
            Assert.Throws<ArgumentException>(() => RegisterEx.ToDoubleArray(new short[] { 1, 2 }));
            Assert.Throws<ArgumentException>(() => RegisterEx.ToDoubleArray(new short[] { 1, 2, 3, 4, 5, 6 }));
        }

        /// <summary>
        /// 测试相关入口 ToDoubleArray_与double的ToShortArray互为逆运算；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ToDoubleArray、ToShortArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToDoubleArray_与double的ToShortArray互为逆运算()
        {
            double[] samples = { 0.0, 1.0, -1.0, 1.5, -2.5, double.MaxValue, double.MinValue, double.Epsilon };

            Assert.Equal(samples, RegisterEx.ToDoubleArray(RegisterEx.ToShortArray(samples)));
        }

        // ══════════════════════════════════════════════════════════════════
        // int / int[] → short[]
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 测试相关入口 ToShortArray_int_低字在前高字在后；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ToShortArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToShortArray_int_低字在前高字在后()
        {
            Assert.Equal(new short[] { 0x5678, 0x1234 }, RegisterEx.ToShortArray(0x12345678));
            Assert.Equal(new short[] { 1, 0 }, RegisterEx.ToShortArray(1));
            Assert.Equal(new short[] { 0, 1 }, RegisterEx.ToShortArray(0x00010000));
        }

        /// <summary>
        /// 测试相关入口 ToShortArray_int_负值走算术右移_高位补符号；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ToShortArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToShortArray_int_负值走算术右移_高位补符号()
        {
            Assert.Equal(new short[] { -1, -1 }, RegisterEx.ToShortArray(-1));
            Assert.Equal(new short[] { -2, -1 }, RegisterEx.ToShortArray(-2));
            // int.MinValue = 0x80000000 → 低字 0，高字 0x8000
            Assert.Equal(new short[] { 0, short.MinValue }, RegisterEx.ToShortArray(int.MinValue));
        }

        /// <summary>
        /// 测试相关入口 ToShortArray_int数组_每个int两个short_顺序保持；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ToShortArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToShortArray_int数组_每个int两个short_顺序保持()
        {
            Assert.Equal(new short[] { 1, 0, 0x0003, 0x0002 },
                RegisterEx.ToShortArray(new int[] { 0x00000001, 0x00020003 }));
        }

        /// <summary>
        /// 测试相关入口 ToShortArray_int数组_null或空返回空数组；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Empty、ToShortArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToShortArray_int数组_null或空返回空数组()
        {
            Assert.Empty(RegisterEx.ToShortArray((int[])null));
            Assert.Empty(RegisterEx.ToShortArray(Array.Empty<int>()));
        }

        // ══════════════════════════════════════════════════════════════════
        // string / string[] → short[]（UTF32 + 丢弃 0 值）
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void ToShortArray_string_UTF32编码_ASCII字符得到码位本身()
        {
            // "AB" → UTF32 小端 41 00 00 00 42 00 00 00 → shorts { 0x41, 0, 0x42, 0 } → 过滤 0
            Assert.Equal(new short[] { 0x41, 0x42 }, RegisterEx.ToShortArray("AB"));
        }

        /// <summary>
        /// 测试相关入口 ToShortArray_string_中文按UTF32得到码位；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ToShortArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToShortArray_string_中文按UTF32得到码位()
        {
            Assert.Equal(new short[] { 0x4E2D }, RegisterEx.ToShortArray("中"));
            Assert.Equal(new short[] { 0x4E2D, 0x6587 }, RegisterEx.ToShortArray("中文"));
        }

        /// <summary>
        /// 测试相关入口 ToShortArray_string_内嵌NUL会被静默丢弃_是有损的；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ToShortArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToShortArray_string_内嵌NUL会被静默丢弃_是有损的()
        {
            // "A\0B" 与 "AB" 得到完全相同的 short[] ⇒ 原始长度信息丢失
            Assert.Equal(new short[] { 0x41, 0x42 }, RegisterEx.ToShortArray("A\0B"));
            Assert.Equal(RegisterEx.ToShortArray("AB"), RegisterEx.ToShortArray("A\0B"));
            Assert.Equal(3, "A\0B".Length);   // 输入确实有 3 个字符
        }

        /// <summary>
        /// 测试相关入口 ToShortArray_string_码位U10000的低字0被丢弃_数据被破坏；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ToShortArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToShortArray_string_码位U10000的低字0被丢弃_数据被破坏()
        {
            // U+10000 → UTF32 小端 00 00 01 00 → shorts { 0, 1 } → 过滤 0 后只剩 { 1 }
            // 正确的 4 字节表示应为 { 0, 1 }；丢失低字后**无法还原**
            Assert.Equal(new short[] { 1 }, RegisterEx.ToShortArray("\U00010000"));
        }

        /// <summary>
        /// 测试相关入口 ToShortArray_string_null或空返回空数组；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Empty、ToShortArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToShortArray_string_null或空返回空数组()
        {
            Assert.Empty(RegisterEx.ToShortArray((string)null));
            Assert.Empty(RegisterEx.ToShortArray(string.Empty));
        }

        /// <summary>
        /// 测试相关入口 ToShortArray_string数组_按元素拼接；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ToShortArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToShortArray_string数组_按元素拼接()
        {
            Assert.Equal(new short[] { 0x41, 0x42, 0x43 },
                RegisterEx.ToShortArray(new[] { "AB", "C" }));
        }

        /// <summary>
        /// 测试相关入口 ToShortArray_string数组_null元素被静默跳过；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ToShortArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToShortArray_string数组_null元素被静默跳过()
        {
            Assert.Equal(new short[] { 0x41, 0x42 },
                RegisterEx.ToShortArray(new[] { "A", null, "B" }));
        }

        /// <summary>
        /// 测试相关入口 ToShortArray_string数组_null或空返回空数组；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Empty、ToShortArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToShortArray_string数组_null或空返回空数组()
        {
            Assert.Empty(RegisterEx.ToShortArray((string[])null));
            Assert.Empty(RegisterEx.ToShortArray(Array.Empty<string>()));
        }

        // ══════════════════════════════════════════════════════════════════
        // float / double → short[]
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 测试相关入口 ToShortArray_float_两个short；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ToShortArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToShortArray_float_两个short()
        {
            Assert.Equal(new short[] { 0x0000, 0x3F80 }, RegisterEx.ToShortArray(1.0f));
        }

        /// <summary>
        /// 测试相关入口 ToShortArray_float数组_每个float两个short；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToShortArray、Equal、ToFloatArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToShortArray_float数组_每个float两个short()
        {
            short[] packed = RegisterEx.ToShortArray(new float[] { 1f, 2f });

            Assert.Equal(4, packed.Length);
            Assert.Equal(new[] { 1f, 2f }, RegisterEx.ToFloatArray(packed));
        }

        /// <summary>
        /// 测试相关入口 ToShortArray_float数组_null或空返回空数组；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Empty、ToShortArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToShortArray_float数组_null或空返回空数组()
        {
            Assert.Empty(RegisterEx.ToShortArray((float[])null));
            Assert.Empty(RegisterEx.ToShortArray(Array.Empty<float>()));
        }

        /// <summary>
        /// 测试相关入口 ToShortArray_double_四个short；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ToShortArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToShortArray_double_四个short()
        {
            Assert.Equal(new short[] { 0, 0, 0, 0x3FF0 }, RegisterEx.ToShortArray(1.0));
        }

        /// <summary>
        /// 测试相关入口 ToShortArray_double数组_每个double四个short；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ToShortArray、Equal、ToDoubleArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToShortArray_double数组_每个double四个short()
        {
            short[] packed = RegisterEx.ToShortArray(new double[] { 1.0, 2.0 });

            Assert.Equal(8, packed.Length);
            Assert.Equal(new[] { 1.0, 2.0 }, RegisterEx.ToDoubleArray(packed));
        }

        /// <summary>
        /// 测试相关入口 ToShortArray_double数组_null或空返回空数组；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Empty、ToShortArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToShortArray_double数组_null或空返回空数组()
        {
            Assert.Empty(RegisterEx.ToShortArray((double[])null));
            Assert.Empty(RegisterEx.ToShortArray(Array.Empty<double>()));
        }

        /// <summary>
        /// 测试相关入口 ToShortArray_double与float占用的short个数比是2比1；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、ToShortArray；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToShortArray_double与float占用的short个数比是2比1()
        {
            Assert.Equal(2, RegisterEx.ToShortArray(1.0f).Length);
            Assert.Equal(4, RegisterEx.ToShortArray(1.0).Length);
            Assert.Equal(4, RegisterEx.ToShortArray(new float[] { 1f, 2f }).Length);
            Assert.Equal(8, RegisterEx.ToShortArray(new double[] { 1.0, 2.0 }).Length);
        }

        // ══════════════════════════════════════════════════════════════════
        // API 面（反射，防止后续重构悄悄改变公开契约）
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void API面_RegisterEx是静态类_共16个公开扩展方法()
        {
            Type t = typeof(RegisterEx);

            Assert.True(t.IsAbstract && t.IsSealed, "扩展方法容器必须是 static class");

            MethodInfo[] methods = t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Assert.Equal(16, methods.Length);
            Assert.All(methods, m =>
                Assert.True(m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false),
                    $"{m.Name} 缺少 [Extension]"));
        }

        /// <summary>
        /// 测试相关入口 API面_ToShortArray有8个重载_是重载最密集的成员；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 GetMethods、Equal、Count、GetParameters；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void API面_ToShortArray有8个重载_是重载最密集的成员()
        {
            MethodInfo[] methods = typeof(RegisterEx)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

            Assert.Equal(8, methods.Count(m => m.Name == "ToShortArray"));
            // int / int[] / string / string[] / float / float[] / double / double[]
            Assert.Equal(8, methods.Count(m => m.Name == "ToShortArray" && m.GetParameters().Length == 1));

            // 重载只按接收器类型区分，没有靠缺省参数「补」出来的重载
            Assert.Equal(2, methods.Count(m => m.Name == "ToBinaryArray"));
            Assert.Equal(2, methods.Count(m => m.Name == "ToBoolArray" || m.Name == "ToList_BoolArray"));
        }
    }
}
