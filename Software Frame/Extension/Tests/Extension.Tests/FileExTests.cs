// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FileExTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.IO、System.Reflection、System.Runtime.CompilerServices、Extension.IO.Files、Extension.IO.Csv。导入不代表所有类型都会在运行时加载。
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Extension.IO.Files;
using Extension.IO.Csv;
using Extension.IO.Retention;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// T0 层「文件读写」专项：<c>Extension.IO.Retention.FileEx</c>。
    /// 只有 2 个方法，但 <c>Write</c> 带一套「写前轮换 <c>.bak</c>」的副作用语义，
    /// 此前零用例覆盖。硬规则 1：下面钉住的 4 处行为**只记录、不顺手改**。
    /// <list type="number">
    /// <item><b><c>.bak</c> 只保留一代</b>：每次覆盖都把上一版滚成 <c>.bak</c>（<c>overwrite: true</c>），
    /// 所以第 N 次写入后 <c>.bak</c> 里是第 N-1 版，更早的版本永久丢失；</item>
    /// <item><b>备份失败不阻断写入</b>：<c>File.Copy</c> 的异常被
    /// <c>when (IOException or UnauthorizedAccessException or NotSupportedException or
    /// ArgumentException or SecurityException)</c> 过滤器吞掉，只留 <c>Debug.WriteLine</c> 痕迹
    /// ⇒ 发布版（无 Debug 监听器）**完全静默**；</item>
    /// <item><b><c>data</c> 为 <c>null</c> 不抛异常，而是把文件截断成 0 字节</b>
    /// —— <c>File.WriteAllText</c> 只校验 path 不校验 contents，<c>StreamWriter.Write(null)</c> 是 no-op，
    /// 但 <c>StreamWriter</c> 构造时文件**已经被截断** ⇒ 静默数据丢失（旧内容此时只剩 <c>.bak</c> 一份）；</item>
    /// <item><b>API 面不对称</b>：<c>Write</c> 是 <c>this string</c> 扩展方法，<c>Read</c> 不是
    /// （<c>Read(filePath, fileName)</c> 必须写成静态调用）。</item>
    /// </list>
    /// </summary>
    public class FileExTests
    {
        // ══════════════════════════════════════════════════════════════════
        // Read
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 测试相关入口 Read_读取已存在文件_返回全部文本；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WriteFile、Equal、Read；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Read_读取已存在文件_返回全部文本()
        {
            using var ws = new TempWorkspace("FileExTests");
            ws.WriteFile("a.txt", "hello 世界");

            Assert.Equal("hello 世界", FileEx.Read(ws.Root, "a.txt"));
        }

        /// <summary>
        /// 测试相关入口 Read_文件不存在_抛FileNotFoundException；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Throws、Read；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Read_文件不存在_抛FileNotFoundException()
        {
            using var ws = new TempWorkspace("FileExTests");

            Assert.Throws<FileNotFoundException>(() => FileEx.Read(ws.Root, "nope.txt"));
        }

        /// <summary>
        /// 测试相关入口 Read_fileName是绝对路径时_忽略filePath；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WriteFile、Combine、Equal、Read；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Read_fileName是绝对路径时_忽略filePath()
        {
            // Path.Combine(base, rooted) 在 .NET 里**直接返回 rooted**，base 被丢弃。
            // 后果：把绝对路径误传进 fileName，Read 会静默读到另一个位置的文件。
            using var ws = new TempWorkspace("FileExTests");
            string rooted = ws.WriteFile(Path.Combine("sub", "b.txt"), "X");

            Assert.Equal("X", FileEx.Read(ws.Root, rooted));
        }

        // ══════════════════════════════════════════════════════════════════
        // Write —— .bak 轮换
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void Write_首次写入_不产生bak()
        {
            using var ws = new TempWorkspace("FileExTests");

            "v1".Write(ws.Root, "f.txt");

            Assert.Equal("v1", File.ReadAllText(ws.Combine("f.txt")));
            Assert.False(File.Exists(ws.Combine("f.txt.bak")));
        }

        /// <summary>
        /// 测试相关入口 Write_覆盖写入时_旧内容轮换为bak；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Write、Equal、ReadAllText、Combine；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Write_覆盖写入时_旧内容轮换为bak()
        {
            using var ws = new TempWorkspace("FileExTests");

            "v1".Write(ws.Root, "f.txt");
            "v2".Write(ws.Root, "f.txt");

            Assert.Equal("v2", File.ReadAllText(ws.Combine("f.txt")));
            Assert.Equal("v1", File.ReadAllText(ws.Combine("f.txt.bak")));
        }

        /// <summary>
        /// 测试相关入口 Write_bak只保留一代_三次写入后bak里是倒数第二版；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Write、Equal、ReadAllText、Combine、Single；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Write_bak只保留一代_三次写入后bak里是倒数第二版()
        {
            using var ws = new TempWorkspace("FileExTests");

            "v1".Write(ws.Root, "f.txt");
            "v2".Write(ws.Root, "f.txt");
            "v3".Write(ws.Root, "f.txt");

            Assert.Equal("v3", File.ReadAllText(ws.Combine("f.txt")));
            Assert.Equal("v2", File.ReadAllText(ws.Combine("f.txt.bak")));   // 不是 v1
            Assert.Single(Directory.GetFiles(ws.Root, "*.bak"));             // 只有一代备份
        }

        // ══════════════════════════════════════════════════════════════════
        // Write —— filePath 空分支
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void Write_filePath为空字符串时_直接以fileName作为路径()
        {
            using var ws = new TempWorkspace("FileExTests");
            string absolute = ws.Combine("rel.txt");   // fileName 传绝对路径，避免写到进程工作目录

            "v".Write(string.Empty, absolute);

            Assert.Equal("v", File.ReadAllText(absolute));
        }

        /// <summary>
        /// 测试相关入口 Write_filePath为null时_直接以fileName作为路径；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Combine、Write、Equal、ReadAllText；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Write_filePath为null时_直接以fileName作为路径()
        {
            using var ws = new TempWorkspace("FileExTests");
            string absolute = ws.Combine("rel2.txt");

            "v".Write(null, absolute);

            Assert.Equal("v", File.ReadAllText(absolute));
        }

        // ══════════════════════════════════════════════════════════════════
        // Write —— 异常 / 边界
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void Write_空字符串内容_写入0字节文件()
        {
            using var ws = new TempWorkspace("FileExTests");

            string.Empty.Write(ws.Root, "empty.txt");

            Assert.True(File.Exists(ws.Combine("empty.txt")));
            Assert.Equal(0L, new FileInfo(ws.Combine("empty.txt")).Length);
        }

        /// <summary>
        /// 测试相关入口 Write_数据为null时不抛异常_而是把文件静默清空；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Write、Combine、Null、Exception、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Write_数据为null时不抛异常_而是把文件静默清空()
        {
            // 实测（.NET）：File.WriteAllText(path, null) 不抛 ArgumentNullException。
            // 它只校验 path，随后 new StreamWriter(path, false, ...) **先截断文件**，
            // 再 writer.Write(null) —— 而 TextWriter.Write(string) 对 null 是 no-op。
            // ⇒ 结果是一个 0 字节文件，且没有任何异常。旧内容此时只剩 .bak 一份。
            using var ws = new TempWorkspace("FileExTests");
            "v1".Write(ws.Root, "f.txt");
            string path = ws.Combine("f.txt");

            Assert.Null(Record.Exception(() => ((string)null).Write(ws.Root, "f.txt")));

            Assert.Equal(0L, new FileInfo(path).Length);
            Assert.Equal("v1", File.ReadAllText(ws.Combine("f.txt.bak")));
        }

        /// <summary>
        /// 测试相关入口 Write_目标目录不存在时_抛DirectoryNotFoundException；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Throws、Write、Combine；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Write_目标目录不存在时_抛DirectoryNotFoundException()
        {
            using var ws = new TempWorkspace("FileExTests");

            Assert.Throws<DirectoryNotFoundException>(
                () => "v".Write(ws.Combine("missing"), "f.txt"));
        }

        /// <summary>
        /// 测试相关入口 Write_备份失败不阻断写入_异常被过滤器吞掉；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Write、CreateDirectory、Combine、Null、Exception；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Write_备份失败不阻断写入_异常被过滤器吞掉()
        {
            // 让 "f.txt.bak" 变成一个**目录**：File.Copy(src, dest, true) 在 dest 是目录时
            // 抛 IOException（"目标文件是目录，不是文件"），正好落在 catch 的过滤器里。
            // 这是唯一能不依赖"磁盘满/只读"就能稳定复现备份失败的构造方式。
            using var ws = new TempWorkspace("FileExTests");
            "v1".Write(ws.Root, "f.txt");
            Directory.CreateDirectory(ws.Combine("f.txt.bak"));

            Assert.Null(Record.Exception(() => "v2".Write(ws.Root, "f.txt")));

            Assert.Equal("v2", File.ReadAllText(ws.Combine("f.txt")));
        }

        // ══════════════════════════════════════════════════════════════════
        // API 面
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void API面_Write是扩展方法_Read不是()
        {
            MethodInfo write = typeof(FileEx).GetMethod("Write", BindingFlags.Public | BindingFlags.Static);
            MethodInfo read = typeof(FileEx).GetMethod("Read", BindingFlags.Public | BindingFlags.Static);

            Assert.True(write.IsDefined(typeof(ExtensionAttribute), inherit: false));
            Assert.Equal(typeof(string), write.GetParameters()[0].ParameterType);
            Assert.Equal(typeof(void), write.ReturnType);

            Assert.False(read.IsDefined(typeof(ExtensionAttribute), inherit: false));
            Assert.Equal(typeof(string), read.ReturnType);
        }

        /// <summary>
        /// 测试相关入口 FileEx_是静态类_2个公开静态方法；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 True、Equal、GetMethods；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void FileEx_是静态类_2个公开静态方法()
        {
            Type t = typeof(FileEx);

            Assert.True(t.IsAbstract && t.IsSealed);   // static class 的元数据特征
            Assert.Equal(2, t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Length);
        }
    }
}
