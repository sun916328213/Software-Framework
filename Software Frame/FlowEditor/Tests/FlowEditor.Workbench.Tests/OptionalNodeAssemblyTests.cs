// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：OptionalNodeAssemblyTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.IO、Xunit。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.IO;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// OptionalNodeAssemblyTests 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class OptionalNodeAssemblyTests
{
    /// <summary>
    /// 测试相关入口 MissingOptionalAssemblyAllowsBuiltInNodes；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 LoadOptionalFlowNodes、Null；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MissingOptionalAssemblyAllowsBuiltInNodes()
    {
        var result = FlowEditorModule.LoadOptionalFlowNodes(() =>
            throw new FileNotFoundException("Not deployed", "FlowNodes, Version=1.0.0.0"));
        Assert.Null(result);
    }

    /// <summary>
    /// 测试相关入口 MissingPluginDependencyKeepsOriginalCause；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、LoadOptionalFlowNodes、Same、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MissingPluginDependencyKeepsOriginalCause()
    {
        var cause = new FileNotFoundException("Missing vendor SDK", "Vendor.Camera");
        var error = Assert.Throws<InvalidOperationException>(() =>
        {
            FlowEditorModule.LoadOptionalFlowNodes(() => throw cause);
        });
        Assert.Same(cause, error.InnerException);
        Assert.Contains("FlowNodes", error.Message);
    }

    /// <summary>
    /// 测试相关入口 BrokenPluginIsNotTreatedAsMissingOptionalAssembly；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、LoadOptionalFlowNodes、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void BrokenPluginIsNotTreatedAsMissingOptionalAssembly()
    {
        var cause = new BadImageFormatException("Invalid plugin");
        var error = Assert.Throws<InvalidOperationException>(() =>
        {
            FlowEditorModule.LoadOptionalFlowNodes(() => throw cause);
        });
        Assert.Same(cause, error.InnerException);
    }
}
