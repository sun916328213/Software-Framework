// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：InactiveNodeContainerTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Nodes.FlowControl、Xunit。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Nodes.FlowControl;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// InactiveNodeContainerTests 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class InactiveNodeContainerTests
{
    /// <summary>
    /// 测试相关入口 Container_IsToolboxDiscoverableAndExcludedFromExecutionByDefault；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 GetCustomAttribute、NotNull、Equal、True、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Container_IsToolboxDiscoverableAndExcludedFromExecutionByDefault()
    {
        var attribute = Attribute.GetCustomAttribute(
            typeof(InactiveNodeContainerNode), typeof(FlowToolAttribute)) as FlowToolAttribute;
        var container = new InactiveNodeContainerNode();

        Assert.NotNull(attribute);
        Assert.Equal("不执行容器", attribute!.ToolName);
        Assert.True(container.IsExcludedFromExecution);
        Assert.False(container.IsInEnabled);
        Assert.False(container.IsOutEnabled);
    }
}
