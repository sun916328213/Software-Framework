namespace FlowEditor.Core;

/// <summary>
/// 主画布结构化执行容器的编辑器接口；Runtime 调度属性继承自最小结构契约。
/// </summary>
public interface IExecutionScopeContainer : IExecutionScopeDescriptor
{
    /// <summary>编辑器中的容器宽度。</summary>
    double ContainerWidth { get; set; }

    /// <summary>编辑器中的容器高度。</summary>
    double ContainerHeight { get; set; }

    /// <summary>是否在容器边缘显示自身的流程端口。</summary>
    bool ShowBoundaryPorts { get; }

}
