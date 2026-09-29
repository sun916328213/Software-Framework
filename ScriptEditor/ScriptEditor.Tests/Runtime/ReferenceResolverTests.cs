using ScriptEditor.Models;
using ScriptEditor.Services;
using Xunit;

namespace ScriptEditor.Tests.Runtime;

/// <summary>引用解析：去重、缓存命中、缺失跳过（无 WPF、无 IO 副作用）。</summary>
public sealed class ReferenceResolverTests
{
    [Fact]
    public void ResolveDedupsByPathCaseInsensitive()
    {
        var resolver = new RoslynScriptReferenceResolver();
        var project = new ScriptProject
        {
            References = new List<ScriptReference> { new(@"C:\libs\a.dll", "a.dll") }
        };
        var script = new ScriptProjectItem
        {
            References = new List<ScriptReference>
            {
                new(@"c:\LIBS\a.dll", "a.dll"),
                new(@"C:\libs\b.dll", "b.dll")
            }
        };

        var resolved = resolver.Resolve(project, script);
        Assert.Equal(2, resolved.Count);
    }

    [Fact]
    public void LoadSkipsBlankAndMissing()
    {
        var resolver = new RoslynScriptReferenceResolver();
        var references = resolver.LoadMetadataReferences(new[]
        {
            new ScriptReference(string.Empty, string.Empty),
            new ScriptReference("  ", "blank.dll"),
            new ScriptReference(@"Z:\definitely\missing-driver.dll", "missing-driver.dll")
        });
        Assert.Empty(references);
    }

    [Fact]
    public void LoadCachesSameFileInstance()
    {
        var path = typeof(object).Assembly.Location;
        var resolver = new RoslynScriptReferenceResolver();
        var first = resolver.LoadMetadataReferences(new[]
        {
            new ScriptReference(path, "core.dll"),
            new ScriptReference(path.ToUpperInvariant(), "core.dll")
        });
        var second = resolver.LoadMetadataReferences(new[]
        {
            new ScriptReference(path, "core.dll")
        });
        Assert.Single(first);
        Assert.Same(first[0], second[0]);
    }

    [Fact]
    public void DefaultReferencesAreAvailable()
    {
        var references = RuntimeReferenceCache.Shared.GetAdditionalDefaultReferences();
        Assert.NotEmpty(references);
        var paths = RuntimeReferenceCache.CollectReferencePaths(references);
        Assert.Equal(paths.Count, references.Count(r => !string.IsNullOrWhiteSpace(r.Display)));
    }
}
