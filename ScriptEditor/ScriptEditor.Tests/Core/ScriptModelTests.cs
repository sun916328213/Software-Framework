using System.Text.Json;
using ScriptEditor.Models;
using Xunit;

namespace ScriptEditor.Tests.Core;

/// <summary>P2 数据模型：纯数据、可独立序列化（无 WPF、无 Roslyn、无 IO）。</summary>
public sealed class ScriptModelTests
{
    [Fact]
    public void ReferenceDescriptionKeepsPathAndAssemblyName()
    {
        var item = new ScriptItem();
        item.SetReferences(new[] { @"C:\scripts\assemblies\Driver.dll" });

        Assert.Single(item.ScriptReferences);
        Assert.Equal(@"C:\scripts\assemblies\Driver.dll", item.ScriptReferences[0].Path);
        Assert.Equal("Driver.dll", item.ScriptReferences[0].Name);
    }

    [Fact]
    public void ProjectItemAlwaysHasValidId()
    {
        var script = new ScriptProjectItem { ScriptId = Guid.Empty };
        Assert.NotEqual(Guid.Empty, script.ScriptId);
    }

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
