using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Extension.IO.Files;
using Extension.IO.Csv;
using Extension.IO.Retention;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// T0 层「CSV 读写」专项：<c>Extension.IO.Retention.CSVEx</c>（355 行，此前零用例覆盖）。
    /// <para>
    /// ⚠️ 这个类**不是死代码** —— 全仓有 **7 处下游调用点**
    /// （`Packing Machine/…/03_初始化通讯.cs` 读 `CRegisterInfo`、`Secs/Secs/Variable/DataBase.cs`
    /// 读写、`Secs/…/VID/VIDBase.cs`、`WPFControl/…/QuerySave.cs`、
    /// `WPFControl/…/AlarmInfoManagerService.cs` ×2），所以下面的缺陷都是**活的风险**。
    /// </para>
    /// <para>
    /// 钉住的 9 处行为（硬规则 1：只记录不顺手改）：
    /// <list type="number">
    /// <item><b>⛔ 读侧把 <c>\</c> 当转义符，写侧却从不产生它</b>
    /// ⇒ 任何含反斜杠的值（Windows 路径！）**往返即损坏**：<c>C:\temp\a.txt</c> 读回来是 <c>C:tempa.txt</c>；
    /// 顺带这个"读侧独有"的转义还能让 <c>a\,b</c> 不被拆成两个字段；</item>
    /// <item><b>⛔ 写侧把 <c>"</c> 双写成 <c>""</c>（符合 RFC 4180），读侧却只做 <c>inQuotes</c> 翻转、不还原</b>
    /// ⇒ 含引号的值往返后引号消失（<c>say "hi"</c> → <c>say hi</c>）；</item>
    /// <item><b>⛔ 每个字段都被 <c>.Trim()</c></b> ⇒ 首尾空格静默丢失，<c>" a "</c> 往返成 <c>"a"</c>；</item>
    /// <item><b>⛔ 集合属性导出的是类型名</b>（<c>value.ToString()</c>）⇒ <c>List&lt;int&gt;</c> 写成
    /// <c>System.Collections.Generic.List`1[System.Int32]</c>，而读回来时 <c>SetPropertyValue</c> 又会把
    /// <c>[1,2]</c> 拆成 <c>List&lt;string&gt;</c> 塞给 <c>List&lt;int&gt;</c> ⇒ 抛异常
    /// —— 读写两侧对集合的处理**完全不配套**；</item>
    /// <item><b>⛔ <c>List&lt;&gt;</c> 属性读侧还要求字段**被引号包裹**</b>：<c>[1,2,3]</c> 不加引号会先被
    /// 逗号拆开，只有 <c>values[0] = "[1"</c> 被用上 ⇒ **静默**变成单元素列表 <c>["1"]</c>；</item>
    /// <item><b>⛔ <c>ReadCsv</c> 把所有异常包成裸 <c>Exception</c></b>（两层包装）⇒ 调用方**无法按类型 catch**
    /// （文件不存在不再是 <c>FileNotFoundException</c>）；</item>
    /// <item><b><c>ExportToCsvAsync_String</c> 的 <c>includeHeader</c> 参数被忽略</b>
    /// —— 内部硬编码 <c>ExportToCsvAsync(wrapped, filePath, false)</c>，且 XML 注释里写的
    /// <c>headerName</c>/<c>delimiter</c>/<c>encoding</c> 三个参数**在签名里根本不存在**；</item>
    /// <item><b>默认编码是 <c>Encoding.UTF8</c>（会写 BOM）</b> ⇒ 导出的 CSV 带 <c>EF BB BF</c>
    /// （同库的 <c>JsonEx.Save</c> 是无 BOM 的 ⇒ **两个类口径不一致**）；</item>
    /// <item><b>空字段被"跳过"而不是"转换失败"</b> ⇒ 属性保持默认值；同时使
    /// <c>SetPropertyValue</c> 里的"可空类型"分支**在 ReadCsv 路径上不可达**。</item>
    /// </list>
    /// </para>
    /// </summary>
    public class CSVExTests
    {
        // ── 测试模型 ──────────────────────────────────────────────────────

        internal sealed class CsvRow
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        internal sealed class CsvNameOnly
        {
            public string Name { get; set; }
        }

        internal sealed class CsvStringListRow
        {
            public List<string> Tags { get; set; }
        }

        internal sealed class CsvIntListRow
        {
            public List<int> Tags { get; set; }
        }

        internal sealed class CsvRichRow
        {
            public int Id { get; set; }
            public DayOfWeek Day { get; set; }
            public int? Optional { get; set; }
        }

        private static List<CsvRow> TwoRows() => new List<CsvRow>
        {
            new CsvRow { Id = 1, Name = "a" },
            new CsvRow { Id = 2, Name = "b" },
        };

        // ══════════════════════════════════════════════════════════════════
        // ExportToCsvAsync —— 正常路径
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task ExportToCsvAsync_基本导出_表头是属性名_数据按声明顺序()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.Combine("out.csv");

            await CSVEx.ExportToCsvAsync(TwoRows(), path);

            string[] lines = File.ReadAllLines(path);
            Assert.Equal(3, lines.Length);
            Assert.Equal("Id,Name", lines[0]);
            Assert.Equal("1,a", lines[1]);
            Assert.Equal("2,b", lines[2]);
        }

        [Fact]
        public async Task ExportToCsvAsync_includeHeader为false_不写表头()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.Combine("out.csv");

            await CSVEx.ExportToCsvAsync(TwoRows(), path, includeHeader: false);

            string[] lines = File.ReadAllLines(path);
            Assert.Equal(2, lines.Length);
            Assert.Equal("1,a", lines[0]);
        }

        [Fact]
        public async Task ExportToCsvAsync_自定义分隔符()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.Combine("out.csv");

            await CSVEx.ExportToCsvAsync(TwoRows(), path, includeHeader: true, delimiter: ';');

            string[] lines = File.ReadAllLines(path);
            Assert.Equal("Id;Name", lines[0]);
            Assert.Equal("1;a", lines[1]);
        }

        [Fact]
        public async Task ExportToCsvAsync_自动创建目标目录()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.Combine("deep", "nested", "out.csv");

            await CSVEx.ExportToCsvAsync(TwoRows(), path);

            Assert.True(File.Exists(path));
        }

        [Fact]
        public async Task ExportToCsvAsync_已存在文件_被覆盖()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("out.csv", "OLD CONTENT");

            await CSVEx.ExportToCsvAsync(TwoRows(), path);

            Assert.DoesNotContain("OLD CONTENT", File.ReadAllText(path));
        }

        [Fact]
        public async Task ExportToCsvAsync_超过BatchSize_2500行全部写出()
        {
            // BatchSize = 1000 ⇒ 2500 行会走 2 次批量 flush + 1 次尾批
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.Combine("big.csv");
            List<CsvRow> rows = Enumerable.Range(1, 2500)
                .Select(i => new CsvRow { Id = i, Name = "n" + i })
                .ToList();

            await CSVEx.ExportToCsvAsync(rows, path);

            string[] lines = File.ReadAllLines(path);
            Assert.Equal(2501, lines.Length);          // 1 表头 + 2500 数据
            Assert.Equal("1,n1", lines[1]);
            Assert.Equal("1000,n1000", lines[1000]);   // 第一次批量边界
            Assert.Equal("1001,n1001", lines[1001]);
            Assert.Equal("2500,n2500", lines[2500]);
        }

        [Fact]
        public async Task ExportToCsvAsync_null属性值_写成空字段()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.Combine("out.csv");

            await CSVEx.ExportToCsvAsync(new List<CsvNameOnly> { new CsvNameOnly { Name = null } }, path);

            string[] lines = File.ReadAllLines(path);
            Assert.Equal("Name", lines[0]);
            Assert.Equal(string.Empty, lines[1]);
        }

        // ══════════════════════════════════════════════════════════════════
        // ExportToCsvAsync —— 转义
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task ExportToCsvAsync_值含分隔符_被引号包裹()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.Combine("out.csv");

            await CSVEx.ExportToCsvAsync(
                new List<CsvNameOnly> { new CsvNameOnly { Name = "a,b" } }, path);

            Assert.Equal("Name", File.ReadAllLines(path)[0]);
            Assert.Equal("\"a,b\"", File.ReadAllLines(path)[1]);
        }

        [Fact]
        public async Task ExportToCsvAsync_值含引号_引号双写()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.Combine("out.csv");

            await CSVEx.ExportToCsvAsync(
                new List<CsvNameOnly> { new CsvNameOnly { Name = "say \"hi\"" } }, path);

            Assert.Equal("\"say \"\"hi\"\"\"", File.ReadAllLines(path)[1]);
        }

        [Fact]
        public async Task ExportToCsvAsync_值含换行_被引号包裹_物理行数多于记录数()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.Combine("out.csv");

            await CSVEx.ExportToCsvAsync(
                new List<CsvNameOnly> { new CsvNameOnly { Name = "a\r\nb" } }, path);

            Assert.Contains("\"a\r\nb\"", File.ReadAllText(path));
            Assert.Equal(3, File.ReadAllLines(path).Length);   // 表头 + 被换行拆开的 2 行
        }

        [Fact]
        public async Task ExportToCsvAsync_值含反斜杠_不加引号不转义()
        {
            // 写侧只对 delimiter / " / CR / LF 加引号，**完全不管反斜杠**
            // ⇒ 与读侧的"反斜杠=转义符"规则不对称（见下面的往返用例）
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.Combine("out.csv");

            await CSVEx.ExportToCsvAsync(
                new List<CsvNameOnly> { new CsvNameOnly { Name = @"C:\temp\a.txt" } }, path);

            Assert.Equal(@"C:\temp\a.txt", File.ReadAllLines(path)[1]);
        }

        // ══════════════════════════════════════════════════════════════════
        // ExportToCsvAsync —— 编码 / 异常
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task ExportToCsvAsync_默认编码会写UTF8_BOM()
        {
            // Encoding.UTF8 的 GetPreamble() = EF BB BF，StreamWriter 首次写入时会写出去。
            // 同库的 JsonEx.Save 是**无 BOM** 的 ⇒ 两个类口径不一致（BOM 会让部分第三方读取器看到 \uFEFF）。
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.Combine("out.csv");

            await CSVEx.ExportToCsvAsync(TwoRows(), path);

            byte[] head = File.ReadAllBytes(path).Take(3).ToArray();
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, head);
        }

        [Fact]
        public async Task ExportToCsvAsync_显式传入无BOM编码_不写BOM()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.Combine("out.csv");

            await CSVEx.ExportToCsvAsync(TwoRows(), path, encoding: new UTF8Encoding(false));

            byte[] head = File.ReadAllBytes(path).Take(3).ToArray();
            Assert.NotEqual(new byte[] { 0xEF, 0xBB, 0xBF }, head);
        }

        [Fact]
        public async Task ExportToCsvAsync_data为null_抛ArgumentNullException()
        {
            using var ws = new TempWorkspace("CSVExTests");

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => CSVEx.ExportToCsvAsync<CsvRow>(null, ws.Combine("out.csv")));
        }

        [Fact]
        public async Task ExportToCsvAsync_路径为空白_抛ArgumentException()
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => CSVEx.ExportToCsvAsync(TwoRows(), "   "));
        }

        [Fact]
        public async Task ExportToCsvAsync_类型没有公共属性_抛InvalidOperationException()
        {
            // int 没有任何公开实例属性 ⇒ 走 "没有可导出的公共属性" 分支
            using var ws = new TempWorkspace("CSVExTests");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CSVEx.ExportToCsvAsync(new[] { 1, 2 }, ws.Combine("out.csv")));
        }

        [Fact]
        public async Task ExportToCsvAsync_集合属性_导出的是类型名而不是元素()
        {
            // ⛔ 缺陷：WriteDataLineToBuilder 用 value?.ToString()
            // ⇒ List<int> 的 ToString() 是类型名，导出结果**不可往返**
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.Combine("out.csv");

            await CSVEx.ExportToCsvAsync(
                new List<CsvIntListRow> { new CsvIntListRow { Tags = new List<int> { 1, 2 } } }, path);

            string line = File.ReadAllLines(path)[1];
            Assert.Contains("System.Collections.Generic.List", line);
            Assert.Contains("Int32", line);
            Assert.DoesNotContain("1,2", line);
        }

        // ══════════════════════════════════════════════════════════════════
        // ReadCsv —— 正常路径
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void ReadCsv_基本读取_表头按属性名匹配()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", "Id,Name\n1,a\n2,b\n");

            List<CsvRow> rows = CSVEx.ReadCsv<CsvRow>(path);

            Assert.Equal(2, rows.Count);
            Assert.Equal(1, rows[0].Id);
            Assert.Equal("a", rows[0].Name);
            Assert.Equal(2, rows[1].Id);
        }

        [Fact]
        public void ReadCsv_表头大小写不敏感()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", "iD,nAmE\n7,zed\n");

            List<CsvRow> rows = CSVEx.ReadCsv<CsvRow>(path);

            Assert.Equal(7, Assert.Single(rows).Id);
            Assert.Equal("zed", rows[0].Name);
        }

        [Fact]
        public void ReadCsv_表头列顺序与属性顺序无关()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", "Name,Id\na,1\n");

            List<CsvRow> rows = CSVEx.ReadCsv<CsvRow>(path);

            Assert.Equal(1, rows[0].Id);
            Assert.Equal("a", rows[0].Name);
        }

        [Fact]
        public void ReadCsv_未匹配的表头列_被忽略_属性保持默认值()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", "Unknown,Id\nzzz,5\n");

            List<CsvRow> rows = CSVEx.ReadCsv<CsvRow>(path);

            Assert.Equal(5, rows[0].Id);
            Assert.Null(rows[0].Name);
        }

        [Fact]
        public void ReadCsv_hasHeaderRow为false_用属性名当表头按顺序匹配()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", "1,a\n2,b\n");

            List<CsvRow> rows = CSVEx.ReadCsv<CsvRow>(path, hasHeaderRow: false);

            Assert.Equal(2, rows.Count);
            Assert.Equal(1, rows[0].Id);
            Assert.Equal("a", rows[0].Name);
        }

        [Fact]
        public void ReadCsv_空文件_返回空列表()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", string.Empty);

            Assert.Empty(CSVEx.ReadCsv<CsvRow>(path));
        }

        [Fact]
        public void ReadCsv_只有表头_返回空列表()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", "Id,Name\n");

            Assert.Empty(CSVEx.ReadCsv<CsvRow>(path));
        }

        [Fact]
        public void ReadCsv_空白行被跳过_物理行数不等于记录数()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", "Id,Name\n1,a\n\n2,b\n   \n");

            List<CsvRow> rows = CSVEx.ReadCsv<CsvRow>(path);

            Assert.Equal(2, rows.Count);
        }

        [Fact]
        public void ReadCsv_自定义分隔符()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", "Id;Name\n3;c\n");

            List<CsvRow> rows = CSVEx.ReadCsv<CsvRow>(path, delimiter: ';');

            Assert.Equal(3, Assert.Single(rows).Id);
            Assert.Equal("c", rows[0].Name);
        }

        [Fact]
        public void ReadCsv_双写引号不被还原_引号被吞掉()
        {
            // ⛔ 缺陷：ParseCsvLine 只对 " 做 inQuotes 翻转，**不把 "" 还原成一个 "**
            // ⇒ 写侧产出 "say ""hi"""（符合 RFC 4180），读回来是 say hi —— 两个引号都消失。
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", "Name\n\"say \"\"hi\"\"\"\n");

            Assert.Equal("say hi", CSVEx.ReadCsv<CsvNameOnly>(path)[0].Name);
        }

        [Fact]
        public async Task 往返_含双引号的值_引号在回读时丢失()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.Combine("rt.csv");
            const string original = "say \"hi\"";

            await CSVEx.ExportToCsvAsync(
                new List<CsvNameOnly> { new CsvNameOnly { Name = original } }, path);
            List<CsvNameOnly> back = CSVEx.ReadCsv<CsvNameOnly>(path);

            Assert.Equal("\"say \"\"hi\"\"\"", File.ReadAllLines(path)[1]);   // 写侧符合 RFC 4180
            Assert.Equal("say hi", back[0].Name);                             // 读侧不还原
        }

        [Fact]
        public void ReadCsv_带引号且内嵌分隔符的字段_不被拆开()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", "Id,Name\n1,\"a,b\"\n");

            List<CsvRow> rows = CSVEx.ReadCsv<CsvRow>(path);

            Assert.Equal("a,b", rows[0].Name);
        }

        [Fact]
        public void ReadCsv_空字段_属性保持默认值而不是转换失败()
        {
            // 调用方用 !string.IsNullOrWhiteSpace(values[j]) 把空值挡在外面
            // ⇒ SetPropertyValue 里那段"可空类型 → SetValue(obj, null)"分支在 ReadCsv 路径上不可达
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", "Id,Name\n,a\n");

            List<CsvRow> rows = CSVEx.ReadCsv<CsvRow>(path);

            Assert.Equal(0, rows[0].Id);          // 默认值，不是异常
            Assert.Equal("a", rows[0].Name);
        }

        [Fact]
        public void ReadCsv_枚举属性_大小写不敏感解析()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", "Id,Day\n1,monday\n");

            Assert.Equal(DayOfWeek.Monday, CSVEx.ReadCsv<CsvRichRow>(path)[0].Day);
        }

        [Fact]
        public void ReadCsv_ListString属性_带引号的方括号列表_被拆成多个元素()
        {
            // 必须写成带引号的 "[1,2,3]" —— 引号让方括号里的逗号不被当作字段分隔符
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", "Tags\n\"[1,2,3]\"\n");

            List<CsvStringListRow> rows = CSVEx.ReadCsv<CsvStringListRow>(path);

            Assert.Equal(new List<string> { "1", "2", "3" }, rows[0].Tags);
        }

        [Fact]
        public void ReadCsv_ListString属性_不加引号的方括号列表_只取到第一个元素()
        {
            // ⛔ 陷阱：不加引号时 ParseCsvLine 先按逗号切开 ⇒ 只有 values[0] = "[1" 被用上
            // ⇒ 静默变成单元素列表，不是"读失败"
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", "Tags\n[1,2,3]\n");

            List<CsvStringListRow> rows = CSVEx.ReadCsv<CsvStringListRow>(path);

            Assert.Equal(new List<string> { "1" }, rows[0].Tags);
        }

        // ══════════════════════════════════════════════════════════════════
        // ReadCsv —— 缺陷 / 陷阱
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void ReadCsv_反斜杠被当成转义符吞掉()
        {
            // ⛔ 缺陷：ParseCsvLine 把 '\' 当转义前缀，但 EscapeCsvValue 从不产生它。
            // "C:\temp\a.txt" → 两个反斜杠各吃掉自己，得到 "C:tempa.txt"
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", "Name\nC:\\temp\\a.txt\n");

            Assert.Equal("C:tempa.txt", CSVEx.ReadCsv<CsvNameOnly>(path)[0].Name);
        }

        [Fact]
        public void ReadCsv_反斜杠还能转义分隔符_使字段不被拆开()
        {
            // 读侧独有的"反斜杠转义"：a\,b 被当成**一个**字段 "a,b"
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", "Id,Name\n1,a\\,b\n");

            List<CsvRow> rows = CSVEx.ReadCsv<CsvRow>(path);

            Assert.Equal("a,b", Assert.Single(rows).Name);
        }

        [Fact]
        public async Task 往返_含反斜杠的值_导出后再读回会损坏()
        {
            // ⛔ 这是 CSVEx 最危险的一条：读写两侧对 '\' 的规则**不对称**
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.Combine("rt.csv");
            const string original = @"D:\Data\Images\a.bmp";

            await CSVEx.ExportToCsvAsync(
                new List<CsvNameOnly> { new CsvNameOnly { Name = original } }, path);
            List<CsvNameOnly> back = CSVEx.ReadCsv<CsvNameOnly>(path);

            Assert.Equal(original, File.ReadAllLines(path)[1]);          // 写出来是对的
            Assert.NotEqual(original, back[0].Name);                     // 读回来不对
            Assert.Equal("D:DataImagesa.bmp", back[0].Name);
        }

        [Fact]
        public void ReadCsv_字段首尾空格被Trim掉()
        {
            // ⛔ 缺陷：ParseCsvLine 对每个字段 .Trim() ⇒ 首尾空格静默丢失
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", "Name\n  a  \n");

            Assert.Equal("a", CSVEx.ReadCsv<CsvNameOnly>(path)[0].Name);
        }

        [Fact]
        public async Task 往返_首尾带空格的值_导出后再读回会丢空格()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.Combine("rt.csv");

            await CSVEx.ExportToCsvAsync(
                new List<CsvNameOnly> { new CsvNameOnly { Name = "  a  " } }, path);
            List<CsvNameOnly> back = CSVEx.ReadCsv<CsvNameOnly>(path);

            Assert.Equal("  a  ", File.ReadAllLines(path)[1]);   // 写出来带空格（也没加引号）
            Assert.Equal("a", back[0].Name);                     // 读回来没了
        }

        [Fact]
        public void ReadCsv_ListInt属性_抛异常_因为塞进去的是ListString()
        {
            // ⛔ 缺陷：SetPropertyValue 对 List<> 一律 Split 成 List<string>，
            // 再 SetValue 给 List<int> ⇒ ArgumentException
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", "Tags\n[1,2]\n");

            Exception ex = Record.Exception(() => CSVEx.ReadCsv<CsvIntListRow>(path));

            Assert.NotNull(ex);
            Assert.Contains("处理第 2 行时出错", ex.Message);
        }

        [Fact]
        public void ReadCsv_文件不存在_抛裸Exception而不是FileNotFoundException()
        {
            // ⛔ 缺陷：ReadCsv 用 catch (Exception) 统一包装 ⇒ 调用方**无法按类型 catch**
            using var ws = new TempWorkspace("CSVExTests");

            Exception ex = Record.Exception(() => CSVEx.ReadCsv<CsvRow>(ws.Combine("missing.csv")));

            Assert.NotNull(ex);
            Assert.IsType<Exception>(ex);                                  // 精确是 Exception
            Assert.Contains("读取CSV文件失败", ex.Message);
            Assert.IsAssignableFrom<FileNotFoundException>(ex.InnerException);
        }

        [Fact]
        public void ReadCsv_单元格类型转换失败_异常被两层包装()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", "Id,Name\nnot-a-number,a\n");

            Exception ex = Record.Exception(() => CSVEx.ReadCsv<CsvRow>(path));

            Assert.NotNull(ex);
            Assert.IsType<Exception>(ex);
            Assert.Contains("读取CSV文件失败", ex.Message);
            Assert.Contains("处理第 2 行时出错", ex.Message);              // 行号是 1-based
        }

        [Fact]
        public void ReadCsv_类型没有公共属性_不抛异常但得到空对象()
        {
            // 与 ExportToCsvAsync 相反（那边抛 InvalidOperationException）
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.WriteFile("in.csv", "A,B\n1,2\n");

            List<NoPropertyRow> rows = CSVEx.ReadCsv<NoPropertyRow>(path);

            Assert.Single(rows);
        }

        internal sealed class NoPropertyRow
        {
        }

        // ══════════════════════════════════════════════════════════════════
        // ExportToCsvAsync_String
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task ExportToCsvAsync_String_写出每个字符串一行()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.Combine("s.csv");

            await CSVEx.ExportToCsvAsync_String(new List<string> { "a", "b" }, path);

            string[] lines = File.ReadAllLines(path);
            Assert.Equal(2, lines.Length);
            Assert.Equal("a", lines[0]);
            Assert.Equal("b", lines[1]);
        }

        [Fact]
        public async Task ExportToCsvAsync_String_includeHeader参数被忽略()
        {
            // ⛔ 缺陷：内部硬编码 ExportToCsvAsync(wrappedData, filePath, **false**)
            // ⇒ 传 includeHeader: true 也拿不到表头（本应写 "Value"）
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.Combine("s.csv");

            await CSVEx.ExportToCsvAsync_String(new List<string> { "a", "b" }, path, includeHeader: true);

            string[] lines = File.ReadAllLines(path);
            Assert.Equal(2, lines.Length);
            Assert.DoesNotContain("Value", lines);
            Assert.Equal("a", lines[0]);
        }

        [Fact]
        public async Task ExportToCsvAsync_String_null元素_写成空行()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.Combine("s.csv");

            await CSVEx.ExportToCsvAsync_String(new List<string> { "a", null }, path);

            string[] lines = File.ReadAllLines(path);
            Assert.Equal(2, lines.Length);
            Assert.Equal(string.Empty, lines[1]);
        }

        [Fact]
        public async Task ExportToCsvAsync_String_含逗号的元素_被引号包裹()
        {
            using var ws = new TempWorkspace("CSVExTests");
            string path = ws.Combine("s.csv");

            await CSVEx.ExportToCsvAsync_String(new List<string> { "a,b" }, path);

            Assert.Equal("\"a,b\"", File.ReadAllLines(path)[0]);
        }

        [Fact]
        public async Task ExportToCsvAsync_String_data为null_抛ArgumentNullException()
        {
            using var ws = new TempWorkspace("CSVExTests");

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => CSVEx.ExportToCsvAsync_String(null, ws.Combine("s.csv")));
        }

        // ══════════════════════════════════════════════════════════════════
        // API 面
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void API面_ExportToCsvAsync_String的签名与XML注释不符()
        {
            // XML 注释里写了 headerName / delimiter / encoding 三个参数，签名里**都没有**；
            // 实际只有 (data, filePath, includeHeader)，且 includeHeader 还是无效的。
            MethodInfo m = typeof(CSVEx).GetMethod(
                "ExportToCsvAsync_String", BindingFlags.Public | BindingFlags.Static);

            Assert.NotNull(m);
            Assert.Equal(3, m.GetParameters().Length);
            Assert.Equal(
                new[] { typeof(List<string>), typeof(string), typeof(bool) },
                m.GetParameters().Select(p => p.ParameterType).ToArray());
        }

        [Fact]
        public void API面_三个公开方法_ReadCsv不是扩展方法()
        {
            MethodInfo[] methods = typeof(CSVEx)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

            Assert.Equal(3, methods.Length);
            Assert.All(methods, m => Assert.False(
                m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), inherit: false),
                $"{m.Name} 不应是扩展方法"));
        }

        [Fact]
        public void CSVEx_是静态类()
        {
            Type t = typeof(CSVEx);

            Assert.True(t.IsAbstract && t.IsSealed);
            Assert.Equal("Extension.IO.Csv", t.Namespace);
        }
    }
}
