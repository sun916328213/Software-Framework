using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using ScriptEditor.Services;

namespace ScriptEditor.Tests;

/// <summary>
/// ScriptEngine globals 注入路径测试：
/// 覆盖 Dictionary 索引、class 成员访问、List 集合、嵌套成员、静态/实例 Run 形式。
/// 直接调用 ScriptEngine（无头引擎），不启动 WPF UI。
/// </summary>
public static class ScriptEngineGlobalsTest
{
    /// <summary>测试用具名 class（匹配用户描述的 Testclass 结构）。</summary>
    public class TestItem
    {
        public string ID { get; set; } = "";
        public string Name { get; set; } = "";
    }

    /// <summary>订单头（含 Items 列表）。</summary>
    public class OrderHeader
    {
        public string Header { get; set; } = "";
        public TestItem[] Items { get; set; } = Array.Empty<TestItem>();
    }

    /// <summary>
    /// 运行脚本并断言：执行成功且 OutputText 包含全部期望片段，否则抛异常。
    /// </summary>
    private static async Task RunAndVerify(string name, object? input, string code, params string[] expectedContains)
    {
        var engine = new ScriptEngine(() => code);
        engine.SetInputData(input);
        var ok = await engine.RunAsync();
        var output = engine.OutputText ?? "";
        if (!ok)
            throw new Exception($"[{name}] 执行失败: StatusText='{engine.StatusText}', Output='{output}'");
        foreach (var exp in expectedContains)
        {
            if (!output.Contains(exp, StringComparison.Ordinal))
                throw new Exception($"[{name}] 输出缺少 '{exp}': 实际='{output}'");
        }
        Trace.WriteLine($"[GlobalsTest][{name}] OK: {output.Replace("\r\n", " | ").Trim()}");
    }

    /// <summary>自启模式：用于在没有宿主 App 时直接运行测试。用法：STAThread 控制台调用本方法。</summary>
    [STAThread]
    public static void RunSelfContained()
    {
        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try
        {
            RunAsync().GetAwaiter().GetResult();
        }
        finally
        {
            Trace.WriteLine("[GlobalsTest] 退出");
            app.Shutdown();
        }
    }

    public static async Task RunAsync()
    {
        Trace.WriteLine("[GlobalsTest] ====== 开始 ======");

        // 组 1：Dictionary<string, object?> 输入 → 索引器 Inputs["key"]
        await RunAndVerify("dict-顶层代码",
            new Dictionary<string, object?> { ["x"] = 42, ["s"] = "hello" },
            "var x = Inputs[\"x\"];\r\nvar s = Inputs[\"s\"];\r\nConsole.WriteLine($\"x={x}, s={s}\");",
            "x=42, s=hello");

        await RunAndVerify("dict-实例Script",
            new Dictionary<string, object?> { ["A"] = 42, ["B"] = "hello" },
            """
            public class Script
            {
                public void Run()
                {
                    var a = Inputs["A"];
                    var b = Inputs["B"];
                    Console.WriteLine($"A={a}, B={b}");
                }
            }
            """,
            "A=42, B=hello");

        // 组 2：任意 class 输入 → 动态成员访问 Inputs.Prop
        await RunAndVerify("class-单对象",
            new TestItem { ID = "T001", Name = "测试项" },
            """
            public class Script
            {
                public void Run()
                {
                    Console.WriteLine($"ID={Inputs.ID}, Name={Inputs.Name}");
                }
            }
            """,
            "ID=T001, Name=测试项");

        await RunAndVerify("class-嵌套List",
            new OrderHeader
            {
                Header = "订单头",
                Items = new[]
                {
                    new TestItem { ID = "L1", Name = "行1" },
                    new TestItem { ID = "L2", Name = "行2" }
                }
            },
            """
            public class Script
            {
                public void Run()
                {
                    Console.WriteLine($"Header={Inputs.Header}");
                    foreach (var t in Inputs.Items)
                        Console.WriteLine($"Item={t.ID}-{t.Name}");
                }
            }
            """,
            "Header=订单头", "Item=L1-行1", "Item=L2-行2");

        // 组 3：List<class> 集合 → foreach 遍历
        await RunAndVerify("list-集合",
            new[]
            {
                new TestItem { ID = "A1", Name = "项1" },
                new TestItem { ID = "A2", Name = "项2" },
                new TestItem { ID = "A3", Name = "项3" }
            },
            """
            public class Script
            {
                public void Run()
                {
                    int n = 0;
                    foreach (var item in Inputs)
                    {
                        Console.WriteLine($"[{n}] ID={item.ID}, Name={item.Name}");
                        n++;
                    }
                    Console.WriteLine($"total={n}");
                }
            }
            """,
            "[0] ID=A1, Name=项1", "total=3");

        // 组 4：Dictionary<string, object> 真实宿主（业务层塞任意类型，脚本自行解析）
        await RunAndVerify("dict-object宿主",
            new Dictionary<string, object>
            {
                ["A"] = 42,
                ["B"] = "hello",
                ["C"] = new TestItem { ID = "X1", Name = "嵌套" }
            },
            """
            public class Script
            {
                public void Run()
                {
                    var a = Inputs["A"];
                    var b = Inputs["B"];
                    var c = Inputs["C"];
                    Console.WriteLine($"A={a}, B={b}, C.Type={c.GetType().Name}");
                }
            }
            """,
            "A=42, B=hello", "C.Type=TestItem");

        // 组 5：静态 Run 形式（suffix 注入 Script.Inputs = Inputs;）
        await RunAndVerify("静态Run",
            new Dictionary<string, object?> { ["A"] = 7 },
            """
            public class Script
            {
                public static void Run()
                {
                    var a = Inputs["A"];
                    Console.WriteLine($"A={a}");
                }
            }
            """,
            "A=7");

        // 组 6：实例 Run + Initialize（同实例共享字段）
        await RunAndVerify("实例Run+Initialize",
            new Dictionary<string, object?> { ["A"] = 42 },
            """
            public class Script
            {
                public dynamic? Saved;
                public void Initialize()
                {
                    Saved = "init-ok";
                }
                public void Run()
                {
                    var a = Inputs["A"];
                    Console.WriteLine($"A={a}, Saved={Saved}");
                }
            }
            """,
            "A=42", "Saved=init-ok");

        // 组 7：未设输入 → 空字典，可安全访问（不抛 NRE）
        await RunAndVerify("null输入",
            null,
            """
            public class Script
            {
                public void Run()
                {
                    Console.WriteLine($"count={Inputs.Count}");
                }
            }
            """,
            "count=0");

        Trace.WriteLine("[GlobalsTest] ====== 完成 ======");
    }
}