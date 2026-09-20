using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Extension.IO.Files;
using Extension.IO.Csv;
using Extension.IO.Retention;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// T0 层「目录操作」专项：<c>Extension.IO.Retention.DirectoryEx</c>（9 个扩展方法）。
    /// 这是"按 年/月/日/产品ID 分层落盘"这套约定的公共底座，此前零用例覆盖。
    /// <para>
    /// 钉住的 6 处「只在运行时暴露」的行为（硬规则 1：只记录不顺手改）：
    /// <list type="number">
    /// <item><b><c>GetParentSecond</c> 对根路径抛 <c>NullReferenceException</c></b>
    /// —— 它直接 <c>Directory.GetParent(pPath).Parent</c>，**没做第一层 null 检查**
    /// （<c>GetParent</c> 做了，所以同一根路径下一个安全一个崩）；</item>
    /// <item><b><c>GetDirName</c> 的 null 检查是死代码</b>：<c>new DirectoryInfo(...)</c> 永不返回 null
    /// （空串直接抛 <c>ArgumentException</c>）⇒ <c>return null</c> 不可达；</item>
    /// <item><b><c>CreatDir</c> / <c>GetPath</c> 的月、日**不补零**</b>
    /// ⇒ 生成 <c>2026\9\18</c> 而不是 <c>2026\09\18</c>，跨月排序（字符串序）会错乱；</item>
    /// <item><b><c>CreatDir</c> 名不副实</b>：叫 Creat 但**只拼路径、不落盘**，
    /// 且 <c>pID</c> 为 <c>null</c> 与空串都少一层（<c>Path.Combine(p, "")</c> 返回 <c>p</c>）；</item>
    /// <item><b><c>DirCopy</c> 用 <c>file.Replace(srcPath, destPath)</c> 做路径换算</b>
    /// —— 纯字符串替换 ⇒ 源路径带尾分隔符而目标不带时，分隔符被吃掉，
    /// 文件会落到**目标目录之外**（<c>...\s\f.txt</c> → <c>...\df.txt</c>）；</item>
    /// <item><b><c>GetFiles(filter)</c> 的过滤是"包含"匹配</b>（拼成 <c>*filter*</c>），
    /// 既不是前缀也不是后缀，传 <c>".txt"</c> 能过、传 <c>"b"</c> 也能过。</item>
    /// </list>
    /// </para>
    /// </summary>
    public class DirectoryExTests
    {
        /// <summary>把 <paramref name="path"/> 相对 <paramref name="basePath"/> 的剩余部分切成段。</summary>
        private static string[] Segments(string path, string basePath)
        {
            string rest = path.Substring(basePath.Length).TrimStart('\\', '/');
            return rest.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        }

        // ══════════════════════════════════════════════════════════════════
        // JudgeDirExist
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void JudgeDirExist_多级目录不存在时_递归创建()
        {
            using var ws = new TempWorkspace("DirectoryExTests");
            string deep = ws.Combine("a", "b", "c");

            deep.JudgeDirExist();

            Assert.True(Directory.Exists(deep));
        }

        [Fact]
        public void JudgeDirExist_目录已存在时_幂等且不抛()
        {
            using var ws = new TempWorkspace("DirectoryExTests");

            Assert.Null(Record.Exception(() => ws.Root.JudgeDirExist()));
            Assert.Null(Record.Exception(() => ws.Root.JudgeDirExist()));

            Assert.True(Directory.Exists(ws.Root));
        }

        // ══════════════════════════════════════════════════════════════════
        // GetFiles
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void GetFiles_过滤是包含匹配_既非前缀也非后缀()
        {
            using var ws = new TempWorkspace("DirectoryExTests");
            ws.WriteFile("a.txt", "1");
            ws.WriteFile("ba.txt", "2");
            ws.WriteFile("b.txt", "3");
            ws.WriteFile("b.log", "4");

            Assert.Equal(3, ws.Root.GetFiles("b").Length);       // ba.txt / b.txt / b.log
            Assert.Equal(3, ws.Root.GetFiles(".txt").Length);    // a.txt / ba.txt / b.txt
            Assert.Equal(4, ws.Root.GetFiles(".").Length);       // 全中（b.log 也含 "."）
            Assert.Empty(ws.Root.GetFiles("zzz"));
        }

        [Fact]
        public void GetFiles_返回FileInfo数组_可读长度()
        {
            using var ws = new TempWorkspace("DirectoryExTests");
            ws.WriteFile("data.txt", "12345");

            FileInfo[] found = ws.Root.GetFiles("data");

            FileInfo one = Assert.Single(found);
            Assert.Equal("data.txt", one.Name);
            Assert.Equal(5L, one.Length);
        }

        [Fact]
        public void GetFiles_目录不存在时_抛DirectoryNotFoundException()
        {
            using var ws = new TempWorkspace("DirectoryExTests");

            Assert.Throws<DirectoryNotFoundException>(() => ws.Combine("missing").GetFiles("x"));
        }

        // ══════════════════════════════════════════════════════════════════
        // CreatDir / GetPath
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void CreatDir_结构是基路径加年月日加产品ID()
        {
            using var ws = new TempWorkspace("DirectoryExTests");
            DateTime now = DateTime.Now;

            string[] segs = Segments(ws.Root.CreatDir("P1"), ws.Root);

            Assert.Equal(4, segs.Length);
            Assert.Equal(now.Year.ToString(), segs[0]);          // 年份固定 4 位，无补零歧义
            Assert.Equal("P1", segs[3]);
            Assert.InRange(int.Parse(segs[1]), 1, 12);
            Assert.InRange(int.Parse(segs[2]), 1, 31);
            // 不补零：若补零，segs[1] 会是 "09" 而 int.Parse("09").ToString() 是 "9"
            Assert.Equal(int.Parse(segs[1]).ToString(), segs[1]);
            Assert.Equal(int.Parse(segs[2]).ToString(), segs[2]);
        }

        [Fact]
        public void GetPath_月日不补零_一月二日是1与2而不是01与02()
        {
            // 用显式日期做**确定性**断言（CreatDir 内部取 DateTime.Now，无法注入时间）
            Assert.Equal(Path.Combine("2026", "1", "2"), new DateTime(2026, 1, 2).GetPath());
            Assert.Equal(Path.Combine("2026", "12", "31"), new DateTime(2026, 12, 31).GetPath());
        }

        [Fact]
        public void CreatDir_pID为null时_少一层()
        {
            using var ws = new TempWorkspace("DirectoryExTests");

            Assert.Equal(3, Segments(ws.Root.CreatDir(null), ws.Root).Length);
        }

        [Fact]
        public void CreatDir_pID为空字符串时_同样少一层()
        {
            // Path.Combine(p, "") 返回 p（不追加分隔符）⇒ 空串与 null 效果相同，
            // 但走的是两个不同分支（一个 if 内、一个 if 外）。
            using var ws = new TempWorkspace("DirectoryExTests");

            Assert.Equal(3, Segments(ws.Root.CreatDir(string.Empty), ws.Root).Length);
        }

        [Fact]
        public void CreatDir_名不副实_只拼路径不落盘()
        {
            using var ws = new TempWorkspace("DirectoryExTests");

            string path = ws.Root.CreatDir("P1");

            Assert.False(Directory.Exists(path));
        }

        // ══════════════════════════════════════════════════════════════════
        // ReadFile
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void ReadFile_目录不存在_返回null()
        {
            using var ws = new TempWorkspace("DirectoryExTests");

            Assert.Null(ws.Combine("missing").ReadFile());
        }

        [Fact]
        public void ReadFile_目录存在但没有子目录_返回空字典()
        {
            using var ws = new TempWorkspace("DirectoryExTests");
            ws.WriteFile("loose.txt", "x");   // 根目录下的散文件不算

            Dictionary<string, FileInfo[]> map = ws.Root.ReadFile();

            Assert.NotNull(map);
            Assert.Empty(map);
        }

        [Fact]
        public void ReadFile_按一级子目录名分组()
        {
            using var ws = new TempWorkspace("DirectoryExTests");
            ws.WriteFile(Path.Combine("P1", "x.txt"), "x");
            ws.WriteFile(Path.Combine("P1", "y.txt"), "y");
            ws.WriteFile(Path.Combine("P2", "z.txt"), "z");

            Dictionary<string, FileInfo[]> map = ws.Root.ReadFile();

            Assert.Equal(2, map.Count);
            Assert.Equal(2, map["P1"].Length);
            Assert.Single(map["P2"]);
        }

        [Fact]
        public void ReadFile_不递归_子目录里的文件不计入()
        {
            using var ws = new TempWorkspace("DirectoryExTests");
            ws.WriteFile(Path.Combine("P1", "sub", "deep.txt"), "d");

            Dictionary<string, FileInfo[]> map = ws.Root.ReadFile();

            Assert.Single(map);                    // 只有 P1 这个键
            Assert.Empty(map["P1"]);               // P1 下没有直接文件
        }

        // ══════════════════════════════════════════════════════════════════
        // GetParent / GetParentSecond
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void GetParent_返回上一级()
        {
            using var ws = new TempWorkspace("DirectoryExTests");
            string sub = ws.Combine("a", "b");
            Directory.CreateDirectory(sub);

            Assert.Equal(ws.Combine("a"), sub.GetParent());
        }

        [Fact]
        public void GetParent_根路径返回null_有null检查()
        {
            Assert.Null(@"C:\".GetParent());
        }

        [Fact]
        public void GetParentSecond_返回上二级()
        {
            using var ws = new TempWorkspace("DirectoryExTests");
            string deep = ws.Combine("a", "b", "c");
            Directory.CreateDirectory(deep);

            Assert.Equal(ws.Combine("a"), deep.GetParentSecond());
        }

        [Fact]
        public void GetParentSecond_根路径抛NullReferenceException_第一层没做null检查()
        {
            // 对比 GetParent（同一条根路径返回 null，安全）：
            // GetParentSecond 写的是 Directory.GetParent(pPath).Parent，
            // 第一层 GetParent 返回 null 后立刻解引用 ⇒ NRE。
            Assert.Throws<NullReferenceException>(() => @"C:\".GetParentSecond());
        }

        // ══════════════════════════════════════════════════════════════════
        // GetDirName
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void GetDirName_返回最后一段_忽略尾分隔符()
        {
            using var ws = new TempWorkspace("DirectoryExTests");
            string leaf = ws.Combine("a", "b", "c");

            Assert.Equal("c", leaf.GetDirName());
            Assert.Equal("c", (leaf + Path.DirectorySeparatorChar).GetDirName());
        }

        [Fact]
        public void GetDirName_不校验路径是否存在()
        {
            using var ws = new TempWorkspace("DirectoryExTests");

            Assert.Equal("ghost", ws.Combine("ghost").GetDirName());
        }

        [Fact]
        public void GetDirName_根路径返回根自身()
        {
            Assert.Equal(@"C:\", @"C:\".GetDirName());
        }

        [Fact]
        public void GetDirName_的null检查不可达_永不返回null()
        {
            // new DirectoryInfo(pPath) 永不返回 null（空串直接抛 ArgumentException）
            // ⇒ 源码里的 `if (dirInfo != null) { ... } return null;` 中 return null 是死代码。
            using var ws = new TempWorkspace("DirectoryExTests");

            Assert.NotNull(ws.Combine("ghost").GetDirName());
            Assert.NotNull(@"C:\".GetDirName());
            Assert.Throws<ArgumentException>(() => string.Empty.GetDirName());   // 空串走构造异常，不是返回 null
        }

        // ══════════════════════════════════════════════════════════════════
        // DirCopy
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task DirCopy_递归复制并保留目录结构()
        {
            using var ws = new TempWorkspace("DirectoryExTests");
            string src = ws.Combine("src");
            string dst = ws.Combine("dst");
            ws.WriteFile(Path.Combine("src", "f1.txt"), "1");
            ws.WriteFile(Path.Combine("src", "sub", "f2.txt"), "2");
            ws.WriteFile(Path.Combine("src", "sub", "deep", "f3.txt"), "3");

            await src.DirCopy(dst);

            Assert.Equal("1", File.ReadAllText(Path.Combine(dst, "f1.txt")));
            Assert.Equal("2", File.ReadAllText(Path.Combine(dst, "sub", "f2.txt")));
            Assert.Equal("3", File.ReadAllText(Path.Combine(dst, "sub", "deep", "f3.txt")));
        }

        [Fact]
        public async Task DirCopy_覆盖已存在的目标文件()
        {
            using var ws = new TempWorkspace("DirectoryExTests");
            string src = ws.Combine("src");
            string dst = ws.Combine("dst");
            ws.WriteFile(Path.Combine("src", "f1.txt"), "NEW");
            ws.WriteFile(Path.Combine("dst", "f1.txt"), "OLD");

            await src.DirCopy(dst);

            Assert.Equal("NEW", File.ReadAllText(Path.Combine(dst, "f1.txt")));
        }

        [Fact]
        public async Task DirCopy_源路径带尾分隔符时_文件会落到目标目录之外()
        {
            // ⚠️ 缺陷：destFile = file.Replace(srcPath, destPath) 是**纯字符串替换**。
            // srcPath 带尾分隔符而 destPath 不带 ⇒ 替换后那个分隔符一起消失：
            //   "...\s\f.txt".Replace("...\s\", "...\d") == "...\df.txt"
            // 目标目录名与文件名被粘成一个新名字，文件写到目标目录的**同级**去了。
            using var ws = new TempWorkspace("DirectoryExTests");
            string src = ws.Combine("s");
            string dst = ws.Combine("d");
            ws.WriteFile(Path.Combine("s", "f.txt"), "1");

            await (src + Path.DirectorySeparatorChar).DirCopy(dst);

            Assert.True(Directory.Exists(dst));                          // 目标目录被建出来了
            Assert.Empty(Directory.GetFiles(dst));                       // 但里面一个文件都没有
            Assert.True(File.Exists(dst + "f.txt"));                     // 落到了同级
            Assert.Equal("df.txt", Path.GetFileName(dst + "f.txt"));     // 目录名+文件名被粘住
            Assert.Equal("1", File.ReadAllText(dst + "f.txt"));
        }

        [Fact]
        public async Task DirCopy_源目录不存在时_抛DirectoryNotFoundException()
        {
            using var ws = new TempWorkspace("DirectoryExTests");

            await Assert.ThrowsAsync<DirectoryNotFoundException>(
                () => ws.Combine("missing").DirCopy(ws.Combine("x")));
        }

        [Fact]
        public void DirCopy_是扩展方法且返回Task()
        {
            MethodInfo m = typeof(DirectoryEx).GetMethod("DirCopy", BindingFlags.Public | BindingFlags.Static);

            Assert.True(m.IsDefined(typeof(ExtensionAttribute), inherit: false));
            Assert.Equal(typeof(Task), m.ReturnType);
        }

        // ══════════════════════════════════════════════════════════════════
        // API 面
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void API面_9个公开方法全部是扩展方法()
        {
            MethodInfo[] methods = typeof(DirectoryEx)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

            Assert.Equal(9, methods.Length);
            Assert.All(methods, m => Assert.True(
                m.IsDefined(typeof(ExtensionAttribute), inherit: false),
                $"{m.Name} 不是扩展方法"));
        }

        [Fact]
        public void DirectoryEx_是静态类()
        {
            Type t = typeof(DirectoryEx);

            Assert.True(t.IsAbstract && t.IsSealed);
            Assert.Empty(t.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        }
    }
}
