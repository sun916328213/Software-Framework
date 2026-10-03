// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：AssemblyCutoverTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.IO、System.Linq、System.Runtime.CompilerServices、System.Text.Json、Extension.Core.Collections。导入不代表所有类型都会在运行时加载。
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Extension.Core.Collections;
using Extension.Core.Reflection;
using Extension.UI.Controls;
using Xunit;

namespace Extension.Tests;

/// <summary>
/// AssemblyCutoverTests 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class AssemblyCutoverTests
{
    /// <summary>
    /// 验证 OriginalAssemblyDoesNotDefineOrForwardAnyExtractedType 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、GetName、Empty、GetForwardedTypes、Parse；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void OriginalAssemblyDoesNotDefineOrForwardAnyExtractedType()
    {
        var original = typeof(DelayEx).Assembly;
        var core = typeof(ConcurrentList<>).Assembly;
        Assert.Equal("Extension", original.GetName().Name);
        Assert.Equal("Extension.Core", core.GetName().Name);
        Assert.Empty(original.GetForwardedTypes());
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "Build/Baselines/Architecture-S5-migrations.json")));
        foreach (var row in document.RootElement.GetProperty("movedPublicTypes").EnumerateArray())
        {
            Assert.Null(original.GetType(row.GetString()!));
            Assert.NotNull(core.GetType(row.GetString()!));
        }
    }

    /// <summary>
    /// 验证 ExplicitOldAssemblyIdentityIsNotRedirectedToCore 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、GetName、Same、ResolvePersistedType、Null；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ExplicitOldAssemblyIdentityIsNotRedirectedToCore()
    {
        // Load the real old assembly so the resolver cannot fall back to an unqualified lookup.
        Assert.Equal("Extension", typeof(DelayEx).Assembly.GetName().Name);
        var type = typeof(ConcurrentList<int>);
        Assert.Same(type, FrameworkTypeResolver.ResolvePersistedType(type.AssemblyQualifiedName));
        const string oldName = "Extension.Core.Collections.ConcurrentList`1[[System.Int32, System.Private.CoreLib]], Extension";
        Assert.Null(Type.GetType(oldName, throwOnError: false));
        Assert.Null(FrameworkTypeResolver.ResolvePersistedType(oldName));
    }

    /// <summary>
    /// 验证 Root 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetFullPath、Combine、GetDirectoryName；具体条件和先后次序见方法体。
    /// </summary>
    private static string Root([CallerFilePath] string file = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, "..", "..", "..", ".."));
}
