using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Extension.Core.Conversion;
using Xunit;

namespace Extension.Tests
{
    /// <summary>往返用样例模型（含中文、集合、数值、时间）</summary>
    internal sealed class JsonSampleModel
    {
        public int IntProp { get; set; }
        public double DoubleProp { get; set; }
        public decimal DecimalProp { get; set; }
        public bool BoolProp { get; set; }
        public string TextProp { get; set; }
        public List<int> Numbers { get; set; }
        public DateTime DateProp { get; set; }
    }

    /// <summary>静态类读写用样例（<c>ToJson(this Type)</c> / <c>SaveStaticClass</c> / <c>LoadStaticClass</c>）</summary>
    internal static class JsonStaticSample
    {
        public static int A { get; set; }
        public static string B { get; set; }
        public static bool C { get; set; }
    }

    /// <summary>
    /// T0 层「JSON」专项：<c>Extension.Core.Conversion.JsonEx</c>（14 个公开静态方法）。
    /// 这是全仓配置读写与报文转换的底座，此前零用例覆盖，同时也是
    /// **可空性警告最集中**的一块（52 条 CS86xx，未修，只记录）。
    /// <para>
    /// 本文件重点钉住 6 组语义：
    /// <list type="number">
    /// <item><c>DefaultOptions</c> 的三个副作用：数字被写成 JSON **字符串**（<c>WriteAsString</c>）、
    /// 中文不转义（<c>UnsafeRelaxedJsonEscaping</c>）、带缩进；</item>
    /// <item><c>ToJson(this Type)</c> 与泛型 <c>ToJson&lt;T&gt;</c> 的**重载选择**：传 <c>typeof(X)</c>
    /// 走的是「静态属性字典」，不是序列化 Type 对象；</item>
    /// <item><c>Read</c>（坏文件**抛异常**）与 <c>TryRead</c>（坏文件**回退 default**）的契约差异，
    /// 以及 <c>TryRead</c> 只吞 <c>IsRecoverableReadFailure</c> 名单内异常；</item>
    /// <item><c>ToList</c> 的**扁平化**语义：嵌套对象的父键会丢、标量数组的键等于值；</item>
    /// <item><c>Replace</c> 只按**值的文本形式**匹配（键不可替换、数字/布尔按文本匹配），
    /// 且**同一个替换节点命中多处时会抛 <c>InvalidOperationException</c>**（缺陷，未修）；</item>
    /// <item><c>Save</c> 不带 BOM 而 <c>SaveStaticClass</c> 带 UTF-8 BOM —— 两者写出物不一致。</item>
    /// </list>
    /// 不确定异常具体类型时一律用 <c>Record.Exception</c> + <c>IsAssignableFrom</c>，
    /// 因为 <c>Assert.Throws&lt;T&gt;</c> 只认**精确**类型。
    /// </para>
    /// </summary>
    public class JsonExTests
    {
        // ══════════════════════════════════════════════════════════════════
        // 临时目录工具
        // ══════════════════════════════════════════════════════════════════

        private static string NewTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "JsonExTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void Cleanup(string dir)
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 临时目录清理失败不该让用例变红
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // ToJson：DefaultOptions 的三个副作用
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void ToJson_数字被写成JSON字符串_是NumberHandling_WriteAsString的副作用()
        {
            // 这是 DefaultOptions 最容易踩的一条：所有数字（含根值）都会带引号
            Assert.Equal("\"123\"", JsonEx.ToJson(123));
            Assert.Equal("\"0\"", JsonEx.ToJson(0));
            Assert.Equal("\"1.5\"", JsonEx.ToJson(1.5));
        }

        [Fact]
        public void ToJson_属性级数字同样带引号_但读回时因AllowReadingFromString仍能还原()
        {
            string json = JsonEx.ToJson(new JsonSampleModel { IntProp = 7 });

            Assert.Contains("\"IntProp\"", json);
            Assert.Contains("\"7\"", json);

            // 读回来仍是 int 7（AllowReadingFromString 兜住了）
            Assert.Equal(7, json.FromJson<JsonSampleModel>().IntProp);
        }

        [Fact]
        public void ToJson_中文不被转义成Unicode转义序列()
        {
            string json = JsonEx.ToJson(new JsonSampleModel { TextProp = "中文配置" });

            Assert.Contains("中文配置", json);
            Assert.DoesNotContain("\\u", json);
        }

        [Fact]
        public void ToJson_带缩进换行()
        {
            Assert.Contains("\n", JsonEx.ToJson(new JsonSampleModel { IntProp = 1 }));
        }

        [Fact]
        public void ToJson_引用类型为null时返回null而不是字符串null()
        {
            Assert.Null(JsonEx.ToJson<JsonSampleModel>(null));
            Assert.Null(JsonEx.ToJson((object)null));
            Assert.Null(JsonEx.ToJson<string>(null));
        }

        [Fact]
        public void ToJson_值类型默认值不会被当成null()
        {
            // pData == null 对值类型恒为 false ⇒ 0 会正常序列化
            Assert.Equal("\"0\"", JsonEx.ToJson(0));
            // ⚠️ WriteAsString 只作用于**数字**：布尔仍是不带引号的字面量，且是小写
            Assert.Equal("false", JsonEx.ToJson(false));
            Assert.Equal("true", JsonEx.ToJson(true));
        }

        [Fact]
        public void ToJson_与FromJson往返_含中文集合与时间()
        {
            var m = new JsonSampleModel
            {
                IntProp = 7,
                DoubleProp = 1.5,
                DecimalProp = 2.5m,
                BoolProp = true,
                TextProp = "中文",
                Numbers = new List<int> { 1, 2, 3 },
                DateProp = new DateTime(2026, 9, 18, 8, 16, 0)
            };

            string json = JsonEx.ToJson(m);
            JsonSampleModel back = json.FromJson<JsonSampleModel>();

            // 序列化是确定性的 ⇒ 往返后重新序列化应与原文完全相同
            Assert.Equal(json, JsonEx.ToJson(back));
            Assert.Equal(new List<int> { 1, 2, 3 }, back.Numbers);
            Assert.Equal(new DateTime(2026, 9, 18, 8, 16, 0), back.DateProp);
        }

        // ══════════════════════════════════════════════════════════════════
        // FromJson
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void FromJson_空白或null返回default()
        {
            Assert.Null(((string)null).FromJson<JsonSampleModel>());
            Assert.Null("".FromJson<JsonSampleModel>());
            Assert.Null("   ".FromJson<string>());
            Assert.Equal(0, "   ".FromJson<int>());
        }

        [Fact]
        public void FromJson_JSON字面量null_引用类型得到null_值类型会抛异常()
        {
            Assert.Null("null".FromJson<JsonSampleModel>());
            // ⚠️ 值类型不对称：把 null 读进非可空值类型会抛，而不是静默给 0
            var ex = Record.Exception(() => "null".FromJson<int>());
            Assert.IsAssignableFrom<JsonException>(ex);
        }

        [Fact]
        public void FromJson_数字既可从数字也可从带引号字符串读入()
        {
            Assert.Equal(123, "123".FromJson<int>());
            Assert.Equal(123, "\"123\"".FromJson<int>());
        }

        [Fact]
        public void FromJson_非法JSON会抛JsonException_没有兜底()
        {
            // ⚠️ FromJson 不做 try/catch ⇒ 坏内容直接抛。要"不抛"必须用 Read+TryRead 组合
            var ex = Record.Exception(() => "{oops".FromJson<JsonSampleModel>());

            Assert.IsAssignableFrom<JsonException>(ex);
        }

        // ══════════════════════════════════════════════════════════════════
        // Save / Read / TryRead（需要 IO）
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void Save与Read往返_含中文()
        {
            string dir = NewTempDir();
            try
            {
                string path = Path.Combine(dir, "cfg.json");
                var m = new JsonSampleModel { IntProp = 7, TextProp = "配置", BoolProp = true };

                m.Save(path);

                Assert.True(File.Exists(path));
                JsonSampleModel back = JsonEx.Read<JsonSampleModel>(path);
                Assert.Equal(7, back.IntProp);
                Assert.Equal("配置", back.TextProp);
                Assert.True(back.BoolProp);
            }
            finally
            {
                Cleanup(dir);
            }
        }

        [Fact]
        public void Save会补建缺失的多级目录()
        {
            string dir = NewTempDir();
            try
            {
                string path = Path.Combine(dir, "sub", "deep", "cfg.json");
                new JsonSampleModel { IntProp = 1 }.Save(path);

                Assert.True(File.Exists(path));
            }
            finally
            {
                Cleanup(dir);
            }
        }

        [Fact]
        public void Save三参重载_把目录与文件名拼接()
        {
            string dir = NewTempDir();
            try
            {
                new JsonSampleModel { IntProp = 2 }.Save(dir, "named.json");

                string path = Path.Combine(dir, "named.json");
                Assert.True(File.Exists(path));
                Assert.Equal(2, JsonEx.Read<JsonSampleModel>(path).IntProp);
            }
            finally
            {
                Cleanup(dir);
            }
        }

        [Fact]
        public void Save写出的文件不带BOM()
        {
            string dir = NewTempDir();
            try
            {
                string path = Path.Combine(dir, "nobom.json");
                new JsonSampleModel { IntProp = 1 }.Save(path);

                byte[] head = File.ReadAllBytes(path).Take(3).ToArray();
                Assert.Equal((byte)'{', head[0]);   // 直接以 '{' 开头，没有 EF BB BF
            }
            finally
            {
                Cleanup(dir);
            }
        }

        [Fact]
        public void Save_对象为null时写出空文件_读回得到default()
        {
            string dir = NewTempDir();
            try
            {
                string path = Path.Combine(dir, "null.json");
                JsonEx.Save((object)null, path);

                Assert.True(File.Exists(path));
                Assert.Equal(string.Empty, File.ReadAllText(path));
                Assert.Null(JsonEx.Read<JsonSampleModel>(path));
            }
            finally
            {
                Cleanup(dir);
            }
        }

        [Fact]
        public void Read_文件不存在返回default_不抛异常()
        {
            string dir = NewTempDir();
            try
            {
                string missing = Path.Combine(dir, "nope.json");

                Assert.Null(JsonEx.Read<JsonSampleModel>(missing));
                Assert.Equal(0, JsonEx.Read<int>(missing));
                // 两参重载同样不抛
                Assert.Null(JsonEx.Read<JsonSampleModel>(dir, "nope.json"));
            }
            finally
            {
                Cleanup(dir);
            }
        }

        [Fact]
        public void Read_文件内容损坏会抛异常_这是与TryRead的分界线()
        {
            string dir = NewTempDir();
            try
            {
                string path = Path.Combine(dir, "broken.json");
                File.WriteAllText(path, "{ this is not json");

                var ex = Record.Exception(() => JsonEx.Read<JsonSampleModel>(path));
                Assert.IsAssignableFrom<JsonException>(ex);
            }
            finally
            {
                Cleanup(dir);
            }
        }

        [Fact]
        public void TryRead_文件不存在返回default()
        {
            string dir = NewTempDir();
            try
            {
                string missing = Path.Combine(dir, "nope.json");

                Assert.Null(JsonEx.TryRead<JsonSampleModel>(missing));
                Assert.Equal(0, JsonEx.TryRead<int>(missing));
                Assert.Null(JsonEx.TryRead<JsonSampleModel>(dir, "nope.json"));
            }
            finally
            {
                Cleanup(dir);
            }
        }

        [Fact]
        public void TryRead_文件内容损坏也回退default_不抛()
        {
            string dir = NewTempDir();
            try
            {
                string path = Path.Combine(dir, "broken.json");
                File.WriteAllText(path, "{ this is not json");

                Assert.Null(JsonEx.TryRead<JsonSampleModel>(path));
            }
            finally
            {
                Cleanup(dir);
            }
        }

        [Fact]
        public void TryRead_正常文件与Read结果一致()
        {
            string dir = NewTempDir();
            try
            {
                string path = Path.Combine(dir, "ok.json");
                new JsonSampleModel { IntProp = 9, TextProp = "ok" }.Save(path);

                JsonSampleModel viaRead = JsonEx.Read<JsonSampleModel>(path);
                JsonSampleModel viaTry = JsonEx.TryRead<JsonSampleModel>(path);

                Assert.Equal(viaRead.IntProp, viaTry.IntProp);
                Assert.Equal("ok", viaTry.TextProp);
            }
            finally
            {
                Cleanup(dir);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // ToJson(this Type) / SaveStaticClass / LoadStaticClass
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void ToJson_传Type时走的是静态属性字典_不是序列化Type对象()
        {
            // 重载决议：非泛型 ToJson(Type) 优于泛型 ToJson<Type>(Type)
            JsonStaticSample.A = 11;
            JsonStaticSample.B = "bee";
            JsonStaticSample.C = true;

            string json = JsonEx.ToJson(typeof(JsonStaticSample));
            var node = json.ToJsonNode() as JsonObject;

            Assert.NotNull(node);
            Assert.Equal(3, node.Count);                       // 只有 3 个静态属性
            Assert.True(node.ContainsKey("A"));                // 键名即属性名
            Assert.True(node.ContainsKey("B"));
            Assert.True(node.ContainsKey("C"));
            // 若被当成 Type 对象序列化，会出现 Assembly / DeclaringType 之类成员
            Assert.False(node.ContainsKey("Assembly"));
        }

        [Fact]
        public void SaveStaticClass与LoadStaticClass往返()
        {
            string dir = NewTempDir();
            try
            {
                string path = Path.Combine(dir, "statics.json");
                JsonStaticSample.A = 3;
                JsonStaticSample.B = "x";
                JsonStaticSample.C = false;

                JsonEx.SaveStaticClass(typeof(JsonStaticSample), path);

                // 全部改成别的值，确认确实是从文件读回来的
                JsonStaticSample.A = 0;
                JsonStaticSample.B = "changed";
                JsonStaticSample.C = true;

                JsonEx.LoadStaticClass(typeof(JsonStaticSample), path);

                Assert.Equal(3, JsonStaticSample.A);
                Assert.Equal("x", JsonStaticSample.B);
                Assert.False(JsonStaticSample.C);
            }
            finally
            {
                Cleanup(dir);
            }
        }

        [Fact]
        public void SaveStaticClass写出的文件带UTF8_BOM_与Save不一致()
        {
            string dir = NewTempDir();
            try
            {
                string path = Path.Combine(dir, "statics.json");
                JsonEx.SaveStaticClass(typeof(JsonStaticSample), path);

                byte[] head = File.ReadAllBytes(path).Take(3).ToArray();
                // SaveStaticClass 显式传 Encoding.UTF8（带 BOM），Save 走 File.WriteAllText 默认（无 BOM）
                Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, head);
            }
            finally
            {
                Cleanup(dir);
            }
        }

        [Fact]
        public void LoadStaticClass_文件不存在时静默返回()
        {
            string dir = NewTempDir();
            try
            {
                JsonStaticSample.A = 42;
                JsonEx.LoadStaticClass(typeof(JsonStaticSample), Path.Combine(dir, "nope.json"));

                Assert.Equal(42, JsonStaticSample.A);
            }
            finally
            {
                Cleanup(dir);
            }
        }

        [Fact]
        public void LoadStaticClass_根不是JSON对象时静默返回()
        {
            string dir = NewTempDir();
            try
            {
                string path = Path.Combine(dir, "array.json");
                File.WriteAllText(path, "[1,2,3]");

                JsonStaticSample.A = 42;
                JsonEx.LoadStaticClass(typeof(JsonStaticSample), path);

                Assert.Equal(42, JsonStaticSample.A);
            }
            finally
            {
                Cleanup(dir);
            }
        }

        [Fact]
        public void LoadStaticClass_单个属性转换失败时保持原值_其余属性继续加载()
        {
            string dir = NewTempDir();
            try
            {
                string path = Path.Combine(dir, "partial.json");
                File.WriteAllText(path, "{\"A\":\"not-a-number\",\"B\":\"loaded\"}");

                JsonStaticSample.A = 42;
                JsonStaticSample.B = "old";

                JsonEx.LoadStaticClass(typeof(JsonStaticSample), path);

                // A 的转换抛 FormatException，属 IsRecoverableReadFailure ⇒ 被吞、保持原值
                Assert.Equal(42, JsonStaticSample.A);
                // 局部失败不拖垮整体
                Assert.Equal("loaded", JsonStaticSample.B);
            }
            finally
            {
                Cleanup(dir);
            }
        }

        [Fact]
        public void LoadStaticClass_JSON里多余的键被忽略()
        {
            string dir = NewTempDir();
            try
            {
                string path = Path.Combine(dir, "extra.json");
                File.WriteAllText(path, "{\"A\":1,\"NotExists\":999}");

                JsonEx.LoadStaticClass(typeof(JsonStaticSample), path);

                Assert.Equal(1, JsonStaticSample.A);
            }
            finally
            {
                Cleanup(dir);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // ToJsonNode
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void ToJsonNode_null或空白返回null()
        {
            Assert.Null(((string)null).ToJsonNode());
            Assert.Null("".ToJsonNode());
            Assert.Null("   ".ToJsonNode());
        }

        [Fact]
        public void ToJsonNode_对象与数组分别得到JsonObject与JsonArray()
        {
            Assert.IsType<JsonObject>("{\"a\":1}".ToJsonNode());
            Assert.IsType<JsonArray>("[1,2]".ToJsonNode());
        }

        [Fact]
        public void ToJsonNode_非法JSON会抛JsonException()
        {
            var ex = Record.Exception(() => "{bad".ToJsonNode());

            Assert.IsAssignableFrom<JsonException>(ex);
        }

        // ══════════════════════════════════════════════════════════════════
        // ToList：扁平化语义
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void ToList_对象_键是属性名_值是ToString的文本()
        {
            List<KeyValuePair<string, string>> list =
                "{\"a\":\"x\",\"b\":1,\"c\":true}".ToJsonNode().ToList();

            Assert.Equal(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("a", "x"),
                new KeyValuePair<string, string>("b", "1"),
                new KeyValuePair<string, string>("c", "true"),
            }, list);
        }

        [Fact]
        public void ToList_嵌套对象_父键会被丢弃()
        {
            // ⚠️ 扁平化不带路径：外层 "outer" 这个键在结果里消失了
            List<KeyValuePair<string, string>> list =
                "{\"outer\":{\"inner\":\"x\"}}".ToJsonNode().ToList();

            Assert.Equal(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("inner", "x"),
            }, list);
        }

        [Fact]
        public void ToList_标量数组_键与值相同()
        {
            List<KeyValuePair<string, string>> list = "[\"x\",\"y\"]".ToJsonNode().ToList();

            Assert.Equal(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("x", "x"),
                new KeyValuePair<string, string>("y", "y"),
            }, list);
        }

        [Fact]
        public void ToList_对象数组_展开并丢弃父键()
        {
            List<KeyValuePair<string, string>> list =
                "[{\"k\":\"v\"},{\"k2\":\"v2\"}]".ToJsonNode().ToList();

            Assert.Equal(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("k", "v"),
                new KeyValuePair<string, string>("k2", "v2"),
            }, list);
        }

        [Fact]
        public void ToList_null返回空列表_不抛异常()
        {
            Assert.Empty(JsonEx.ToList(null));
        }

        [Fact]
        public void ToList_根是标量时返回空列表()
        {
            // 既不是 JsonObject 也不是 JsonArray ⇒ 直接空
            Assert.Empty("123".ToJsonNode().ToList());
            Assert.Empty("true".ToJsonNode().ToList());
            Assert.Empty("\"abc\"".ToJsonNode().ToList());
        }

        // ══════════════════════════════════════════════════════════════════
        // Replace：按值文本替换
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void Replace_对象里按值文本替换_并返回同一个节点实例()
        {
            JsonNode node = "{\"a\":\"x\",\"b\":\"y\"}".ToJsonNode();

            JsonNode ret = node.Replace("x", JsonValue.Create(123));

            Assert.Same(node, ret);                                    // 原地修改
            Assert.Equal("{\"a\":123,\"b\":\"y\"}", node.ToJsonString());
        }

        [Fact]
        public void Replace_数字与布尔值按文本形式匹配()
        {
            JsonNode node = "{\"a\":1,\"b\":true}".ToJsonNode();

            node.Replace("1", JsonValue.Create("one"));
            node.Replace("true", JsonValue.Create(false));

            Assert.Equal("{\"a\":\"one\",\"b\":false}", node.ToJsonString());
        }

        [Fact]
        public void Replace_数组里替换_仅一处命中()
        {
            JsonNode node = "[\"x\",\"y\",\"z\"]".ToJsonNode();

            node.Replace("x", JsonValue.Create("X"));

            Assert.Equal("[\"X\",\"y\",\"z\"]", node.ToJsonString());
        }

        [Fact]
        public void Replace_递归替换嵌套对象与数组()
        {
            JsonNode node = "{\"o\":{\"k\":\"x\"},\"arr\":[{\"k2\":\"y\"}]}".ToJsonNode();

            node.Replace("x", JsonValue.Create("X"));
            node.Replace("y", JsonValue.Create("Y"));

            Assert.Equal("{\"o\":{\"k\":\"X\"},\"arr\":[{\"k2\":\"Y\"}]}", node.ToJsonString());
        }

        [Fact]
        public void Replace_同一个替换节点命中多处时会抛InvalidOperationException_是缺陷()
        {
            // ⚠️ 缺陷（未修，只记录）：Replace 把同一个 replacement 实例赋给多个位置，
            //    而 JsonNode 不允许「一个节点挂在两个父节点下」⇒ 第二处赋值时抛 InvalidOperationException。
            //    调用方若确实要批量替换，必须为每处传独立节点（但 API 只接受一个节点，做不到）。
            JsonNode node = "[\"x\",\"x\"]".ToJsonNode();

            var ex = Record.Exception(() => node.Replace("x", JsonValue.Create("X")));

            Assert.IsAssignableFrom<InvalidOperationException>(ex);
        }

        [Fact]
        public void Replace_键不会被替换_只比较值()
        {
            JsonNode node = "{\"x\":\"v\"}".ToJsonNode();

            node.Replace("x", JsonValue.Create("X"));

            Assert.Equal("{\"x\":\"v\"}", node.ToJsonString());   // 键 "x" 原样保留
        }

        [Fact]
        public void Replace_无命中时内容不变()
        {
            JsonNode node = "{\"a\":\"x\"}".ToJsonNode();

            node.Replace("nothing-matches", JsonValue.Create(1));

            Assert.Equal("{\"a\":\"x\"}", node.ToJsonString());
        }

        [Fact]
        public void Replace_根是标量时不生效()
        {
            JsonNode node = "123".ToJsonNode();

            JsonNode ret = node.Replace("123", JsonValue.Create(456));

            Assert.Same(node, ret);
            Assert.Equal("123", node.ToJsonString());
        }

        [Fact]
        public void Replace_null节点不抛_直接返回null()
        {
            Assert.Null(JsonEx.Replace(null, "x", JsonValue.Create(1)));
        }

        // ══════════════════════════════════════════════════════════════════
        // API 面
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void API面_JsonEx是静态类_共14个公开静态方法()
        {
            Type t = typeof(JsonEx);

            Assert.True(t.IsAbstract && t.IsSealed, "JsonEx 必须是 static class");

            MethodInfo[] methods = t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Assert.Equal(14, methods.Length);

            Assert.Equal(2, methods.Count(m => m.Name == "ToJson"));     // 泛型 T + Type
            Assert.Equal(2, methods.Count(m => m.Name == "Save"));
            Assert.Equal(2, methods.Count(m => m.Name == "Read"));
            Assert.Equal(2, methods.Count(m => m.Name == "TryRead"));
            Assert.Equal(1, methods.Count(m => m.Name == "FromJson"));
            Assert.Equal(1, methods.Count(m => m.Name == "ToJsonNode"));
            Assert.Equal(1, methods.Count(m => m.Name == "ToList"));
            Assert.Equal(1, methods.Count(m => m.Name == "Replace"));

            // 6 个泛型方法：ToJson<T> / FromJson<T> / Read<T>×2 / TryRead<T>×2
            Assert.Equal(6, methods.Count(m => m.IsGenericMethodDefinition));
        }

        [Fact]
        public void API面_8个是扩展方法_6个是普通静态方法()
        {
            MethodInfo[] methods = typeof(JsonEx)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

            string[] ext = methods
                .Where(m => m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
                .Select(m => m.Name).OrderBy(n => n).ToArray();
            string[] plain = methods
                .Where(m => !m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
                .Select(m => m.Name).OrderBy(n => n).ToArray();

            Assert.Equal(8, ext.Length);
            Assert.Equal(6, plain.Length);

            // ⚠️ API 面不对称（未改，只记录）：Save 是扩展方法，Read / TryRead 却**不是**
            //    ⇒ 可以写 data.Save(path)，但不能写 path.Read<T>()，只能 JsonEx.Read<T>(path)。
            Assert.Contains("Save", ext);
            Assert.Contains("ToJson", ext);
            Assert.Contains("ToJsonNode", ext);
            Assert.Contains("ToList", ext);
            Assert.Contains("Replace", ext);
            Assert.Contains("FromJson", ext);

            Assert.Equal(new[] { "LoadStaticClass", "Read", "Read", "SaveStaticClass", "TryRead", "TryRead" }, plain);
        }
    }
}
