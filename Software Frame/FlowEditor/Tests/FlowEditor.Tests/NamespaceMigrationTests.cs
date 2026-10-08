// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：NamespaceMigrationTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Core、FlowEditor.Core.Models、Xunit。导入不代表所有类型都会在运行时加载。
using FlowEditor.Core;
using FlowEditor.Core.Models;
using Xunit;

namespace FlowEditor.Tests;

/// <summary>
/// NamespaceMigrationTests 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class NamespaceMigrationTests
{
    /// <summary>
    /// 测试相关入口 LegacyDocumentNameResolvesWithoutExtensionAssemblyDependency；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、ResolvePersistedType、DoesNotContain、GetReferencedAssemblies；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void LegacyDocumentNameResolvesWithoutExtensionAssemblyDependency()
    {
        Assert.Equal(typeof(NodeDocumentModel), FrameworkTypeResolver.ResolvePersistedType("FlowEditor.Models.NodeDocumentModel, FlowEditor.Core"));
        Assert.DoesNotContain(typeof(FrameworkTypeResolver).Assembly.GetReferencedAssemblies(), name => name.Name == "Extension");
    }

    /// <summary>
    /// 测试相关入口 LegacyGenericArgumentResolvesToCurrentDocumentType；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、ResolvePersistedType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void LegacyGenericArgumentResolvesToCurrentDocumentType()
    {
        var name = "System.Collections.Generic.List`1[[FlowEditor.Models.NodeDocumentModel, FlowEditor.Core]], System.Private.CoreLib";
        Assert.Equal(typeof(List<NodeDocumentModel>), FrameworkTypeResolver.ResolvePersistedType(name));
    }

    /// <summary>
    /// 测试相关入口 HistoricalAssemblyNameResolvesAfterModuleAssemblyRename；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、ResolvePersistedType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void HistoricalAssemblyNameResolvesAfterModuleAssemblyRename()
    {
        Assert.Equal(typeof(NodeDocumentModel), FrameworkTypeResolver.ResolvePersistedType("FlowEditor.Models.NodeDocumentModel, FlowEditor"));
    }

    /// <summary>
    /// 测试相关入口 UnknownTypeRemainsUnresolved；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Null、ResolvePersistedType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void UnknownTypeRemainsUnresolved()
    {
        Assert.Null(FrameworkTypeResolver.ResolvePersistedType("FlowEditor.Models.UnknownNode, FlowEditor.Core"));
    }
}
