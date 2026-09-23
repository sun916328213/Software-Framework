using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Extension.Core.Conversion;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// T0 层「类型转换」专项：<c>Extension.Core.Conversion</c>。
    /// 该命名空间是全仓被引用最多的一个（57 个下游文件），但此前只有 1 个用例覆盖它。
    /// 全部为纯函数，无 IO / 无 vendor / 无 UI。
    /// </summary>
    public class ConversionTests
    {
        // ══════════════════════════════════════════════════════════════════
        // HexEx —— 十六进制字符串
        // ══════════════════════════════════════════════════════════════════

        // ⛔ 注意：本命名空间里 HexEx.ToASC(string) 与 BinaryEx.ToASC(string) **签名完全相同**，
        // 只要 `using Extension.Core.Conversion;` 存在，`"41".ToASC()` 就是 CS0121 二义性错误，
        // 必须写成静态调用 HexEx.ToASC(...) / BinaryEx.ToASC(...)。这是真实的 API 冲突，
        // 见本文件末尾「API 面」一节。以下全部改用静态调用形式。

        [Fact]
        public void HexEx_ToASC_空串与null都返回空串()
        {
            Assert.Equal(string.Empty, HexEx.ToASC(""));
            Assert.Equal(string.Empty, HexEx.ToASC(null));
        }

        [Fact]
        public void HexEx_ToASC_正常偶数长度()
        {
            Assert.Equal("A", HexEx.ToASC("41"));
            Assert.Equal("AB", HexEx.ToASC("4142"));
        }

        [Fact]
        public void HexEx_ToBytes_偶数长度直通()
        {
            Assert.Equal(new byte[] { 0x41 }, "41".ToBytes());
            Assert.Equal(new byte[] { 0x41, 0x42 }, "4142".ToBytes());
        }

        [Fact]
        public void HexEx_ToBytes_奇数长度是左补零而非右补零()
        {
            // ⚠️ 约定锚点：HexEx 是 **左**补零（"ABC" -> "0ABC" -> 0x0A,0xBC），
            // 而同族的 BinaryEx.ToASC 是 **右**补零。两者不一致，见本文件末尾的对照用例。
            Assert.Equal(new byte[] { 0x0A, 0xBC }, "ABC".ToBytes());
        }

        [Fact]
        public void HexEx_ToBytes_空串得到空数组()
        {
            Assert.Empty("".ToBytes());
        }

        [Fact]
        public void HexEx_ToBytes_非法字符抛FormatException()
        {
            Assert.Throws<FormatException>(() => "ZZ".ToBytes());
        }

        // ══════════════════════════════════════════════════════════════════
        // BinaryEx —— 二进制字符串
        // ══════════════════════════════════════════════════════════════════

        [Theory]
        [InlineData("0", 0)]
        [InlineData("1010", 10)]
        [InlineData("11111111", 255)]
        [InlineData("100000000", 256)]
        public void BinaryEx_ToDec_按二进制解析(string input, int expected)
        {
            Assert.Equal(expected, input.ToDec());
        }

        [Fact]
        public void BinaryEx_ToDec_空串会抛异常()
        {
            // 无输入校验，直接进 Convert.ToInt32。用 ThrowsAny 是因为异常类型取决于 BCL 版本。
            Assert.ThrowsAny<Exception>(() => "".ToDec());
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("1", "1")]                 // 补到 4 位 -> 0001
        [InlineData("1010", "A")]
        [InlineData("1111", "F")]
        [InlineData("11111111", "FF")]
        [InlineData("1111111111111111", "FFFF")]
        public void BinaryEx_ToHex_按4位一组左补零(string input, string expected)
        {
            // 必须显式写 BinaryEx.ToHex —— 否则 input.ToHex() 虽然恰好也绑到 BinaryEx（见下文陷阱用例），
            // 但那属于"靠重载决议碰巧选中"，不是本用例要表达的意图。
            Assert.Equal(expected, BinaryEx.ToHex(input));
        }

        [Fact]
        public void BinaryEx_ToASC_按8位一组右补零()
        {
            Assert.Equal("A", BinaryEx.ToASC("01000001"));
            Assert.Equal("AB", BinaryEx.ToASC("0100000101000010"));
            Assert.Equal(string.Empty, BinaryEx.ToASC(""));

            // ⚠️ 关键对照：7 位时是**右**补零 -> "01000000" = 0x40 = '@'
            // 若按 HexEx 的左补零约定，会得到 "00000010" = 0x02（不可见控制符）。
            Assert.Equal("@", BinaryEx.ToASC("0100000"));
        }

        [Fact]
        public void 补零方向对照_HexEx左补零_BinaryEx右补零()
        {
            // 同一份"半字节"输入，两个类的补零方向相反 —— 这是真实存在的约定不一致，
            // 不是缺陷（各自的调用方依赖现行为），但必须被测试钉住，避免被"顺手统一"。
            Assert.Equal(new byte[] { 0x0A }, "A".ToBytes());        // HexEx:  "A" -> 左补 -> "0A"
            Assert.Equal("@", BinaryEx.ToASC("0100000"));            // BinaryEx: 右补 -> "01000000" = 0x40 = '@'

            // 反证：同样是 7 位，若是左补零会得到 "00100000" = 0x20 = 空格
            Assert.NotEqual(" ", BinaryEx.ToASC("0100000"));
            Assert.Equal(" ", BinaryEx.ToASC("00100000"));
        }

        // ══════════════════════════════════════════════════════════════════
        // ASCIIEx —— ASCII / Modbus 寄存器打包
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void ASCIIEx_ToHex_默认大写_可切小写()
        {
            Assert.Equal("616263", ASCIIEx.ToHex("abc"));
            Assert.Equal("616263", ASCIIEx.ToHex("abc", false));
            Assert.Equal("0A", ASCIIEx.ToHex("\n"));
            Assert.Equal("0a", ASCIIEx.ToHex("\n", false));
            Assert.Equal(string.Empty, ASCIIEx.ToHex(""));
        }

        [Fact]
        public void 重载决议陷阱_字符串ToHex默认绑到BinaryEx而不是ASCIIEx()
        {
            // ⛔⛔ 这是本库最容易踩的一个坑，且**只在运行时暴露**：
            //   ASCIIEx.ToHex(this string data, bool uppercase = true)   <- 缺省参数
            //   BinaryEx.ToHex(this string data)                          <- 无缺省参数
            // C# 重载决议在"省略可选参数"与"形参数量完全匹配"之间**优先后者**，
            // 于是 `someString.ToHex()` 绑到 **BinaryEx.ToHex（二进制解析）**，
            // 而不是看起来更"对口"的 ASCIIEx.ToHex。
            // 后果：把普通文本当二进制串解析 —— 轻则 FormatException，重则静默给出错误结果。
            Assert.Equal("A", "1010".ToHex());                     // 二进制语义：1010b = 0xA
            Assert.Equal("31303130", ASCIIEx.ToHex("1010"));      // ASCII 语义：'1''0''1''0'

            // 非 0/1 文本直接炸（FormatException: Additional non-parsable characters...）
            Assert.Throws<FormatException>(() => "abc".ToHex());
            Assert.Equal("616263", ASCIIEx.ToHex("abc"));

            // 传了第二个实参就没有歧义了，正常走 ASCIIEx
            Assert.Equal("616263", "abc".ToHex(true));

            // 全仓下游之所以没出事，是因为**一律显式限定**，例如
            // Device/Device/RFID/.../XK15A_R01Helper.cs:12  `ASCIIEx.ToHex(page.ToString())`
            // Communication/Communication/MC/MCHelper11.cs:814 `BinaryEx.ToASC(InputData[...])`
            // ⚠️ 2026-09-23：上一条引用文件已随旧通信栈删除，保留原行仅作溯源。
            // ⇒ 这是一个"靠调用方自律才不炸"的 API 设计。
        }

        [Fact]
        public void ASCIIEx_ToUshorts_大端按2字节打包()
        {
            // "1234" -> UTF8 [0x31,0x32,0x33,0x34] -> BE: 0x3132, 0x3334
            Assert.Equal(new ushort[] { 0x3132, 0x3334 }, "1234".ToUshorts());
        }

        [Fact]
        public void ASCIIEx_ToUshorts_小端按2字节打包()
        {
            Assert.Equal(new ushort[] { 0x3231, 0x3433 },
                "1234".ToUshorts(EEndianType.Little_Endian));
        }

        [Fact]
        public void ASCIIEx_ToUshorts_奇数长度末尾补零字节()
        {
            // "123" -> [0x31,0x32,0x33] 补 1 个 0 -> [0x31,0x32,0x33,0x00] -> [0x3132, 0x3300]
            Assert.Equal(new ushort[] { 0x3132, 0x3300 }, "123".ToUshorts());
        }

        [Fact]
        public void ASCIIEx_ToUshorts_空串抛ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => "".ToUshorts());
            Assert.Throws<ArgumentNullException>(() => ((string)null).ToUshorts());
        }

        [Fact]
        public void ASCIIEx_ToShorts_用的是UTF32而非UTF8_与ToUshorts结果不同()
        {
            // ⛔ 已知不一致：ToShorts 用 Encoding.UTF32.GetBytes，ToUshorts 用 Encoding.UTF8.GetBytes。
            // 同一输入 "12"：
            //   ToUshorts -> UTF8 2 字节  -> 1 个寄存器  [0x3132]
            //   ToShorts  -> UTF32 8 字节 -> 4 个寄存器  [0x3100, 0x0000, 0x3200, 0x0000]
            // 这不是笔误就是刻意的历史包袱，两者**不可能**同时正确。已记入 FILE-TIERING.md §5.12。
            Assert.Equal(new ushort[] { 0x3132 }, "12".ToUshorts());
            Assert.Equal(new short[] { 0x3100, 0x0000, 0x3200, 0x0000 }, "12".ToShorts());
        }

        [Fact]
        public void ASCIIEx_ToShorts_空串抛ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => "".ToShorts());
        }

        // ══════════════════════════════════════════════════════════════════
        // BoolEx
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void BoolEx_单值转换_true为1_false为0()
        {
            Assert.Equal((short)1, true.ToShort());
            Assert.Equal((short)0, false.ToShort());
            Assert.Equal(1, true.ToInt());
            Assert.Equal(0, false.ToInt());
            Assert.Equal((byte)1, true.ToByte());
            Assert.Equal((byte)0, false.ToByte());
        }

        [Fact]
        public void BoolEx_ToShortArray_下标0对应最低位()
        {
            Assert.Equal((short)0, new bool[0].ToShortArray());
            Assert.Equal((short)0, new[] { false }.ToShortArray());
            Assert.Equal((short)1, new[] { true }.ToShortArray());
            Assert.Equal((short)1, new[] { true, false }.ToShortArray());
            Assert.Equal((short)2, new[] { false, true }.ToShortArray());
            Assert.Equal((short)3, new[] { true, true }.ToShortArray());
        }

        [Fact]
        public void BoolEx_ToShortArray_16位全true得到负一()
        {
            // 第 16 位（下标15）是符号位：(short)(1<<15) == short.MinValue
            var all = Enumerable.Repeat(true, 16).ToArray();
            Assert.Equal((short)-1, all.ToShortArray());
        }

        [Fact]
        public void BoolEx_ToShortArray_超过16位抛ArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new bool[17].ToShortArray());
        }

        [Fact]
        public void BoolEx_ToShortArray_null抛ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ((bool[])null).ToShortArray());
            Assert.Throws<ArgumentNullException>(() => ((List<bool[]>)null).ToShortArray());
        }

        [Fact]
        public void BoolEx_ToShortArray_列表重载逐项转换()
        {
            var input = new List<bool[]>
            {
                new[] { true },            // 1
                new[] { false, true },     // 2
                new bool[0],               // 0
            };
            Assert.Equal(new short[] { 1, 2, 0 }, input.ToShortArray());
        }

        // ══════════════════════════════════════════════════════════════════
        // DecimalEx
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void DecimalEx_ToBinary_不指定长度时不补零()
        {
            Assert.Equal("1010", 10.ToBinary());
            Assert.Equal("1010", ((short)10).ToBinary());
            Assert.Equal("0", 0.ToBinary());
        }

        [Fact]
        public void DecimalEx_ToBinary_指定长度时按PaddingLength补零()
        {
            Assert.Equal("00001010", 10.ToBinary(8));
            // 目标长度比实际短时，保留右对齐并截尾（与 StringEx.PaddingLength 一致）
            Assert.Equal("010", 10.ToBinary(3));
        }

        [Fact]
        public void DecimalEx_ToHex_是小写_与同族的其他ToHex相反()
        {
            // ⛔ 约定不一致：DecimalEx.ToHex 走 Convert.ToString(data, 16) —— **小写**；
            // 而 ASCIIEx.ToHex（默认）与 BinaryEx.ToHex 走 ToString("X") —— **大写**。
            // 三个 ToHex 同名不同大小写，是本库真实存在的不一致，用测试钉住以免被"顺手统一"。
            Assert.Equal("ff", 255.ToHex());
            Assert.Equal("00ff", 255.ToHex(4));
            Assert.Equal("a", 10.ToHex());
        }

        [Fact]
        public void 十六进制大小写对照_ASCIIEx与BinaryEx大写_DecimalEx小写()
        {
            // 三个 ToHex 同名、大小写策略不同（全部显式限定，避免重载决议干扰）：
            Assert.Equal("C3BF", ASCIIEx.ToHex("\u00ff"));        // ASCIIEx：默认大写（UTF8: C3 BF）
            Assert.Equal("c3bf", ASCIIEx.ToHex("\u00ff", false)); // ASCIIEx：显式小写
            Assert.Equal("AB", BinaryEx.ToHex("10101011"));       // BinaryEx：固定大写（0xAB）
            Assert.Equal("ab", 171.ToHex());                      // DecimalEx：固定小写（0xAB）
        }

        [Fact]
        public void DecimalEx_ToASC_int按十六进制展开()
        {
            Assert.Equal("A", 65.ToASC());            // 0x41
            Assert.Equal("AB", 16706.ToASC());        // 0x4142
        }

        [Fact]
        public void DecimalEx_ToASC_short数组默认大端()
        {
            Assert.Equal("AB", new short[] { 0x4142 }.ToASC(EEndianType.Big_Endian));
            Assert.Equal("BA", new short[] { 0x4142 }.ToASC(EEndianType.Little_Endian));
        }

        [Fact]
        public void DecimalEx_ToASC_ushort数组默认大端()
        {
            Assert.Equal("AB", new ushort[] { 0x4142 }.ToASC());
            Assert.Equal("BA", new ushort[] { 0x4142 }.ToASC(EEndianType.Little_Endian));
        }

        [Fact]
        public void DecimalEx_ToASC_无参重载走的是小端_与显式大端结果相反()
        {
            // ⛔ 重载陷阱：short[] 有两个 ToASC 重载
            //   ToASC(this short[] data)                                   <- 走 Buffer.BlockCopy，即机器字节序（小端）
            //   ToASC(this short[] data, EEndianType = Big_Endian)          <- 走显式大端
            // 无参调用按重载决议会绑定到**前者**，于是 data.ToASC() 与小端一致、与"默认大端"相反。
            // 读代码的人几乎一定会以为无参 = 大端。已记入 FILE-TIERING.md §5.12。
            var data = new short[] { 0x4142 };
            Assert.Equal("BA", data.ToASC());                                   // 实际：小端
            Assert.Equal("AB", data.ToASC(EEndianType.Big_Endian));             // 显式大端
            Assert.NotEqual(data.ToASC(), data.ToASC(EEndianType.Big_Endian));  // 两者相反
        }

        [Fact]
        public void DecimalEx_ToASC_空数组返回空串()
        {
            Assert.Equal(string.Empty, new short[0].ToASC());
            Assert.Equal(string.Empty, new ushort[0].ToASC());
        }

        // ══════════════════════════════════════════════════════════════════
        // ReadOnlySpanEx
        // ══════════════════════════════════════════════════════════════════

        // ⛔ 两个坑：
        //   1) 扩展方法的接收器**不做隐式转换**：`new byte[]{..}.AsSpan()` 得到 Span<byte>，
        //      而 ToUInt 声明在 ReadOnlySpan<byte> 上 ⇒ CS1929，必须显式声明 ReadOnlySpan<byte> 局部变量。
        //   2) ReadOnlySpan<byte> 是 ref struct，**不能被 lambda 捕获** ⇒ Assert.Throws 的
        //      lambda 里必须就地新建 span，不能在外部声明后捕获。

        [Fact]
        public void ReadOnlySpanEx_ToUInt_小端()
        {
            ReadOnlySpan<byte> two = new byte[] { 0x01, 0x02 };
            Assert.Equal(0x0201u, two.ToUInt());

            ReadOnlySpan<byte> four = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            Assert.Equal(0x04030201u, four.ToUInt());
        }

        [Fact]
        public void ReadOnlySpanEx_ToUInt_大端()
        {
            ReadOnlySpan<byte> two = new byte[] { 0x01, 0x02 };
            Assert.Equal(0x0102u, two.ToUInt(isLE: false));

            ReadOnlySpan<byte> four = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            Assert.Equal(0x01020304u, four.ToUInt(isLE: false));
        }

        [Fact]
        public void ReadOnlySpanEx_ToUInt_可指定起点与长度()
        {
            ReadOnlySpan<byte> buf = new byte[] { 0xFF, 0x01, 0x02, 0xFF };
            Assert.Equal(0x0201u, buf.ToUInt(1, 2));
            Assert.Equal(0x0102u, buf.ToUInt(1, 2, isLE: false));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        public void ReadOnlySpanEx_ToUInt_byteCount越界抛异常(int byteCount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                ReadOnlySpan<byte> buf = new byte[] { 1, 2, 3, 4 };
                buf.ToUInt(0, byteCount);
            });
        }

        [Fact]
        public void ReadOnlySpanEx_ToUInt_startIndex越界抛异常()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                ReadOnlySpan<byte> buf = new byte[] { 1, 2 };
                buf.ToUInt(-1, 2);
            });
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                ReadOnlySpan<byte> buf = new byte[] { 1, 2 };
                buf.ToUInt(1, 2);
            });
        }

        [Fact]
        public void ReadOnlySpanEx_ToUInt_单字节与三字节都合法()
        {
            // 代码注释写的是 byteCount 允许 1..4，实测确认边界内的 1 与 3 都放行
            ReadOnlySpan<byte> one = new byte[] { 0x7F };
            Assert.Equal(0x7Fu, one.ToUInt());

            ReadOnlySpan<byte> three = new byte[] { 0x01, 0x02, 0x03 };
            Assert.Equal(0x030201u, three.ToUInt());
            Assert.Equal(0x010203u, three.ToUInt(isLE: false));
        }

        // ══════════════════════════════════════════════════════════════════
        // DateTimeEx
        // ══════════════════════════════════════════════════════════════════

        private static readonly DateTime Sample = new DateTime(2026, 9, 18, 14, 5, 6, 789);

        [Fact]
        public void DateTimeEx_ToDateTime_带斜杠与秒()
        {
            Assert.Equal("2026/09/18 14:05:06", Sample.ToDateTime());
        }

        [Fact]
        public void DateTimeEx_ToTime_与ToDate_ToYearMonth()
        {
            Assert.Equal("14:05:06", Sample.ToTime());
            Assert.Equal("2026/09/18", Sample.ToDate());
            Assert.Equal("2026/09", Sample.ToYearMonth());
        }

        [Fact]
        public void DateTimeEx_ToDateNOSapce_无分隔符()
        {
            Assert.Equal("20260918", Sample.ToDateNOSapce());
        }

        [Fact]
        public void DateTimeEx_ToTimeMs_不补零_位数不固定()
        {
            // ⚠️ 实现是 Hour.ToString() + Minute.ToString() + ...，**没有补零**，
            // 所以结果既不是定长也不可直接按位置切片解析。
            Assert.Equal("1456789", Sample.ToTimeMs());

            var padded = new DateTime(2026, 1, 2, 3, 4, 5, 6);
            Assert.Equal("3456", padded.ToTimeMs());   // 3/4/5/6 全部个位数
        }

        [Fact]
        public void DateTimeEx_ToYYYYMMDDHHMMSSFFF_小时用的是12小时制hh()
        {
            // ⚠️ 格式串是 "yyyyMMddhhmmssfff"，其中 hh = 12 小时制。
            // 14:05 -> "02"，且没有 AM/PM 标记 ⇒ 14 点和 2 点产出同一个字符串。
            Assert.Equal("20260918020506789", Sample.ToYYYYMMDDHHMMSSFFF());

            var twoPm = new DateTime(2026, 9, 18, 14, 5, 6, 789);
            var twoAm = new DateTime(2026, 9, 18, 2, 5, 6, 789);
            Assert.Equal(twoAm.ToYYYYMMDDHHMMSSFFF(), twoPm.ToYYYYMMDDHHMMSSFFF());
        }

        [Fact]
        public void DateTimeEx_ToSeconds_基于毫秒换算()
        {
            Assert.Equal(0.0, new Stopwatch().ToSeconds());   // 未启动 -> 0
            var sw = Stopwatch.StartNew();
            Assert.True(sw.ToSeconds() >= 0.0);
            Assert.True(sw.ToSeconds() < 60.0);
        }

        // ══════════════════════════════════════════════════════════════════
        // RegexEx
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void RegexEx_ReplaceNum_默认删除所有数字()
        {
            Assert.Equal("abc", "abc123".ReplaceNum());
            Assert.Equal("ab", "1a2b3".ReplaceNum());
        }

        [Fact]
        public void RegexEx_ReplaceNum_只删末尾数字时保留中间数字()
        {
            Assert.Equal("abc", "abc123".ReplaceNum(OnlyDeleteEnd: true));
            Assert.Equal("1a2b", "1a2b3".ReplaceNum(OnlyDeleteEnd: true));
            Assert.Equal("a1b2c", "a1b2c".ReplaceNum(OnlyDeleteEnd: true)); // 末尾不是数字 -> 原样
        }

        [Fact]
        public void RegexEx_ReplaceNum_null返回null()
        {
            Assert.Null(((string)null).ReplaceNum());
        }

        [Fact]
        public void RegexEx_ReplaceLetter_删除英文字母()
        {
            Assert.Equal("12", "a1b2".ReplaceLetter());
            Assert.Equal("12", "A1B2".ReplaceLetter());
            Assert.Null(((string)null).ReplaceLetter());
        }

        [Fact]
        public void RegexEx_CleanString_只保留十六进制字符()
        {
            Assert.Equal("ABcd", "AB cd\n".CleanString());
            Assert.Equal("12", "ZZ 12\n".CleanString());
            Assert.Equal("", "".CleanString());
            Assert.Equal("", ((string)null).CleanString());
        }

        [Fact]
        public void RegexEx_RemoveControl_去掉控制符但保留可见字符()
        {
            Assert.Equal("abc", "a\u0001b\u007Fc".RemoveControl());
            Assert.Equal("a b", "a b".RemoveControl());          // 空格(0x20)不在删除范围
            Assert.Equal("a\tb", "a\tb".RemoveControl());        // \t(0x09) 不在删除范围
        }

        [Fact]
        public void RegexEx_SplitByDateTime_按时间块切分()
        {
            var text = "2025/10/11 12:09:20 hello2025/10/12 08:00:00 world";
            var parts = text.SplitByDateTime();
            Assert.Equal(2, parts.Length);
            Assert.Equal("2025/10/11 12:09:20 hello", parts[0]);
            Assert.Equal("2025/10/12 08:00:00 world", parts[1]);
        }

        [Fact]
        public void RegexEx_SplitByDateTime_空输入返回空数组()
        {
            Assert.Empty("".SplitByDateTime());
            Assert.Empty(((string)null).SplitByDateTime());
        }

        [Fact]
        public void RegexEx_FindMatches_非全词匹配可重叠扫描()
        {
            var hits = "aXbXc".FindMatches("X", wholeWord: false);
            Assert.Equal(2, hits.Count);
            Assert.Equal((1, 1), hits[0]);
            Assert.Equal((3, 1), hits[1]);
        }

        [Fact]
        public void RegexEx_FindMatches_区分大小写开关()
        {
            Assert.Equal(2, "abcABC".FindMatches("abc", wholeWord: false).Count);
            var sensitive = "abcABC".FindMatches("abc", wholeWord: false, caseSensitive: true);
            Assert.Single(sensitive);
            Assert.Equal((0, 3), sensitive[0]);
        }

        [Fact]
        public void RegexEx_FindMatches_全词匹配会排除更长单词内的命中()
        {
            var hits = "abc abcd abc".FindMatches("abc", wholeWord: true);
            Assert.Equal(2, hits.Count);
            Assert.Equal((0, 3), hits[0]);
            Assert.Equal((9, 3), hits[1]);
        }

        [Fact]
        public void RegexEx_FindMatches_空参数返回空集合()
        {
            Assert.Empty(((string)null).FindMatches("x", wholeWord: false));
            Assert.Empty("abc".FindMatches(null, wholeWord: false));
            Assert.Empty("abc".FindMatches("", wholeWord: false));
        }

        [Fact]
        public void RegexEx_OrderByDescending_按长度降序并转义()
        {
            var sorted = new[] { "a", "ccc", "bb" }.OrderByDescending().ToArray();
            Assert.Equal(new[] { "ccc", "bb", "a" }, sorted);
        }

        [Fact]
        public void RegexEx_OrderByDescending_结果已转义可直接拼正则()
        {
            // 名字叫 OrderByDescending 但实际还做了 Regex.Escape —— 调用方依赖它拼"最长优先"模式
            var sorted = new[] { "a+b", "ab" }.OrderByDescending().ToArray();
            Assert.Equal(new[] { @"a\+b", "ab" }, sorted);
        }

        // ══════════════════════════════════════════════════════════════════
        // API 面 —— 把「只能靠编译器发现」的冲突用反射钉住
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void API面_字符串扩展方法重名冲突_ToASC有两个同签名实现()
        {
            // HexEx.ToASC(string) 与 BinaryEx.ToASC(string) 签名完全一致、又在同一命名空间，
            // 所以 `using Extension.Core.Conversion;` 之后写 `"41".ToASC()` 必然 CS0121。
            // 这不是运行时行为，编译器才看得见；这里用反射把它固化成可见的契约，
            // 避免后来者以为"能这么调"、或者以为其中一个是多余的。
            var stringExtensions = new[]
            {
                typeof(HexEx), typeof(BinaryEx), typeof(ASCIIEx), typeof(DecimalEx),
                typeof(DateTimeEx), typeof(RegexEx), typeof(StringEx),
            };

            var toAscOnString = stringExtensions
                .SelectMany(t => t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                .Where(m => m.Name == "ToASC")
                .Where(m => m.GetParameters().Length >= 1
                            && m.GetParameters()[0].ParameterType == typeof(string))
                .Select(m => m.DeclaringType!.Name)
                .OrderBy(n => n)
                .ToArray();

            Assert.Equal(new[] { "BinaryEx", "HexEx" }, toAscOnString);
        }

        [Fact]
        public void API面_同一个ToHex在三个类里大小写约定不同()
        {
            // 三个 ToHex 都在 Extension.Core.Conversion，名字一样、大小写策略不一样。
            // 反射只能证明"存在"，大小写差异由上面 DecimalEx / 大小写对照 两条用例钉住。
            var owners = new[] { typeof(ASCIIEx), typeof(BinaryEx), typeof(DecimalEx) }
                .Where(t => t.GetMethods().Any(m => m.Name == "ToHex"))
                .Select(t => t.Name)
                .OrderBy(n => n)
                .ToArray();

            Assert.Equal(new[] { "ASCIIEx", "BinaryEx", "DecimalEx" }, owners);
        }

        [Fact]
        public void API面_所有扩展方法所在类型都是静态类()
        {
            // 扩展方法必须宿主于静态类；顺带确认这些类都不是泛型/抽象，避免下游误当实例方法用
            var types = new[]
            {
                typeof(HexEx), typeof(BinaryEx), typeof(ASCIIEx), typeof(BoolEx),
                typeof(DecimalEx), typeof(DateTimeEx), typeof(RegexEx), typeof(StringEx),
                typeof(ReadOnlySpanEx),
            };

            Assert.All(types, t =>
            {
                Assert.True(t.IsAbstract && t.IsSealed, $"{t.Name} 应当是 static class");
                Assert.False(t.IsGenericTypeDefinition, $"{t.Name} 不应当是泛型");
            });
        }
    }
}
