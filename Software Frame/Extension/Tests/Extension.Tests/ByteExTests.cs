// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ByteExTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Text、Extension.Core.Conversion、Xunit。导入不代表所有类型都会在运行时加载。
using System;
using System.Text;
using Extension.Core.Conversion;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// T0 层「字节序」专项：<c>Extension.Core.Conversion.ByteEx</c>。
    /// 这是全仓 Modbus / PLC 通信的字节序底座，此前**零用例覆盖**。
    /// 本文件既覆盖正常往返，也把三处**已知缺陷/副作用**固化成可见的测试。
    /// <para>✅ <b>序 8b 已修 2/3（2026-09-18）</b>：<c>ToBytes(this float)</c> ——
    /// 原为「任何输入必抛 <c>ArgumentOutOfRangeException</c>」，现按 IEEE754 返回 **4 字节**；
    /// <c>ToASCII(this byte[])</c> —— 原「大端会**就地反转调用方数组**」，现改为**反转副本**。</para>
    /// <para>⛔ 剩余 1 处**仍未修**（硬规则 1：发现即记录，不顺手改）：
    /// <c>ToHexBools</c> 名不副实（只取每字节 bit4 / bit0，其余位丢弃）——
    /// 但其实现本身**是对的**（正是三菱 MC 协议位软元件的打包方式），只是名字误导。</para>
    /// <para>⚠️ 这两处**都有真实生产调用点**（<b>不是</b>零引用，文档曾误记为“全仓零调用”）：
    /// <c>ToASCII</c> **10 处** / <c>ToHexBools</c> 1 处，详见 <c>FILE-TIERING.md</c> §5.13。</para>
    /// </summary>
    public class ByteExTests
    {
        // ══════════════════════════════════════════════════════════════════
        // 其他类型 -> byte[]（默认大端）
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void ToBytes_short_大小端()
        {
            Assert.Equal(new byte[] { 0x12, 0x34 }, ((short)0x1234).ToBytes());
            Assert.Equal(new byte[] { 0x34, 0x12 }, ((short)0x1234).ToBytes(EEndianType.Little_Endian));

            // 负数：补码全 F
            Assert.Equal(new byte[] { 0xFF, 0xFE }, ((short)-2).ToBytes());
            Assert.Equal(new byte[] { 0xFF, 0xFF }, ((short)-1).ToBytes());
        }

        /// <summary>
        /// 验证 ToBytes_ushort_大小端 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、ToBytes；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToBytes_ushort_大小端()
        {
            Assert.Equal(new byte[] { 0x12, 0x34 }, ((ushort)0x1234).ToBytes());
            Assert.Equal(new byte[] { 0x34, 0x12 }, ((ushort)0x1234).ToBytes(EEndianType.Little_Endian));
        }

        /// <summary>
        /// 验证 ToBytes_int_大小端 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、ToBytes；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToBytes_int_大小端()
        {
            Assert.Equal(new byte[] { 0x12, 0x34, 0x56, 0x78 }, 0x12345678.ToBytes());
            Assert.Equal(new byte[] { 0x78, 0x56, 0x34, 0x12 }, 0x12345678.ToBytes(EEndianType.Little_Endian));
        }

        /// <summary>
        /// 验证 ToBytes_uint_大小端 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、ToBytes；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToBytes_uint_大小端()
        {
            Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, 0xFFFFFFFFu.ToBytes());
            Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, 0xFFFFFFFFu.ToBytes(EEndianType.Little_Endian));
        }

        /// <summary>
        /// 验证 ToBytes_long_与ulong_都是8字节 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、ToBytes；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToBytes_long_与ulong_都是8字节()
        {
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 },
                0x0102030405060708L.ToBytes());
            Assert.Equal(new byte[] { 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01 },
                0x0102030405060708L.ToBytes(EEndianType.Little_Endian));

            Assert.Equal(8, 1UL.ToBytes().Length);
            Assert.Equal(8, 1UL.ToBytes(EEndianType.Little_Endian).Length);
        }

        /// <summary>
        /// 验证 ToBytes_double_按IEEE754 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、ToBytes；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToBytes_double_按IEEE754()
        {
            // 1.0 == 0x3FF0000000000000
            Assert.Equal(new byte[] { 0x3F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 1.0.ToBytes());
            Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F },
                1.0.ToBytes(EEndianType.Little_Endian));
        }

        // ══════════════════════════════════════════════════════════════════
        // ✅ 序 8b 已修（2026-09-18）：float 的 ToBytes 现在可用，固定 4 字节
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 修前：<c>ByteEx.cs</c> 的 <c>ToBytes(this float)</c> 里写的是
        /// <c>stackalloc byte[4]</c>，却调用 <c>BinaryPrimitives.WriteInt64BigEndian</c> /
        /// <c>WriteInt64LittleEndian</c>（要求 ≥ 8 字节）⇒ **任何输入**都在边界检查处抛
        /// <c>ArgumentOutOfRangeException</c>，方法**完全不可用**。
        /// <para>修法：保持 <c>stackalloc byte[4]</c>（<c>float</c> 的 IEEE754 就是 4 字节），
        /// 改调 <c>WriteUInt32*</c>。</para>
        /// <para>⚠️ 文档曾给的另一个选项「改 <c>stackalloc byte[8]</c>」是**错的** ——
        /// 那会让 <c>float</c> 返回 8 字节。本用例末尾专门断言长度为 <c>4</c>，正是为了钉住这一点。</para>
        /// </summary>
        [Fact]
        public void ToBytes_float_按IEEE754_固定4字节()
        {
            // 1.0f 的 IEEE754 位模式是 0x3F800000
            Assert.Equal(new byte[] { 0x3F, 0x80, 0x00, 0x00 }, 1.0f.ToBytes());
            Assert.Equal(new byte[] { 0x00, 0x00, 0x80, 0x3F },
                1.0f.ToBytes(EEndianType.Little_Endian));

            // -1.0f 的位模式是 0xBF800000 ⇒ 用来确认符号位被保留（没有走 int 截断）
            Assert.Equal(new byte[] { 0xBF, 0x80, 0x00, 0x00 }, (-1.0f).ToBytes());
            Assert.Equal(new byte[] { 0x00, 0x00, 0x80, 0xBF },
                (-1.0f).ToBytes(EEndianType.Little_Endian));

            // ⛔ 长度必须是 4（不是 8）—— 这正是文档那条错误建议会破坏的性质
            Assert.Equal(4, 0f.ToBytes().Length);
            Assert.Equal(4, 0f.ToBytes(EEndianType.Little_Endian).Length);

            // 往返：4 字节 -> float 应当还原（ToFloat 走 BitConverter，与大小端无关）
            Assert.Equal(1.5f, 1.5f.ToBytes().ToFloat());

            // 对照组：double 的同名方法本来就是对的（stackalloc byte[8]）
            Assert.Equal(8, 1.0.ToBytes().Length);
            Assert.Equal(new byte[] { 0x3F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
                1.0.ToBytes());
        }

        // ══════════════════════════════════════════════════════════════════
        // ✅ 副作用 2 已消：ToASCII 改为反转副本，不再动调用方的数组
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void ToASCII_大端反转副本_不改动调用方数组()
        {
            // ✅ 序 8b（2026-09-18 修）：修前大端分支走 data.AsSpan().Reverse()
            //   —— Span 的 Reverse 是**原地**操作，会直接改写调用方传进来的 byte[]，
            //   方法签名看不出这一点。现改为 (byte[])data.Clone() 之后反转。
            var data = new byte[] { 0x41, 0x42 };          // "AB"
            Assert.Equal("BA", data.ToASCII());            // 返回值与修前**一致**（大端先反转再解码）
            Assert.Equal(new byte[] { 0x41, 0x42 }, data); // ✅ 调用方的数组**未被改动**

            // 幂等：同一数组重复调用结果稳定
            // （修前第二次会因二次反转而变回 "AB" —— 这正是共享数组场景下的串扰来源）
            Assert.Equal("BA", data.ToASCII());
            Assert.Equal(new byte[] { 0x41, 0x42 }, data);

            // 小端分支本就不反转，且同样不碰调用方数组
            var le = new byte[] { 0x41, 0x42 };
            Assert.Equal("AB", le.ToASCII(EEndianType.Little_Endian));
            Assert.Equal(new byte[] { 0x41, 0x42 }, le);

            // 多字节同样只反转副本
            var abc = new byte[] { 0x41, 0x42, 0x43 };     // "ABC"
            Assert.Equal("CBA", abc.ToASCII());
            Assert.Equal(new byte[] { 0x41, 0x42, 0x43 }, abc);
        }

        /// <summary>
        /// 验证 ToASCII_空数组与null 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、ToASCII、Throws；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToASCII_空数组与null()
        {
            Assert.Equal(string.Empty, new byte[0].ToASCII());
            Assert.Throws<ArgumentNullException>(() => ((byte[])null).ToASCII());
        }

        // ══════════════════════════════════════════════════════════════════
        // byte[] -> 其他类型
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void ToShort_大小端与负值()
        {
            Assert.Equal((short)0x1234, new byte[] { 0x12, 0x34 }.ToShort());
            Assert.Equal((short)0x1234, new byte[] { 0x34, 0x12 }.ToShort(EEndianType.Little_Endian));
            Assert.Equal((short)-2, new byte[] { 0xFF, 0xFE }.ToShort());
        }

        /// <summary>
        /// 验证 ToUShort_大小端 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、ToUShort；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToUShort_大小端()
        {
            Assert.Equal((ushort)0xFFFF, new byte[] { 0xFF, 0xFF }.ToUShort());
            Assert.Equal((ushort)1, new byte[] { 0x00, 0x01 }.ToUShort());
            Assert.Equal((ushort)1, new byte[] { 0x01, 0x00 }.ToUShort(EEndianType.Little_Endian));
        }

        /// <summary>
        /// 验证 ToInt_大小端 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、ToInt；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToInt_大小端()
        {
            Assert.Equal(0x12345678, new byte[] { 0x12, 0x34, 0x56, 0x78 }.ToInt());
            Assert.Equal(0x12345678, new byte[] { 0x78, 0x56, 0x34, 0x12 }.ToInt(EEndianType.Little_Endian));
        }

        /// <summary>
        /// 验证 ToUInt_大小端 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、ToUInt；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToUInt_大小端()
        {
            Assert.Equal(0x12345678u, new byte[] { 0x12, 0x34, 0x56, 0x78 }.ToUInt());
            Assert.Equal(0x12345678u, new byte[] { 0x78, 0x56, 0x34, 0x12 }.ToUInt(EEndianType.Little_Endian));
        }

        /// <summary>
        /// 验证 ToFloat_大小端 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、ToFloat；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToFloat_大小端()
        {
            // 1.0f == 0x3F800000
            Assert.Equal(1.0f, new byte[] { 0x3F, 0x80, 0x00, 0x00 }.ToFloat());
            Assert.Equal(1.0f, new byte[] { 0x00, 0x00, 0x80, 0x3F }.ToFloat(EEndianType.Little_Endian));
            // -1.0f == 0xBF800000
            Assert.Equal(-1.0f, new byte[] { 0xBF, 0x80, 0x00, 0x00 }.ToFloat());
        }

        /// <summary>
        /// 验证 ToDouble_大小端 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、ToDouble；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToDouble_大小端()
        {
            // 1.0 == 0x3FF0000000000000
            Assert.Equal(1.0, new byte[] { 0x3F, 0xF0, 0, 0, 0, 0, 0, 0 }.ToDouble());
            Assert.Equal(1.0, new byte[] { 0, 0, 0, 0, 0, 0, 0xF0, 0x3F }.ToDouble(EEndianType.Little_Endian));
        }

        /// <summary>
        /// 验证 字节数不足时_BinaryPrimitives抛参数异常 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Exception、ToShort、NotNull、IsAssignableFrom、ToInt；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void 字节数不足时_BinaryPrimitives抛参数异常()
        {
            // 这些方法都不做长度校验，直接交给 BinaryPrimitives，长度不够就抛 ArgumentException 系
            var ex = Record.Exception(() => new byte[] { 0x01 }.ToShort());
            Assert.NotNull(ex);
            Assert.IsAssignableFrom<ArgumentException>(ex);

            var ex4 = Record.Exception(() => new byte[] { 0x01, 0x02 }.ToInt());
            Assert.NotNull(ex4);
            Assert.IsAssignableFrom<ArgumentException>(ex4);
        }

        /// <summary>
        /// 验证 ToUInt16_自带边界检查_可指定起点 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、ToUInt16；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToUInt16_自带边界检查_可指定起点()
        {
            var buf = new byte[] { 0x12, 0x34, 0x56 };
            Assert.Equal((ushort)0x3412, buf.ToUInt16(0));                    // 默认 isLE = true
            Assert.Equal((ushort)0x1234, buf.ToUInt16(0, isLE: false));
            Assert.Equal((ushort)0x5634, buf.ToUInt16(1));
            Assert.Equal((ushort)0x3456, buf.ToUInt16(1, isLE: false));
        }

        /// <summary>
        /// 验证 ToUInt16_越界与null抛异常 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Throws、ToUInt16；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToUInt16_越界与null抛异常()
        {
            Assert.Throws<ArgumentNullException>(() => ((byte[])null).ToUInt16(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new byte[] { 0x12 }.ToUInt16(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new byte[] { 0x12, 0x34 }.ToUInt16(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new byte[] { 0x12, 0x34 }.ToUInt16(-1));
        }

        // ══════════════════════════════════════════════════════════════════
        // ToHexBools —— 只认 0x10 与 0x01 两位
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void ToHexBools_每字节拆两位_下标0对应高4位()
        {
            Assert.Equal(new[] { true, false }, new byte[] { 0x10 }.ToHexBools());
            Assert.Equal(new[] { false, true }, new byte[] { 0x01 }.ToHexBools());
            Assert.Equal(new[] { true, true }, new byte[] { 0x11 }.ToHexBools());
            Assert.Equal(new[] { false, false }, new byte[] { 0x00 }.ToHexBools());

            Assert.Equal(new[] { true, false, false, true }, new byte[] { 0x10, 0x01 }.ToHexBools());
        }

        /// <summary>
        /// 验证 ToHexBools_只认0x10和0x01_其余位被忽略 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、ToHexBools；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToHexBools_只认0x10和0x01_其余位被忽略()
        {
            // ⚠️ 命名与实现不符：叫 ToHexBools（"十六进制"），但只看每字节的 bit4 与 bit0，
            //    0x20 / 0x40 / 0x80 / 0x02 / 0x04 / 0x08 全部被丢弃。
            Assert.Equal(new byte[] { 0xFF }.ToHexBools(), new byte[] { 0x11 }.ToHexBools()); // 高位被忽略
            Assert.Equal(new byte[] { 0xEF }.ToHexBools(), new byte[] { 0x01 }.ToHexBools()); // 高位被忽略
            Assert.Equal(new[] { false, false }, new byte[] { 0x2C }.ToHexBools());
        }

        /// <summary>
        /// 验证 ToHexBools_空与null返回空数组 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Empty、ToHexBools；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToHexBools_空与null返回空数组()
        {
            Assert.Empty(new byte[0].ToHexBools());
            Assert.Empty(((byte[])null).ToHexBools());
        }

        // ══════════════════════════════════════════════════════════════════
        // 字符串 -> byte[]（按 Modbus 寄存器 2 字节对齐 + 可切字节序）
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void ToBytes_字符串_大端时只补齐不交换()
        {
            Assert.Equal(new byte[] { 0x41, 0x42 }, "AB".ToBytes(Encoding.ASCII));
            // 奇数长度：补 1 个 0 字节到偶数（Modbus 16 位寄存器对齐）
            Assert.Equal(new byte[] { 0x41, 0x42, 0x43, 0x00 }, "ABC".ToBytes(Encoding.ASCII));
            Assert.Empty("".ToBytes(Encoding.ASCII));
        }

        /// <summary>
        /// 验证 ToBytes_字符串_小端时按寄存器两两交换 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、ToBytes；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToBytes_字符串_小端时按寄存器两两交换()
        {
            Assert.Equal(new byte[] { 0x42, 0x41 }, "AB".ToBytes(Encoding.ASCII, EEndianType.Little_Endian));
            // "ABCD" -> [41 42 43 44] -> 每个寄存器内部交换 -> [42 41 44 43]
            Assert.Equal(new byte[] { 0x42, 0x41, 0x44, 0x43 },
                "ABCD".ToBytes(Encoding.ASCII, EEndianType.Little_Endian));
        }

        /// <summary>
        /// 验证 ToBytes_字符串_UTF8多字节按字节处理 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、ToBytes；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToBytes_字符串_UTF8多字节按字节处理()
        {
            // "中" UTF8 = E4 B8 AD（3 字节）-> 补 1 -> [E4 B8 AD 00]
            Assert.Equal(new byte[] { 0xE4, 0xB8, 0xAD, 0x00 }, "中".ToBytes(Encoding.UTF8));
        }

        /// <summary>
        /// 验证 ToBytes_字符串_超长输入走堆分配不炸栈 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ToBytes、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ToBytes_字符串_超长输入走堆分配不炸栈()
        {
            // 实现里 >1024 字节走 new byte[]，这里确认大输入路径可用
            var big = new string('A', 5000);
            var bytes = big.ToBytes(Encoding.ASCII);
            Assert.Equal(5000, bytes.Length);
            Assert.Equal((byte)0x41, bytes[0]);
        }

        // ══════════════════════════════════════════════════════════════════
        // 往返一致性
        // ══════════════════════════════════════════════════════════════════

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(-1)]
        [InlineData(12345)]
        [InlineData(short.MaxValue)]
        [InlineData(short.MinValue)]
        public void 往返_short(int value)
        {
            var s = (short)value;
            Assert.Equal(s, s.ToBytes().ToShort());
            Assert.Equal(s, s.ToBytes(EEndianType.Little_Endian).ToShort(EEndianType.Little_Endian));
        }

        /// <summary>
        /// 验证 往返_uint 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、ToUInt、ToBytes；具体条件和先后次序见方法体。
        /// </summary>
        [Theory]
        [InlineData(0u)]
        [InlineData(1u)]
        [InlineData(0xFFFFFFFFu)]
        [InlineData(0x12345678u)]
        public void 往返_uint(uint value)
        {
            Assert.Equal(value, value.ToBytes().ToUInt());
            Assert.Equal(value, value.ToBytes(EEndianType.Little_Endian).ToUInt(EEndianType.Little_Endian));
        }

        /// <summary>
        /// 验证 往返_double 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、ToDouble、ToBytes；具体条件和先后次序见方法体。
        /// </summary>
        [Theory]
        [InlineData(0.0)]
        [InlineData(1.0)]
        [InlineData(-1.5)]
        [InlineData(double.MaxValue)]
        public void 往返_double(double value)
        {
            Assert.Equal(value, value.ToBytes().ToDouble());
            Assert.Equal(value, value.ToBytes(EEndianType.Little_Endian).ToDouble(EEndianType.Little_Endian));
        }
    }
}
