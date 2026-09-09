using FlowEditor.Core;
using FlowEditor.Nodes.FlowControl;
using Xunit;

namespace FlowEditorTest;

public class InactiveNodeContainerTests
{
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
