namespace FlowEditor.ViewModels;

/// <summary>
/// 创建独立流程标签页 ViewModel 的同层 seam。
/// 由 Presentation adapter 实现，避免通用 Interfaces 模块反向依赖 ViewModel。
/// </summary>
public interface IFlowTabFactory
{
    /// <summary>创建一个具有独立流程会话的标签页。</summary>
    /// <returns>新的流程标签页 ViewModel。</returns>
    FlowChartViewModel Create();
}
