using FlowEditor.ViewModels;

namespace FlowEditor.Interfaces;

/// <summary>创建独立流程标签页 ViewModel 的宿主 seam。</summary>
public interface IFlowTabFactory
{
    /// <summary>创建一个具有独立流程会话的标签页。</summary>
    /// <returns>新的流程标签页 ViewModel。</returns>
    FlowChartViewModel Create();
}
