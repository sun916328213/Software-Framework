// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ScriptModelTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Text.Json、ScriptEditor.Core.Models、Xunit。导入不代表所有类型都会在运行时加载。
using System.Text.Json;
using ScriptEditor.Core.Models;
using Xunit;

namespace ScriptEditor.Tests.Core;

/// <summary>P2 数据模型：纯数据、可独立序列化（无 WPF、无 Roslyn、无 IO）。</summary>
public sealed class ScriptModelTests
{
    /// <summary>
    /// 验证 ReferenceDescriptionKeepsPathAndAssemblyName 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 SetReferences、Single、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ReferenceDescriptionKeepsPathAndAssemblyName()
    {
        var item = new ScriptItem();
        item.SetReferences(new[] { @"C:\scripts\assemblies\Driver.dll" });

        Assert.Single(item.ScriptReferences);
        Assert.Equal(@"C:\scripts\assemblies\Driver.dll", item.ScriptReferences[0].Path);
        Assert.Equal("Driver.dll", item.ScriptReferences[0].Name);
    }

    /// <summary>
    /// 验证 ProjectItemAlwaysHasValidId 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 NotEqual；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ProjectItemAlwaysHasValidId()
    {
        var script = new ScriptProjectItem { ScriptId = Guid.Empty };
        Assert.NotEqual(Guid.Empty, script.ScriptId);
    }

    /// <summary>
    /// 验证 BreakpointsRoundTrip 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Add、Deserialize、Serialize、NotNull、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void BreakpointsRoundTrip()
    {
        var item = new ScriptItem { Title = "debug", Code = "Console.WriteLine(1);" };
        item.Breakpoints.Add(4);
        item.Breakpoints.Add(9);

        var copy = JsonSerializer.Deserialize<ScriptItem>(JsonSerializer.Serialize(item));
        Assert.NotNull(copy);
        Assert.Equal(new[] { 4, 9 }, copy.Breakpoints);
    }

    /// <summary>
    /// 验证 CopySemanticsKeepContentWithNewId 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 SetReferences、Add、NotEqual、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CopySemanticsKeepContentWithNewId()
    {
        var original = new ScriptItem { Title = "source", Code = "Console.WriteLine(42);" };
        original.SetReferences(new[] { "missing-driver.dll" });
        original.Breakpoints.Add(3);

        var copy = new ScriptItem { Title = $"{original.Title} - 副本", Code = original.Code };
        copy.SetReferences(original.References);
        foreach (var line in original.Breakpoints)
            copy.Breakpoints.Add(line);

        Assert.NotEqual(original.ScriptId, copy.ScriptId);
        Assert.Equal(original.Code, copy.Code);
        Assert.Equal(original.References, copy.References);
        Assert.Equal(original.Breakpoints, copy.Breakpoints);
    }

    /// <summary>
    /// 验证 ViewStateIsNotPersisted 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Serialize、DoesNotContain；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ViewStateIsNotPersisted()
    {
        var item = new ScriptItem { Title = "t", Code = "Console.WriteLine(1);" };
        item.CaretOffset = 12;
        item.EditorScrollOffset = 34.5;
        item.ScrollOffset = 56.5;

        var json = JsonSerializer.Serialize(item);
        Assert.DoesNotContain("CaretOffset", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ScrollOffset", json, StringComparison.Ordinal);
    }
}
