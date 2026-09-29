using System.Diagnostics;
using System.Windows;
using ScriptEditor.Models;
using ScriptEditor.Views;
using Script.Tests.Core;
using Script.Tests.CoreTests;

namespace Script.Tests.UiTests;

/// <summary>
/// ScriptEditorView 加载性能测试工具（含引用程序集解析验证）。
/// 观察 Visual Studio 输出窗口（Debug）中的 [ScriptEditor] 和 [LoadTest] 日志。
/// </summary>
public static class ScriptEditorLoadTest
{
    /// <summary>
    /// 创建测试窗口，包含一个模拟的 ScriptEditorView，模拟 Tab 选中后的完整初始化流程，
    /// 输出各阶段耗时，并验证全局引用程序集（XDocument / System.Text.Json）可被脚本解析。
    /// </summary>
    public static async Task RunAsync(TestContext ctx)
    {
        Debug.WriteLine($"[LoadTest] ====== 开始性能测试 ======");

        // 先跑 globals 注入单元测试（无需 UI 即可执行）
        await ScriptEngineGlobalsTest.RunAsync();

        // 构造模拟的 ScriptItem
        var totalSw = Stopwatch.StartNew();
        var mockItem = new ScriptItem
        {
            Title = "加载性能测试",
            Code = @"// 这是一个模拟的启动脚本
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// 测试代码
var message = ""Hello from load test!"";
Console.WriteLine(message);

// 模拟一些业务逻辑
var items = Enumerable.Range(1, 100).ToList();
var result = items.Where(x => x % 2 == 0).Sum();
Console.WriteLine($""Sum of even numbers: {result}"");
"
        };
        mockItem.References.Add(typeof(System.Xml.Linq.XDocument).Assembly.Location);
        mockItem.References.Add(typeof(System.Text.Json.JsonSerializer).Assembly.Location);
        ctx.Metric("ScriptItem 创建耗时", totalSw.Elapsed.TotalMilliseconds);
        Debug.WriteLine($"[LoadTest] ScriptItem 创建完成: {totalSw.ElapsedMilliseconds}ms");

        // 创建 ScriptEditorView（构造函数只做轻量 InitializeComponent）
        var sw = Stopwatch.StartNew();
        var editorView = new ScriptEditorView();
        ctx.Metric("ScriptEditorView 构造耗时", sw.Elapsed.TotalMilliseconds);
        Debug.WriteLine($"[LoadTest] ScriptEditorView 构造完成: {sw.ElapsedMilliseconds}ms");

        var window = new Window
        {
            Title = "ScriptEditor 加载性能测试",
            Width = 1200,
            Height = 800,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = editorView
        };
        window.Show();
        DispatcherUtil.Pump();

        // 设置 DataContext，自动触发完整初始化链路
        sw.Restart();
        editorView.DataContext = mockItem;
        ctx.Metric("DataContext 设置耗时", sw.Elapsed.TotalMilliseconds);
        Debug.WriteLine($"[LoadTest] DataContext 设置完成: {sw.ElapsedMilliseconds}ms");

        sw.Restart();
        var success = await editorView.EnsureInitializedAsync(timeoutMs: 30000);
        ctx.Metric("EnsureInitializedAsync 耗时", sw.Elapsed.TotalMilliseconds);
        ctx.Metric("总耗时", totalSw.Elapsed.TotalMilliseconds);
        ctx.Assert(success, "编辑器初始化超时");

        Debug.WriteLine($"[LoadTest] ====== 初始化完成 ======");
        Debug.WriteLine($"[LoadTest] 是否成功: {success}");
        Debug.WriteLine($"[LoadTest] 总耗时 (从 Run 调用): {totalSw.ElapsedMilliseconds}ms");

        window.Close();
        DispatcherUtil.Pump();
    }
}
