// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ReferenceResolverTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Core.Models、ScriptEditor.Runtime.Services、Xunit。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Core.Models;
using ScriptEditor.Runtime.Services;
using Xunit;

namespace ScriptEditor.Tests.Runtime;

/// <summary>引用解析：去重、缓存命中、缺失跳过（无 WPF、无 IO 副作用）。</summary>
public sealed class ReferenceResolverTests
{
    /// <summary>
    /// 测试相关入口 ResolveDedupsByPathCaseInsensitive；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Resolve、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 LoadSkipsBlankAndMissing；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 LoadMetadataReferences、Empty；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 LoadCachesSameFileInstance；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 LoadMetadataReferences、ToUpperInvariant、Single、Same；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 DefaultReferencesAreAvailable；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 GetAdditionalDefaultReferences、NotEmpty、CollectReferencePaths、Equal、Count；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DefaultReferencesAreAvailable()
    {
        var references = RuntimeReferenceCache.Shared.GetAdditionalDefaultReferences();
        Assert.NotEmpty(references);
        var paths = RuntimeReferenceCache.CollectReferencePaths(references);
        Assert.Equal(paths.Count, references.Count(r => !string.IsNullOrWhiteSpace(r.Display)));
    }
}
