using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Extension.Core.Buffers;
using Extension.IO.Compression;
using Extension.Integration.Cloning;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// T0 层「GZIP 解压」专项：<c>Extension.Integration.Cloning.ZIPEX</c>（只有 1 个方法）。
    /// 此前零用例覆盖。
    /// <para>
    /// 钉住的 2 处行为（硬规则 1：只记录不顺手改）：
    /// <list type="number">
    /// <item><b>内部用 <c>new StreamReader(gzipStream)</c>，即默认 UTF-8 + BOM 探测</b>
    /// ⇒ ① 带 BOM 的负载会把 BOM **静默吃掉**（返回值比原文短 1 个字符）；
    /// ② 非 UTF-8 负载（如 GBK 日志）不会报错，而是**静默替换成 U+FFFD**
    /// —— 解出来是乱码但调用方无从察觉；</item>
    /// <item><b>只解不压、且没有 <c>TryGZIPExtractToString</c> 之类的容错入口</b>：
    /// 文件缺失抛 <c>FileNotFoundException</c>、内容不是 GZIP 抛 <c>InvalidDataException</c>，
    /// 都由调用方自己兜。</item>
    /// </list>
    /// </para>
    /// </summary>
    public class ZipExTests
    {
        private static string WriteGzipBytes(string path, byte[] payload)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var gz = new GZipStream(fs, CompressionMode.Compress))
            {
                gz.Write(payload, 0, payload.Length);
            }
            return path;
        }

        private static string WriteGzipText(string path, string content)
        {
            // 用 UTF8Encoding(false) 只影响 GetPreamble()，不影响内容里的 U+FEFF 字符
            return WriteGzipBytes(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content));
        }

        // ══════════════════════════════════════════════════════════════════
        // 正常往返
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void GZIPExtractToString_往返_ASCII()
        {
            using var ws = new TempWorkspace("ZipExTests");
            string path = WriteGzipText(ws.Combine("a.gz"), "hello world");

            Assert.Equal("hello world", ZIPEX.GZIPExtractToString(path));
        }

        [Fact]
        public void GZIPExtractToString_往返_中文UTF8()
        {
            using var ws = new TempWorkspace("ZipExTests");
            string path = WriteGzipText(ws.Combine("cn.gz"), "中文内容：换行\n制表\t结束");

            Assert.Equal("中文内容：换行\n制表\t结束", ZIPEX.GZIPExtractToString(path));
        }

        [Fact]
        public void GZIPExtractToString_空内容_返回空字符串()
        {
            using var ws = new TempWorkspace("ZipExTests");
            string path = WriteGzipText(ws.Combine("empty.gz"), string.Empty);

            Assert.Equal(string.Empty, ZIPEX.GZIPExtractToString(path));
        }

        [Fact]
        public void GZIPExtractToString_大内容_往返一致()
        {
            using var ws = new TempWorkspace("ZipExTests");
            var sb = new StringBuilder();
            for (int i = 0; i < 4096; i++)
            {
                sb.Append("行").Append(i).Append("：0123456789abcdef\r\n");
            }
            string content = sb.ToString();
            string path = WriteGzipText(ws.Combine("big.gz"), content);

            Assert.Equal(content, ZIPEX.GZIPExtractToString(path));
        }

        // ══════════════════════════════════════════════════════════════════
        // 编码陷阱
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void GZIPExtractToString_带BOM的负载_BOM会被静默吃掉()
        {
            // StreamReader 默认 detectEncodingFromByteOrderMarks: true ⇒ 读到 EF BB BF 会剥掉。
            // 对"GZIP 里装的是 UTF-8 文本"这个场景，等于返回值比压缩前少了一个字符。
            using var ws = new TempWorkspace("ZipExTests");
            string path = WriteGzipText(ws.Combine("bom.gz"), "\uFEFFhello");

            string result = ZIPEX.GZIPExtractToString(path);

            Assert.Equal("hello", result);
            Assert.NotEqual("\uFEFFhello", result);
        }

        [Fact]
        public void GZIPExtractToString_非UTF8负载_静默替换为UFFFD而不报错()
        {
            // GBK 的 "中" = 0xD6 0xD0，不是合法 UTF-8。
            // 默认 UTF8Encoding 的 DecoderFallback 是 ReplacementFallback ⇒ 不抛异常，只出 U+FFFD。
            // 后果：解 GBK 日志得到一串 "�" 却没有任何错误信号。
            using var ws = new TempWorkspace("ZipExTests");
            string path = WriteGzipBytes(ws.Combine("gbk.gz"), new byte[] { 0xD6, 0xD0 });

            string result = ZIPEX.GZIPExtractToString(path);

            Assert.Contains("\uFFFD", result);
        }

        // ══════════════════════════════════════════════════════════════════
        // 异常
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void GZIPExtractToString_文件不存在_抛FileNotFoundException()
        {
            using var ws = new TempWorkspace("ZipExTests");

            Assert.Throws<FileNotFoundException>(
                () => ZIPEX.GZIPExtractToString(ws.Combine("missing.gz")));
        }

        [Fact]
        public void GZIPExtractToString_内容不是GZIP_抛InvalidDataException()
        {
            using var ws = new TempWorkspace("ZipExTests");
            string path = ws.WriteFile("plain.txt", "这不是 gzip，只是一段普通文本");

            Assert.Throws<InvalidDataException>(() => ZIPEX.GZIPExtractToString(path));
        }

        // ══════════════════════════════════════════════════════════════════
        // API 面
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void API面_GZIPExtractToString不是扩展方法()
        {
            MethodInfo m = typeof(ZIPEX).GetMethod("GZIPExtractToString", BindingFlags.Public | BindingFlags.Static);

            Assert.False(m.IsDefined(typeof(ExtensionAttribute), inherit: false));
            Assert.Equal(typeof(string), m.ReturnType);
            Assert.Single(m.GetParameters());
        }

        [Fact]
        public void ZIPEX_是静态类_1个公开静态方法()
        {
            Type t = typeof(ZIPEX);

            Assert.True(t.IsAbstract && t.IsSealed);
            Assert.Single(t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        }
    }
}
